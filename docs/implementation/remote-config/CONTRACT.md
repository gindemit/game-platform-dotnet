# Remote configuration contract mirror

Authority: backend `contracts/remote-config/README.md` and its v1 JSON files.
The exact JSON copies here have SHA256 pins in `contract-pins.json`; standalone
SDK tests verify pins without requiring any sibling checkout. These mirror tests
must run alongside backend contract tests before a coordinated contract update.

Canonical values/limits, strict SemVer, first-match AND conditions, verified/fresh
source checks, stable `sha1-first7-mod100-v1` allocation, selection reason codes and
pure activation policy are defined by the owning contract. Unknown conditions
never match inequality. ConfigCat wire parsing and extended predicates are excluded.
The shared corpus ports the 22 recorded research vectors, including unknown input
projection for an unsafe number. No vendor source is copied or adapted.

The optional `GamePlatform.Configuration` project is independently usable on
WebGL and Android and has no runtime package/project references. Regular SDK UPM
delivery remains mobile-only; a separate config-only stage is used for measurements.
Evaluation is local selection; immutable refs and digests do not grant authority.
