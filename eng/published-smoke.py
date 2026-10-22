#!/usr/bin/env python3
"""Extract and launch the real self-contained apphost. Never builds a verifier or references source projects."""
import argparse, hashlib, json, os, platform, shutil, sqlite3, stat, struct, subprocess, tarfile, time, urllib.parse, zipfile
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath


def digest(path):
    value=hashlib.sha256()
    with path.open('rb') as data:
        for block in iter(lambda:data.read(1024*1024),b''):value.update(block)
    return value.hexdigest()


def machine_types(path):
    with path.open('rb') as data:
        header=data.read(4096)
        if header[:2]==b'MZ':
            offset=struct.unpack_from('<I',header,0x3c)[0];data.seek(offset)
            pe=data.read(6)
            if pe[:4]!=b'PE\0\0':raise ValueError('Invalid PE header')
            return [hex(struct.unpack_from('<H',pe,4)[0])]
        if header[:4]==b'\x7fELF':return [hex(struct.unpack_from('<H',header,18)[0])]
        if header[:4]==b'\xcf\xfa\xed\xfe':return [hex(struct.unpack_from('<I',header,4)[0])]
        if header[:4]==b'\xca\xfe\xba\xbe':
            count=struct.unpack_from('>I',header,4)[0]
            if count>16:raise ValueError('Invalid fat Mach-O architecture count')
            return [hex(struct.unpack_from('>I',header,8+i*20)[0]) for i in range(count)]
    raise ValueError('Unknown executable format: '+path.name)


def unpack(archive,target):
    total=0
    def safe(name,size,regular=True):
        nonlocal total
        path=PurePosixPath(name)
        if path.is_absolute() or '..' in path.parts or '\\' in name or ':' in name or not regular:raise ValueError('Unsafe archive member')
        total+=size
        if total>1_500_000_000:raise ValueError('Archive exceeds verification extraction limit')
        return target.joinpath(*path.parts)
    if archive.suffix=='.zip':
        with zipfile.ZipFile(archive) as package:
            if len(package.infolist())>5000 or sum(m.file_size for m in package.infolist())>1_500_000_000:raise ValueError('Invalid/oversized archive')
            for member in package.infolist():
                kind=stat.S_IFMT(member.external_attr>>16)
                path=safe(member.filename,member.file_size,kind!=stat.S_IFLNK)
                if member.is_dir():path.mkdir(parents=True,exist_ok=True);continue
                path.parent.mkdir(parents=True,exist_ok=True)
                with package.open(member) as source,path.open('xb') as output:shutil.copyfileobj(source,output)
    else:
        with tarfile.open(archive) as package:
            for index,member in enumerate(package):
                if index>=5000:raise ValueError('Too many archive members')
                path=safe(member.name,member.size,member.isfile() or member.isdir())
                if member.isdir():path.mkdir(parents=True,exist_ok=True);continue
                path.parent.mkdir(parents=True,exist_ok=True)
                with package.extractfile(member) as source,path.open('xb') as output:shutil.copyfileobj(source,output)
                path.chmod(member.mode&0o777)


