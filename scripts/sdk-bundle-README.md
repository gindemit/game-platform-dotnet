# Managed SDK bundle

This unpublished bundle contains the exact managed assemblies and portable
symbols listed in dependency-manifest.json, actual assembly references,
target-framework metadata, public type inventory, hashes, licenses and source
commit. A sourceDirty=true or buildSkipped=true bundle is development evidence
and must not be used for pinned import. Native SQLite, production codec,
live service and AOT/device capabilities remain absent/unverified.

The MessagePack qualification assembly is explicitly excluded because its full
transitive dependency/license delivery belongs to CL-015. The 14 included
assemblies have no external runtime dependencies beyond netstandard. An included
assembly referencing an excluded or missing assembly fails packaging.

Build from a clean committed checkout using `rtk proxy python scripts/package-sdk.py`.
Verify with `rtk proxy python scripts/package-sdk.py --verify`. Output is
artifacts/sdk; the previous verified output remains at artifacts/sdk-previous.
Other artifacts/evidence directories are preserved. Run clean builds twice and
compare both files inventories to qualify reproducibility. Desktop byte equality
does not certify device compatibility.

This managed bundle is not a NuGet feed; nothing is published. Import only
assemblies required by each consumer's explicit precompiled-reference list and
their full assemblies[].references dependency closure; do not automatically
reference every DLL from every Unity asmdef. Install each assembly once. Use core
contracts as sole canonical owner and remove duplicated declarations in the same
coordinated consumer change. Packaging changes no game code or Unity settings.

Keep the prior manifest/imported files before adoption. Roll back callers and
managed files together to that exact version; do not rewrite identities, clear
pending commands or delete saves. Actual Unity import/compilation/runtime
verification belongs to INT-002. Native/AOT bundling and acceptance belong to
CL-015/INT-009. A qualification MessagePack harness is not production capability.
