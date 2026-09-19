[CmdletBinding()]
param(
    [string] $WorkRoot = (Join-Path ([System.IO.Path]::GetTempPath()) "game-platform-cl006-sqlite"),
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$candidateRevision = "08248bd5884d8eb932a837aa56d4ff456daf913f"
$sqliteNetRevision = "5f72241035dd48f4305a7c811eba8e7c955e9840"
$candidateRoot = Join-Path $WorkRoot "unity-sqlite-net"
$databasePath = Join-Path $WorkRoot "probe.sqlite3"
$projectPath = Join-Path $PSScriptRoot "DesktopProbe/DesktopProbe.csproj"

if (-not (Test-Path -LiteralPath $candidateRoot)) {
    git clone --filter=blob:none https://github.com/gilzoide/unity-sqlite-net.git $candidateRoot
}

git -C $candidateRoot fetch --tags origin
git -C $candidateRoot checkout --detach $candidateRevision
git -C $candidateRoot submodule update --init --recursive

$actualRevision = (git -C $candidateRoot rev-parse HEAD).Trim()
$actualSqliteNetRevision = (git -C $candidateRoot rev-parse HEAD:Plugins/sqlite-net~).Trim()
if ($actualRevision -ne $candidateRevision -or $actualSqliteNetRevision -ne $sqliteNetRevision) {
    throw "Candidate source pin mismatch."
}

$expectedHashes = @{
    "LICENSE.txt" = "F029C8AAAFE1CEB2DC57FAE54EE065F150132530209003DA4D6444318D335725"
    "Runtime/sqlite-net/LICENSE.txt" = "F4F848E81A747B85A1F5C7B761BF12B85098DD6A6A46ACEBD54958E2FE2DD62D"
    "Runtime/sqlite-net/SQLite.cs" = "73D35895222B80C54CD5E5531DB5DB1C0213E35DB09E18937054E0E11CE3A30F"
    "Plugins/lib/windows/x86_64/gilzoide-sqlite-net.dll" = "E7E370938925DFF66D9A4D50BFAC6D18CC9505BD2F3F127A232B410491B46E3E"
}
foreach ($relativePath in $expectedHashes.Keys) {
    $actualHash = (Get-FileHash -Algorithm SHA256 (Join-Path $candidateRoot $relativePath)).Hash
    if ($actualHash -ne $expectedHashes[$relativePath]) {
        throw "SHA-256 mismatch for $relativePath."
    }
}

dotnet run --project $projectPath --configuration $Configuration --property:SqliteCandidateRoot=$candidateRoot -- $databasePath
if ($LASTEXITCODE -ne 0) {
    throw "Desktop SQLite probe failed with exit code $LASTEXITCODE."
}
