import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('contract_sync', Path(__file__).with_name('sync-contracts.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ContractSyncTests(unittest.TestCase):
    def fixture(self, root):
        source, target = root / 'source', root / 'target'
        source.mkdir(); target.mkdir()
        (source / 'one.json').write_bytes(b'{}\n')
        snapshot = {'schemaVersion':1,'files':{'one.json':hashlib.sha256(b'{}\n').hexdigest()}}
        return source, target, snapshot

    def test_copies_complete_reviewed_set_exactly(self):
        with tempfile.TemporaryDirectory() as temp:
            source,target,snapshot=self.fixture(Path(temp))
            (source/'snapshot.json').write_text(json.dumps(snapshot))
            self.assertEqual(1,module.sync(source,target))
            self.assertEqual((source/'one.json').read_bytes(),(target/'one.json').read_bytes())
            self.assertEqual((source/'snapshot.json').read_bytes(),(target/'snapshot.json').read_bytes())

    def test_digest_failure_writes_nothing(self):
        with tempfile.TemporaryDirectory() as temp:
            source,target,snapshot=self.fixture(Path(temp))
            snapshot['files']['one.json']='0'*64
            (source/'snapshot.json').write_text(json.dumps(snapshot))
            with self.assertRaisesRegex(ValueError,'digest mismatch'): module.sync(source,target)
            self.assertEqual([],list(target.iterdir()))

    def test_path_escape_writes_nothing(self):
        with tempfile.TemporaryDirectory() as temp:
            source,target,snapshot=self.fixture(Path(temp))
            snapshot['files']['../escape.json']='0'*64
            (source/'snapshot.json').write_text(json.dumps(snapshot))
            with self.assertRaisesRegex(ValueError,'Unsafe'): module.sync(source,target)
            self.assertEqual([],list(target.iterdir()))


if __name__=='__main__': unittest.main()
