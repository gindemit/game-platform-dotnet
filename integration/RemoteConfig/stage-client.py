"""Stage inspected optional core/client plus owned host sources; never build/import/deploy."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil

HERE=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('configuration_core_stage',HERE/'stage.py');core=importlib.util.module_from_spec(spec);spec.loader.exec_module(core)
NAMES={'GamePlatform.Configuration','GamePlatform.Configuration.Client'}
CLIENT_GUID='a2d3e48556fb4620bb9371b401dd520b'

def stage(assemblies,metadata,output):
    paths={Path(path).stem:Path(path).resolve() for path in assemblies};metadata=Path(metadata).resolve();output=Path(output).resolve()
    if set(paths)!=NAMES or any(path.name!=name+'.dll' or not path.is_file() for name,path in paths.items()):raise ValueError('client_closure_required')
    proof=json.loads(metadata.read_text(encoding='utf-8'));items=proof.get('assemblies');hashes=proof.get('assemblySha256')
    if not isinstance(items,list) or len(items)!=2 or {item.get('name') for item in items}!=NAMES or not isinstance(hashes,dict) or set(hashes)!=NAMES:raise ValueError('client_closure_required')
    for item in items:
        name=item['name'];expected={'netstandard'} if name=='GamePlatform.Configuration' else {'netstandard','GamePlatform.Configuration'}
        if item.get('targetFramework')!='.NETStandard,Version=v2.1' or {ref.get('name') for ref in item.get('references',[])}!=expected:raise ValueError('client_only_closure_required')
        if hashes[name]!=hashlib.sha256(paths[name].read_bytes()).hexdigest():raise ValueError('metadata_assembly_hash_mismatch')
    if output.exists() and any(output.iterdir()):raise ValueError('output_must_be_empty')
    plugins=output/'Runtime/Plugins';plugins.mkdir(parents=True,exist_ok=True)
    for name,path in paths.items():
        shutil.copy2(path,plugins/path.name);guid=core.GUID if name=='GamePlatform.Configuration' else CLIENT_GUID
        (plugins/(path.name+'.meta')).write_text(core.META.replace(core.GUID,guid),encoding='utf-8')
    sources={
      'UnityConfigurationHost.cs':'Runtime/Unity/UnityConfigurationHost.cs',
      'configuration-client.jslib':'Runtime/Plugins/WebGL/configuration-client.jslib',
      'ConfigurationHttp.java':'Runtime/Plugins/Android/com/gindemit/configuration/ConfigurationHttp.java',
      'proguard-rules.pro':'Runtime/Plugins/Android/proguard-rules.pro',
      'README.md':'README.md',
    }
    for name,relative in sources.items():
        source=HERE/'Client'/name
        if not source.is_file():raise ValueError('adapter_source_missing:'+name)
        target=output/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
    (output/'package.json').write_text(json.dumps({'name':'com.gindemit.game-platform.configuration.client','version':'0.1.0-local','displayName':'Game Platform Configuration Client (isolated)','unity':'6000.5','description':'Optional standalone lifecycle and host adapters; no account/mobile SDK dependency.'},indent=2)+'\n',encoding='utf-8')
    inventory={'schemaVersion':1,'runtimeThirdPartyPackages':0,'assemblies':{name:{'bytes':path.stat().st_size,'sha256':hashes[name]} for name,path in sorted(paths.items())},'metadataSha256':hashlib.sha256(metadata.read_bytes()).hexdigest(),'files':{path.relative_to(output).as_posix():hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(output.rglob('*')) if path.is_file()},'platforms':['Editor','Android','WebGL'],'runtimeQualification':'separate; Java/build is not physical JNI evidence'}
    (output/'inventory.json').write_text(json.dumps(inventory,indent=2)+'\n',encoding='utf-8');return inventory

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--assembly',action='append',required=True);parser.add_argument('--metadata',required=True);parser.add_argument('--output',required=True);args=parser.parse_args();print(json.dumps(stage(args.assembly,args.metadata,args.output),indent=2))

if __name__=='__main__':main()
