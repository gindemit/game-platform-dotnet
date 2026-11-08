"""Exercise actual repository analyzer policy through isolated compiler builds."""
import subprocess
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'tests/analyzer-fixtures/AnalyzerFixture.csproj'


class AnalyzerPolicyTests(unittest.TestCase):
    def build_fixture(self, symbol):
        # The project inherits the real props/editorconfig; only outputs and
        # fixture-only restore lock behavior are overridden, never CA severity.
        with tempfile.TemporaryDirectory(prefix='sdk-ca-fixture-') as temporary:
            output = Path(temporary).as_posix() + '/'
            command = ['dotnet', 'build', str(PROJECT), '-c', 'Release', '--nologo',
                       '-v:minimal', '-p:DefineConstants=' + symbol,
                       '-p:BaseIntermediateOutputPath=' + output + 'obj/',
                       '-p:MSBuildProjectExtensionsPath=' + output + 'obj/',
                       '-p:OutputPath=' + output + 'bin/',
                       '-p:RestorePackagesWithLockFile=false', '-p:RestoreLockedMode=false',
                       '-nodeReuse:false', '-p:UseSharedCompilation=false']
            return subprocess.run(command, cwd=ROOT, text=True, stdout=subprocess.PIPE,
                                  stderr=subprocess.STDOUT, timeout=120)

    def test_valid_netstandard_valuetask_usage_passes_actual_policy(self):
        result = self.build_fixture('GOOD')
        self.assertEqual(0, result.returncode, result.stdout)

    def test_rethrow_regression_fails_with_ca2200(self):
        result = self.build_fixture('BAD_RETHROW')
        self.assertNotEqual(0, result.returncode, result.stdout)
        self.assertIn('error CA2200:', result.stdout)

    def test_valuetask_regression_fails_with_ca2012(self):
        result = self.build_fixture('BAD_VALUETASK')
        self.assertNotEqual(0, result.returncode, result.stdout)
        self.assertIn('error CA2012:', result.stdout)


if __name__ == '__main__':
    unittest.main()
