import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('configuration_client_stage',ROOT/'integration/RemoteConfig/stage-client.py');stage=importlib.util.module_from_spec(spec);spec.loader.exec_module(stage)
spec=importlib.util.spec_from_file_location('client_package',ROOT/'scripts/package-sdk.py');package=importlib.util.module_from_spec(spec);spec.loader.exec_module(package)

class ClientStagingTests(unittest.TestCase):
    def test_both_configuration_assemblies_are_excluded_from_regular_navigation_bundle(self):
        projects=package.regular_runtime_projects(ROOT);self.assertEqual(16,len(projects));self.assertFalse(stage.NAMES & {p.stem for p in projects})
        self.assertIn('GamePlatform.Navigation.Kernel', {p.stem for p in projects})
    def test_hash_bound_closure_and_owned_sources_stage_without_replacement(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);paths=[];proof={'assemblies':[],'assemblySha256':{}}
            for name in sorted(stage.NAMES):
                path=root/(name+'.dll');path.write_bytes(('owned staging fixture '+name).encode());paths.append(path);proof['assemblySha256'][name]=hashlib.sha256(path.read_bytes()).hexdigest();proof['assemblies'].append({'name':name,'targetFramework':'.NETStandard,Version=v2.1','references':[{'name':'netstandard'}]+([{'name':'GamePlatform.Configuration'}] if name.endswith('.Client') else [])})
            metadata=root/'metadata.json';metadata.write_text(json.dumps(proof),encoding='utf-8');output=root/'stage';inventory=stage.stage(paths,metadata,output)
            self.assertEqual(0,inventory['runtimeThirdPartyPackages']);self.assertEqual(2,len(list(output.rglob('*.dll'))));self.assertTrue((output/'Runtime/Plugins/WebGL/configuration-client.jslib').is_file());self.assertIn('validateReferences: 1',next(output.rglob('*.dll.meta')).read_text())
            host=output/'Runtime/Unity/GamePlatform.Configuration.Unity.asmdef';definition=json.loads(host.read_text(encoding='utf-8'))
            self.assertEqual('GamePlatform.Configuration.Unity',definition['name']);self.assertTrue(definition['overrideReferences'])
            self.assertEqual({'GamePlatform.Configuration.Client.dll','GamePlatform.Configuration.dll'},set(definition['precompiledReferences']))
            self.assertEqual([],definition['references']);self.assertEqual(['Editor','Android','WebGL'],definition['includePlatforms'])
            self.assertIn(stage.HOST_GUID,(host.parent/(host.name+'.meta')).read_text(encoding='utf-8'))
            self.assertEqual(hashlib.sha256(host.read_bytes()).hexdigest(),inventory['files'][host.relative_to(output).as_posix()])
            with self.assertRaisesRegex(ValueError,'output_must_be_empty'):stage.stage(paths,metadata,output)
            proof['assemblies'][1]['references'].append({'name':'GamePlatform.Storage.Sqlite'});metadata.write_text(json.dumps(proof),encoding='utf-8')
            with self.assertRaisesRegex(ValueError,'client_only_closure_required'):stage.stage(paths,metadata,root/'bad')
    def test_wrong_assembly_hash_is_rejected_before_output(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);paths=[];proof={'assemblies':[],'assemblySha256':{}}
            for name in sorted(stage.NAMES):
                path=root/(name+'.dll');path.write_bytes(b'fixture');paths.append(path);proof['assemblySha256'][name]='0'*64;proof['assemblies'].append({'name':name,'targetFramework':'.NETStandard,Version=v2.1','references':[{'name':'netstandard'}]+([{'name':'GamePlatform.Configuration'}] if name.endswith('.Client') else [])})
            metadata=root/'metadata.json';metadata.write_text(json.dumps(proof),encoding='utf-8')
            with self.assertRaisesRegex(ValueError,'metadata_assembly_hash_mismatch'):stage.stage(paths,metadata,root/'bad')
            self.assertFalse((root/'bad').exists())

if __name__=='__main__':unittest.main()
