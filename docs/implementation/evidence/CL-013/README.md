# CL-013 managed bundle evidence

Artifact source: `5dfac45875fa4b829e9e04729938eb4a984e1db5`, branch
`impl/cl013-p2`. The later script-only path-safety commit `a07aa0af` did not
change the shipped artifact. No Unity files or runtime behavior were changed by
this evidence capture.

The original `artifacts/sdk` and `artifacts/sdk-previous` outputs each contain
14 DLLs, 14 portable PDBs, a license notice, architecture/features/contract pin
metadata, and bundle instructions: 33 hashed files plus their manifest. Actual
PE metadata establishes each assembly's netstandard2.1 target and complete
included dependency closure. MessagePack qualification is explicitly excluded;
its transitive license/bundle work remains CL-015. Production codec, native,
live-service, Unity import and AOT/device acceptance are not certified here.

Both original output manifests are archived as `build-one-manifest.json` and
`build-two-manifest.json`. Their bytes are identical, SHA-256
`acfbf1f679b79186cf074ca6e9dc9917835dad86ff7d54af134825eb4ee092be`.
Capture rechecked all 33 file hashes and sizes in both original output folders,
their exact source commit, clean-source flag and non-skipped Rebuild flag.
See `results.json` and the reproducible capture procedure `capture-evidence.py`.

Two original executions of `rtk proxy python scripts/package-sdk.py` from the
clean committed task worktree both succeeded. This is an author observation
supported by retained matching output artifacts: raw build stdout existed in
the agent tool transcript but was not archived on disk. The capture did not
rebuild, replace, or mutate the artifact used by the consumer. Do not describe
these two original runs as independently reviewed build logs.

Freshly captured commands, both exit 0:

- `rtk proxy python -m unittest discover -s scripts -p test_packaging.py`:
  11 tests passed; raw output in `packaging-tests.txt`.
- `rtk proxy python scripts/package-sdk.py --verify`: inventory, hashes and
  declared dependency closure verified; raw output in `bundle-verification.txt`.

The Core DLL is `91e91b4b722923af8f0392ce7083b6216f55ead4296710b720cad55e81b447be`;
Features.Contracts DLL is
`e388238ebc4c3f440f3acfc4f107a78305de1b63f19432428e509e203cbe43a4`.
The consumer owner received this exact pair for explicit minimal import.
