"""Build licence inventory from locked runtime NuGet metadata and publisher-supplied texts; no network in packaging."""
import hashlib,json,shutil,xml.etree.ElementTree as ET
from pathlib import Path


def collect(source,dependencies,cache):
    retained=Path(__file__).resolve().parent/'licenses';destination=source/'licenses';destination.mkdir(exist_ok=True)
    retained_sources=json.loads((retained/'SOURCES.json').read_text())
    for name,record in retained_sources['files'].items():
        if hashlib.sha256((retained/name).read_bytes()).hexdigest()!=record['sha256']:raise ValueError('Retained licence source hash mismatch: '+name)
        shutil.copyfile(retained/name,destination/name)
    shutil.copyfile(retained/'SOURCES.json',destination/'SOURCES.json')
    inventory=[]
    for identity,details in sorted(dependencies['libraries'].items()):
        if details.get('type')!='package' or identity.startswith('runtimepack.'):continue
        package,version=identity.split('/');folder=cache/package.lower()/version.lower();spec=folder/(package.lower()+'.nuspec')
        if not spec.exists():raise ValueError('Restored package metadata missing: '+identity)
        tree=ET.parse(spec)
        def entry(name):return next((x for x in tree.iter() if x.tag.split('}')[-1]==name),None)
        licence=entry('license');copyright=entry('copyright');repository=entry('repository')
        if licence is None or not licence.text:raise ValueError('Review unlicensed package metadata: '+identity)
        copies=[];package_out=destination/'packages'/package/version;package_out.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(spec,package_out/spec.name)
        for item in folder.rglob('*'):
            if not item.is_file() or not any(s in item.name.lower() for s in ['license','licence','notice','copying','copyright']):continue
            if item.stat().st_size>3_000_000:raise ValueError('Unexpected large notice: '+identity)
            relative=item.relative_to(folder);out=package_out/relative;out.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(item,out)
            copies.append(out.relative_to(source).as_posix())
        applicable=[];lower=package.lower()
        if lower.startswith('avalonia') and lower!='avalonia.angle.windows.natives':applicable.append('Avalonia-MIT.txt')
        elif lower=='microcom.runtime':applicable.append('MicroCom-MIT.txt')
        elif lower.startswith('microsoft.data.sqlite'):applicable.append('Microsoft-Data-Sqlite-MIT.txt')
        elif lower.startswith('sqlitepclraw.'):
            applicable.append('Apache-2.0.txt')
            if lower=='sqlitepclraw.lib.e_sqlite3':applicable.append('SQLite-public-domain.txt')
        elif lower=='tmds.dbus.protocol':applicable.append('Tmds-DBus-MIT.txt')
        if lower=='avalonia.fonts.inter':applicable.append('Inter-OFL.txt')
        if licence.attrib.get('type')=='file' and not (folder/licence.text).is_file():raise ValueError('Declared package licence file missing: '+identity)
        if not copies and not applicable:raise ValueError('No reviewed licence text for runtime dependency: '+identity)
        inventory.append(dict(package=package,version=version,declared_license=licence.text,license_type=licence.attrib.get('type'),
            copyright=copyright.text if copyright is not None else None,repository=repository.attrib if repository is not None else {},
            retained_notices=copies+['licenses/'+name for name in applicable]))
    (source/'DEPENDENCY_NOTICES.json').write_text(json.dumps({'runtime_packages':inventory,'runtime_pack_notices':['licenses/dotnet/LICENSE.TXT','licenses/dotnet/THIRD-PARTY-NOTICES.TXT'],
        'font':'Inter3.019: embedded name-table copyright2020 The Inter Project Authors; full SIL OFL1.1 retained. See licenses/SOURCES.json.',
        'note':'Metadata is from the locked restored packages. Native publisher notices and upstream copyright/licence texts are retained without replacing the original Synups/WirePeep MIT.'},indent=2)+'\n')
    lines=['# Bundled dependency notices','', 'Synups derives from WirePeep by Bill Menees; see the unchanged LICENSE.','',
        'The .NET runtime notices are in licenses/dotnet. Native Skia/HarfBuzz/ANGLE notices are retained per package. Inter font software uses SIL OFL1.1; its Avalonia wrapper declares MIT. SQLitePCLRaw wrappers declare Apache2.0; native SQLite is public domain.','',
        '| Package | Version | Declared licence | Copyright |','|---|---|---|---|']
    for p in inventory:lines.append('| '+ ' | '.join(str(p[k] or '').replace('|','/') for k in ['package','version','declared_license','copyright'])+' |')
    lines+=['','Exact publisher metadata and file paths: DEPENDENCY_NOTICES.json. Retained upstream source URLs/hashes and font metadata: licenses/SOURCES.json.']
    (source/'THIRD_PARTY_NOTICES.md').write_text('\n'.join(lines)+'\n')
    return len(inventory)
