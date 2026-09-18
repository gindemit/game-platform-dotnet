import copy
import json
from pathlib import Path
import unittest
from validate_manifest import validate

BASE=json.loads(Path(__file__).with_name('execution-manifest.json').read_text())
class ManifestTests(unittest.TestCase):
    def changed(self, fn):
        value=copy.deepcopy(BASE); fn(value); return validate(value)
    def test_valid_plan(self): self.assertEqual([],validate(BASE))
    def test_duplicate_id(self): self.assertTrue(self.changed(lambda d:d['tasks'].append(copy.deepcopy(d['tasks'][0]))))
    def test_missing_dependency(self): self.assertTrue(self.changed(lambda d:d['tasks'][0]['dependencies'].append('CL-999')))
    def test_cycle(self):
        def change(d): d['tasks'][0]['dependencies']=['CL-002']
        self.assertTrue(any('cycle' in e for e in self.changed(change)))
    def test_same_wave_lock(self):
        def change(d): d['tasks'][5]['locks']=d['tasks'][0]['locks']
        self.assertTrue(any('collision' in e for e in self.changed(change)))
    def test_same_wave_path(self):
        def change(d): d['tasks'][5]['owned_paths']=d['tasks'][0]['owned_paths']
        self.assertTrue(any('collision' in e for e in self.changed(change)))
    def test_dependency_wave(self):
        def change(d): d['tasks'][1]['wave']=0
        self.assertTrue(any('earlier wave' in e for e in self.changed(change)))
    def test_missing_field(self): self.assertTrue(self.changed(lambda d:d['tasks'][0].pop('owner_repo')))
    def test_unsafe_path(self):
        def change(d): d['tasks'][0]['owned_paths']=['../secret']
        self.assertTrue(self.changed(change))
    def test_unknown_peer(self): self.assertTrue(self.changed(lambda d:d['tasks'][0]['peer_dependencies'].append('BE-FAKE')))
    def test_false_task_completion(self):
        def change(d): d['tasks'][0]['status']='complete'
        self.assertTrue(self.changed(change))
    def test_false_integration(self):
        def change(d): d['feature_ledger'][0]['mrsquare_integration_status']='integrated'
        self.assertTrue(self.changed(change))
    def test_missing_feature(self): self.assertTrue(self.changed(lambda d:d['feature_ledger'].pop()))
    def test_wrong_baseline(self):
        def change(d): d['tasks'][0]['baseline_commit']='0'*40
        self.assertTrue(self.changed(change))
if __name__=='__main__': unittest.main()
