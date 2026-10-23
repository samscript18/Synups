"""Local evidence orchestration only: observe actual packaged processes, never inject measurements."""
import argparse,ctypes,json,platform,subprocess,time,importlib.util
from pathlib import Path
from datetime import datetime,timezone
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--package',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);parser.add_argument('--expected-revision',required=True);parser.add_argument('--seconds',type=int,default=300)
args=parser.parse_args()
if platform.system()!='Darwin' or platform.machine()!='arm64':parser.error('This observer measures macOS arm64 only')
if not 10<=args.seconds<=300:parser.error('seconds must be 10–300 per process')
repo=Path(__file__).resolve().parent.parent;root=args.output.resolve();root.mkdir(exist_ok=False)
archive=args.package.resolve()
spec=importlib.util.spec_from_file_location('smoke',repo/'eng/published-smoke.py');smoke=importlib.util.module_from_spec(spec);spec.loader.exec_module(smoke)
if archive.with_name(archive.name+'.sha256').read_text().split()[0]!=smoke.digest(archive):raise ValueError('Package checksum mismatch')
smoke.unpack(archive,root/'unpacked')
package=root/'unpacked/Synups-osx-arm64';manifest=json.loads((package/'ARTIFACT_MANIFEST.json').read_text());exe=package/manifest['apphost_relative_path']
assert manifest['source_revision']==args.expected_revision
for name,digest in manifest['files_sha256'].items():assert smoke.digest(package/name)==digest
lib=ctypes.CDLL('/usr/lib/libproc.dylib');lib.proc_pidinfo.argtypes=[ctypes.c_int,ctypes.c_int,ctypes.c_uint64,ctypes.c_void_p,ctypes.c_int];lib.proc_pidinfo.restype=ctypes.c_int

def descriptors(pid):
 size=lib.proc_pidinfo(pid,1,0,None,0)
 if size<=0:return None
 buffer=ctypes.create_string_buffer(size);actual=lib.proc_pidinfo(pid,1,0,buffer,size);return actual//8 if actual>0 else None

def cpu_seconds(value):
 pieces=[float(v) for v in value.split(':')];total=0
 for v in pieces:total=total*60+v
 return total
reports=root/'reports';reports.mkdir();data=root/'isolated-data';summaries=[]
for stage in ['exercise','restart']:
 started=datetime.now(timezone.utc).isoformat();clock=time.monotonic();samples=[]
 with (reports/(stage+'-console.txt')).open('w') as log:
  process=subprocess.Popen([str(exe),'--data-directory',str(data),'--verify-installation',str(reports),'--verification-stage',stage,'--observe-seconds',str(args.seconds)],cwd=exe.parent,stdout=log,stderr=subprocess.STDOUT)
  try:
   with (root/(stage+'-resources.jsonl')).open('w') as stream:
    while process.poll() is None:
     if time.monotonic()-clock>args.seconds+120:process.kill();process.wait();raise TimeoutError('Owned GUI observation exceeded bound')
     raw=subprocess.run(['/bin/ps','-p',str(process.pid),'-o','rss=,time='],capture_output=True,text=True,check=False).stdout.split()
     if len(raw)==2:
      record=dict(utc=datetime.now(timezone.utc).isoformat(),pid=process.pid,elapsed_seconds=time.monotonic()-clock,rss_bytes=int(raw[0])*1024,cpu_seconds=cpu_seconds(raw[1]),descriptors=descriptors(process.pid));samples.append(record);stream.write(json.dumps(record)+'\n');stream.flush()
      if len(samples)%12==0:print('GUI_OBSERVATION_SAMPLE='+json.dumps(record),flush=True)
     time.sleep(5)
   code=process.wait()
  except BaseException:
   if process.poll() is None:process.kill();process.wait(timeout=10)
   raise
 report=json.loads((reports/(stage+'.json')).read_text());assert code==0 and report['status']=='passed';assert Path(report['executable']).resolve()==exe.resolve()
 sampled=samples[2:] or samples
 summary=dict(stage=stage,pid=process.pid,started_utc=started,finished_utc=datetime.now(timezone.utc).isoformat(),exit_code=code,observed_seconds=args.seconds,samples=len(samples),rss_first=samples[0]['rss_bytes'],rss_after_warmup=sampled[0]['rss_bytes'],rss_last=samples[-1]['rss_bytes'],rss_peak=max(s['rss_bytes'] for s in samples),cpu_seconds=samples[-1]['cpu_seconds'],cpu_average_percent=100*samples[-1]['cpu_seconds']/samples[-1]['elapsed_seconds'],descriptors_first=samples[0]['descriptors'],descriptors_after_warmup=sampled[0]['descriptors'],descriptors_last=samples[-1]['descriptors'],descriptors_peak=max(s['descriptors'] or 0 for s in samples),database_bytes=(data/'history.v1.db').stat().st_size)
 summaries.append(summary);print('GUI_OBSERVATION_STAGE_PASS='+json.dumps(summary),flush=True)
sessions,events,outages=smoke.history(data);assert len(sessions)==2 and all(s['EndedUtc'] is not None and not s['Interrupted'] for s in sessions)
summary=dict(source_revision=manifest['source_revision'],package_sha256=smoke.digest(archive),apphost_sha256=smoke.digest(exe),stages=summaries,durable_sessions=len(sessions),actual_outages=outages,limits='Two bounded healthy-loopback GUI observations of primary packaged processes on macOS; not multi-hour GUI endurance, no leak proof or outage experiment. ps samples include startup/shutdown; snapshots every 5 seconds.')
(root/'summary.json').write_text(json.dumps(summary,indent=2)+'\n');print('GUI_OBSERVATION_PASS='+json.dumps(summary),flush=True)
