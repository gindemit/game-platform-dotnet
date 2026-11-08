"""Pure output-policy tests: no exporter or UPM generation is executed."""
import importlib.util
import os
import tempfile
import unittest
from pathlib import Path
from unittest import mock


SCRIPT = Path(__file__).with_name('build-upm-package.py')
SPEC = importlib.util.spec_from_file_location('upm_validation_policy', SCRIPT)
UPM = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(UPM)


class ValidationOutputPolicyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='upm-output-policy-non-package-')
        self.addCleanup(self.temp.cleanup)
        self.parent = Path(self.temp.name).resolve()
        self.output = self.parent / 'new-evidence'

    def test_new_external_output_is_accepted_without_creating_it(self):
        self.assertEqual(self.output, UPM.validation_destination(self.output))
        self.assertEqual([], list(self.parent.iterdir()))

    def test_relative_and_repository_or_ancestor_outputs_are_rejected(self):
        with self.assertRaisesRegex(ValueError, 'must_be_absolute'):
            UPM.validation_destination(Path('relative-evidence'))
        for path in (UPM.ROOT, UPM.ROOT / 'artifacts/new-validation', UPM.ROOT.parent):
            with self.subTest(path=path), self.assertRaisesRegex(ValueError, 'must_be_external'):
                UPM.validation_destination(path)

    def test_nonexistent_or_file_parent_is_rejected_without_creation(self):
        for parent in (self.parent / 'missing', self.parent / 'file'):
            if parent.name == 'file':
                parent.write_text('preserve')
            with self.subTest(parent=parent), self.assertRaisesRegex(ValueError, 'parent_missing'):
                UPM.validation_destination(parent / 'output')
        self.assertEqual('preserve', (self.parent / 'file').read_text())
        self.assertFalse((self.parent / 'missing').exists())

    def test_existing_file_or_directory_is_rejected_and_retained(self):
        self.output.write_text('existing evidence')
        with self.assertRaisesRegex(ValueError, 'already_exists'):
            UPM.validation_destination(self.output)
        self.assertEqual('existing evidence', self.output.read_text())
        self.output.unlink()
        self.output.mkdir()
        with self.assertRaisesRegex(ValueError, 'already_exists'):
            UPM.validation_destination(self.output)
        self.assertTrue(self.output.is_dir())

    def test_previous_collision_is_never_removed(self):
        previous = self.output.with_name(self.output.name + '.previous')
        previous.write_text('unrelated prior evidence')
        with self.assertRaisesRegex(ValueError, 'previous_collision'):
            UPM.validation_destination(self.output)
        self.assertEqual('unrelated prior evidence', previous.read_text())

    def test_non_upm_stage_installs_only_into_new_owned_directory(self):
        stage = self.parent / 'arbitrary-stage'
        stage.mkdir()
        (stage / 'opaque.bin').write_bytes(b'non-package fixture')
        UPM.install_validation_stage(stage, self.output)
        self.assertEqual(b'non-package fixture', (self.output / 'opaque.bin').read_bytes())
        self.assertEqual([], list(stage.iterdir()))
        self.assertFalse(self.output.with_name(self.output.name + '.previous').exists())

    def test_raced_destination_is_preserved_and_stage_is_untouched(self):
        stage = self.parent / 'arbitrary-stage'
        stage.mkdir()
        (stage / 'source.txt').write_text('stage evidence')
        validate = UPM.validation_destination

        def collide(path):
            result = validate(path)
            # Simulate a different writer immediately after path-policy checks.
            result.mkdir()
            (result / 'foreign.txt').write_text('foreign evidence')
            return result

        with mock.patch.object(UPM, 'validation_destination', side_effect=collide):
            with self.assertRaises(FileExistsError):
                UPM.install_validation_stage(stage, self.output)
        self.assertEqual('foreign evidence', (self.output / 'foreign.txt').read_text())
        self.assertEqual('stage evidence', (stage / 'source.txt').read_text())

    def make_link(self, link, target):
        if os.name == 'nt':
            import _winapi
            _winapi.CreateJunction(str(target), str(link))
            self.addCleanup(lambda: link.rmdir() if os.path.lexists(link) else None)
        else:
            link.symlink_to(target, target_is_directory=True)
            self.addCleanup(lambda: link.unlink() if os.path.lexists(link) else None)

    def test_linked_parent_is_rejected_before_resolve(self):
        target = self.parent / 'target'
        target.mkdir()
        linked = self.parent / 'linked'
        self.make_link(linked, target)
        with self.assertRaisesRegex(ValueError, 'linked_path'):
            UPM.validation_destination(linked / 'new')
        self.assertEqual([], list(target.iterdir()))

    def test_dangling_previous_link_is_rejected_and_retained(self):
        target = self.parent / 'removed-target'
        target.mkdir()
        previous = self.output.with_name(self.output.name + '.previous')
        self.make_link(previous, target)
        target.rmdir()
        with self.assertRaisesRegex(ValueError, 'previous_collision'):
            UPM.validation_destination(self.output)
        self.assertTrue(os.path.lexists(previous))


if __name__ == '__main__':
    unittest.main()