def history(data):
    path=data/'history.v1.db'
    with sqlite3.connect('file:'+urllib.parse.quote(str(path),safe='/:')+'?mode=ro',uri=True,timeout=5) as connection:
        if connection.execute('PRAGMA quick_check').fetchone()[0]!='ok':raise RuntimeError('History integrity failed')
        sessions=[json.loads(r[0]) for r in connection.execute('SELECT payload FROM sessions')]
        events=[json.loads(r[0]) for r in connection.execute('SELECT payload FROM events ORDER BY sequence')]
        outages=connection.execute('SELECT COUNT(*) FROM outages').fetchone()[0]
    return sessions,events,outages


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package',type=Path,required=True);parser.add_argument('--rid',required=True,choices=['win-x64','osx-arm64','linux-x64'])
    parser.add_argument('--output',type=Path,required=True);parser.add_argument('--expected-revision');parser.add_argument('--seconds',type=int,default=2)
    args=parser.parse_args()
    if not 1<=args.seconds<=300:parser.error('seconds must be 1–300')
    actual_platform={'Windows':'win-x64','Darwin':'osx-arm64','Linux':'linux-x64'}.get(platform.system())
    arch=platform.machine().lower()
    if actual_platform!=args.rid or (args.rid=='osx-arm64' and arch not in ['arm64','aarch64']) or (args.rid!='osx-arm64' and arch not in ['amd64','x86_64']):
        parser.error('Published apphost must run on its native target architecture/OS')
    output=args.output.resolve()
    if output.exists() and any(output.iterdir()):parser.error('Output must be new or empty; no existing evidence will be removed')
    output.mkdir(parents=True,exist_ok=True)
    package=args.package.resolve();checksum=package.with_name(package.name+'.sha256')
    package_hash=digest(package)
    if not checksum.exists() or checksum.read_text().split()[0]!=package_hash:raise ValueError('Package sidecar SHA-256 mismatch')
    unpack(package,output/'unpacked');root=output/'unpacked'/('Synups-'+args.rid)
    manifest=json.loads((root/'ARTIFACT_MANIFEST.json').read_text())
    if manifest.get('rid')!=args.rid or manifest.get('installation_check_protocol')!=1:raise ValueError('Archive does not declare the supported apphost-check protocol')
    if args.expected_revision and manifest.get('source_revision')!=args.expected_revision:raise ValueError('Unexpected source revision')
    for name,expected in manifest['files_sha256'].items():
        if digest(root/name)!=expected:raise ValueError('Published file hash mismatch: '+name)
    actual_files={p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()}
    if actual_files!=set(manifest['files_sha256'])|{'ARTIFACT_MANIFEST.json'}:raise ValueError('Unexpected/missing packaged files')
    executable=root/manifest['apphost_relative_path']
    expected_machine={'win-x64':'0x8664','osx-arm64':'0x100000c','linux-x64':'0x3e'}[args.rid]
    if expected_machine not in machine_types(executable):raise ValueError('Wrong apphost architecture')
    runtime_root=root/manifest.get('runtime_relative_path',str(Path(manifest['apphost_relative_path']).parent))
    sqlite=runtime_root/{'win-x64':'e_sqlite3.dll','osx-arm64':'libe_sqlite3.dylib','linux-x64':'libe_sqlite3.so'}[args.rid]
    if expected_machine not in machine_types(sqlite):raise ValueError('Wrong native SQLite architecture')
    if args.rid!='win-x64' and executable.stat().st_mode&0o111==0:raise ValueError('Executable permission missing')
    if args.rid=='osx-arm64':subprocess.run(['codesign','--verify','--deep','--strict',str(root/'Synups.app')],check=True)
    environment=os.environ.copy()
    for key in list(environment):
        if key=='DOTNET_ROOT' or key.startswith('DOTNET_ROOT_'):environment.pop(key)
    data=output/'isolated-data';reports=output/'reports';reports.mkdir()
    launches=[];identity=None
    for stage in ['exercise','restart','interruption','recover','storage-error','storage-recovered']:
        command=[str(executable),'--data-directory',str(data),'--verify-installation',str(reports),'--verification-stage',stage,'--observe-seconds',str(args.seconds)]
        if stage=='exercise':command.append('--minimized')
        started=datetime.now(timezone.utc).isoformat();report_path=reports/(stage+'.json')
        database=data/'history.v1.db';original_mode=None;readonly_hash=None
        if stage=='storage-error':
            original_mode=stat.S_IMODE(database.stat().st_mode);readonly_hash=digest(database)
            database.chmod(stat.S_IREAD)
        with (reports/(stage+'-console.txt')).open('w',encoding='utf-8') as log:
            process=subprocess.Popen(command,cwd=executable.parent,stdout=log,stderr=subprocess.STDOUT,env=environment)
            try:
                if stage=='interruption':
                    deadline=time.monotonic()+args.seconds+120
                    while not report_path.exists():
                        if process.poll() is not None:raise RuntimeError('Apphost exited before interruption checkpoint')
                        if time.monotonic()>deadline:raise TimeoutError('Interruption checkpoint did not arrive')
                        time.sleep(.2)
                    report=json.loads(report_path.read_text())
                    if report['status']!='ready-for-termination':raise RuntimeError('Apphost did not reach a real persisted monitoring session')
                    sessions,events,outages=history(data)
                    if not any(s['EndedUtc'] is None for s in sessions):raise RuntimeError('No active session before controlled termination')
                    process.kill();code=process.wait(timeout=10)
                else:code=process.wait(timeout=args.seconds+140)
            except BaseException:
                if process.poll() is None:process.kill();process.wait(timeout=10)
                raise
            finally:
                if original_mode is not None:database.chmod(original_mode)
        if not report_path.exists():raise RuntimeError('Actual apphost exited '+str(code)+' before producing a report; inspect '+str(reports/(stage+'-console.txt')))
        report=json.loads(report_path.read_text())
        if report['process_id']!=process.pid or Path(report['executable']).resolve()!=executable.resolve():raise RuntimeError('Report was not emitted by the launched packaged apphost')
        if report['apphost_sha256']!=manifest['files_sha256'][manifest['apphost_relative_path']]:raise RuntimeError('Executed apphost hash mismatch')
        if Path(report['core_library_path']).resolve().parent!=runtime_root.resolve():raise RuntimeError('Apphost did not use its packaged .NET runtime')
        if stage!='interruption' and (code!=0 or report['status']!='passed'):raise RuntimeError('Actual apphost verification failed; inspect '+str(report_path))
        configuration=json.loads((data/'configuration.v1.json').read_text())
        groups=configuration['Groups']
        if len(groups)!=1 or len(groups[0]['Targets'])!=1 or groups[0]['Targets'][0]['Address']!='127.0.0.1':raise RuntimeError('Actual editor did not persist the isolated loopback profile')
        observed=(groups[0]['Id'],groups[0]['Targets'][0]['Id'])
        if identity is None:identity=observed
        elif identity!=observed:raise RuntimeError('Profile identities changed across real process restart')
        sessions,events,outages=history(data)
        if any(e['Kind']==3 for e in events):raise RuntimeError('Healthy stop/restart invented a recovery')
        if stage!='interruption' and any(s['EndedUtc'] is None and not s['Interrupted'] for s in sessions):raise RuntimeError('Clean process exit left an unended live session')
        if stage in ['recover','storage-recovered'] and (sum(s['Interrupted'] for s in sessions)!=1 or sum(e['Kind']==7 for e in events)!=1):raise RuntimeError('Controlled termination was not preserved exactly once')
        if stage=='storage-error' and digest(database)!=readonly_hash:raise RuntimeError('Read-only history bytes were changed')
        launches.append(dict(stage=stage,pid=process.pid,started_utc=started,exit_code=code,expected_external_termination=stage=='interruption',durable_sessions=len(sessions),actual_outages=outages,status=report['status']))
        print('PUBLISHED_APPHOST_STAGE='+json.dumps(launches[-1]),flush=True)
    summary=dict(source_revision=manifest['source_revision'],package_sha256=package_hash,apphost_sha256=digest(executable),rid=args.rid,platform=platform.platform(),verified_utc=datetime.now(timezone.utc).isoformat(),actual_published_apphost=True,
        launches=launches,captures_sha256={p.name:digest(p) for p in reports.glob('*.png')},full_manifest_verified=True,
        limits=['Programmatic real-control automation; no physical tray/menu clicks or OS-native save picker automation.','Loopback only. Controlled process termination is not a real network outage or a physical power-failure test.','Linux uses Xvfb in CI; Wayland/installed desktop and native accessibility remain separate.'])
    (output/'launch-summary.json').write_text(json.dumps(summary,indent=2)+'\n')
    print('PUBLISHED_APPHOST_PASS='+json.dumps(summary),flush=True)

if __name__=='__main__':main()
