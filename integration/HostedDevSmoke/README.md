# Retained hosted-dev smoke preflight

This is a separate, opt-in preflight for the exact Supabase development project
`anvbhjteuxaufnqkbhju`. It uses the SDK's `BackendHttpConfiguration` to check
the configured authority and namespace. It performs **no network request or
hosted write**. Ordinary SDK tests do not invoke it.

Use the pinned .NET 9 SDK. Run `dotnet run --project
integration/HostedDevSmoke/HostedDevSmoke.csproj -- --self-test` for negative
target-guard tests. Create an untracked, non-secret target JSON outside this
repository with these fields: `ProjectRef`, `Endpoint`, `Issuer`, `Audience`,
`BackendNamespace`, `AppIdText`, `FixtureOwnership`, `DeployedSourceSha`,
`SchemaRegistrySha256`. The endpoint must be exactly
`https://anvbhjteuxaufnqkbhju.supabase.co/functions/v1/game-platform`, issuer
exactly `https://anvbhjteuxaufnqkbhju.supabase.co/auth/v1`, audience
`authenticated`, and fixture ownership `run-owned-disposable`. The namespace,
app ID and schema inventory digest must be checked against the retained
deployment by a trusted read-only source; the deployment record does not yet
publish their exact values. `plan <target.json>` prints a redacted manifest.

`preflight <target.json>` also requires `GP_HOSTED_DEV_ACCESS_TOKEN` in the
process environment. It checks the JWT's local shape, ES256 header, exact
issuer/audience/role, canonical subject and expiry, then prints only the
subject ID. This is **not signature verification**; only the hosted gateway can
authenticate it. Never copy a token into JSON, a command argument or a log.

An execution and separately resumable exact-ID cleanup path are not yet
implemented. Therefore this program deliberately has no `execute` command.
Provisioning or bootstrap against the retained project must wait for an
approved run-owned app/Auth fixture, proven cleanup, independent safety review
and a checked deployed identity. A local preflight pass is not hosted SDK
integration evidence.
