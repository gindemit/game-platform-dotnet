# P2 MessagePack qualification dependency

The already selected MessagePack-CSharp adapter now pins 3.1.8. The upstream
[release](https://github.com/MessagePack-CSharp/MessagePack-CSharp/releases/tag/v3.1.8)
and [NuGet metadata](https://www.nuget.org/packages/MessagePack/3.1.8) were reviewed
on 2026-09-19. It provides a netstandard2.1 asset under the MIT license. This
avoids the known vulnerabilities in older 3.1.4 releases. Exact transitive
versions and content hashes are recorded in generated NuGet lockfiles.

Use only explicit bounded reader/writer paths. No typeless resolver, reflection
serializer, runtime code generation, compression or serializer annotations in
portable contracts are authorized. `MessagePackWireCodec` is the production
`IWireCodec` adapter over the same generated mapping and bounded reader/writer
that passed the approved G2 corpus on Android ARM64 IL2CPP with high stripping.
It does not provide HTTP execution or host registration. Complete transitive
bundle/license delivery remains CL-015 evidence. No package is published or
automatically installed in Unity.

Diagnostic JSON parsing for the peer CLI uses the .NET 9 test host's BCL. It
does not select a JSON library for the portable JSON adapter.
