# CL-005 semantic HTTP evidence

Date: 2026-09-19

Provisioning source commit: `5b7308d5537462623ef7cf6b3b53bcd1312439bf`

Private-sync provider source commit: `ebef5ae2e5efc63b897f12b1f5d53e138dfb90de`

The qualified provisioning slice now maps a semantic Core-only port to the
frozen `POST /v1/apps/{appId}/provision` wire operation through injected neutral
HTTP, codec, auth-session and backend-configuration boundaries. It requires
HTTPS and the v1 MessagePack media type, bounds and defensively copies bodies,
validates response app/stream/version semantics, distinguishes pre-send failure
from uncertain delivery, and coordinates one refresh per rejected auth-session
generation. An ambiguous mutation is never automatically replayed or retried as
JSON.

Twelve focused cases cover request construction/redaction boundaries, timeout
before and after send, a concurrent 401 refresh race, 403, 409, 429 Retry-After,
500/503, wrong media/encoding, empty/truncated/oversize payloads, codec failure,
wrong-owner response, caller cancellation and authority/path confusion.

## Commands and raw results

```text
rtk dotnet restore GamePlatform.sln --locked-mode
exit 0
ok dotnet restore: 18 projects, 0 errors, 0 warnings (unknown)

rtk dotnet build GamePlatform.sln -c Release --no-restore
exit 0
ok dotnet build: 18 projects, 0 errors, 0 warnings (00:00:01.51)

rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~Cl005ProvisioningHttpProviderTests
exit 0
ok dotnet test: 12 tests passed, 0 warnings in 1 projects (587 ms)

rtk dotnet test GamePlatform.sln -c Release --no-build --no-restore
exit 0
ok dotnet test: 264 tests passed, 0 warnings in 1 projects (3.4 s)

rtk python scripts/validate.py
exit 0
Validation passed: 15 projects, 16 evidence-aware feature states, direct/transitive references, runtime packages, module READMEs, and reviewed contract pins.

rtk python -m unittest discover -s scripts -p "test_*.py"
exit 0
Ran 33 tests in 6.714s
OK

rtk python docs/implementation/validate_manifest.py
exit 0
VALID: 52 tasks; 16 feature ledgers; 21 conservative waves

(from docs/implementation) rtk python -m unittest test_manifest
exit 0
Ran 14 tests in 0.036s
OK

rtk git diff --check
exit 0
```

## Remaining limits

Provisioning and portable bootstrap/pull providers are implemented.
Account/push/profile/receipt/recovery providers remain unavailable; there is no portable production codec registration,
real host executor, BE-016 deployable backend composition, UnityWebRequest adapter,
live TLS/provider sandbox, or device evidence. BE-008 cannot yet truthfully return
the full durable provisioning response. CL-005 remains `in_progress`.

## 2026-09-20 portable bootstrap/pull provider increment

At coordinated SDK head `568d945c5597f085484465ac4372e4d2e2a3b9b3`, the
requested bootstrap/pull composition was inspected against backend
`e1e687605e9f5d2fc51aa6be15d804c0f1248e96`. The backend freezes authenticated
`GET /v1/apps/{appId}/bootstrap`, `POST /v1/apps/{appId}/bootstrap/pages`, and
`POST /v1/apps/{appId}/sync/pull`, preferring the v1 MessagePack media type and
permitting only explicitly negotiated diagnostic JSON.

The semantic provider was independently actionable and is now implemented over
the injected `IWireCodec`, `IHttpExecutor` and auth-session ports. It covers the
three frozen routes, explicit representation selection, request/response bounds,
canonical opaque-token conversion, one coordinated 401 refresh, safe read
cancellation/transport classification, protocol and pre-envelope errors,
complete projection groups, final-page cursor handoff and reset envelopes. It
never retries a failed decode as JSON.

Twenty-one new scripted boundary cases bring the combined CL-005/CL-010 focused
selection to 53/53 and the full suite to 317/317. These cases prove portable
orchestration only: their executor and codec are scripted, not live or production.
`TypedQualificationCodec` remains qualification-only,
`UnavailableMessagePackCodec` remains production-facing, and no concrete host
executor/composition exists. No qualification type was relabeled.
