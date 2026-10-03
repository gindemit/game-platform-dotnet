using System.Text;
using System.Text.Json;
using GamePlatform.Core;
using GamePlatform.Transport.Http;

internal static class Program
{
    private const string Project = "anvbhjteuxaufnqkbhju";
    private const string Origin = "https://anvbhjteuxaufnqkbhju.supabase.co";
    private const string Endpoint = Origin + "/functions/v1/game-platform";
    private const string Issuer = Origin + "/auth/v1";

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--self-test") { SelfTest(); Console.WriteLine("preflight self-test passed"); return 0; }
            if (args.Length != 2 || (args[0] != "plan" && args[0] != "preflight"))
                throw new InvalidOperationException("Usage: HostedDevSmoke plan|preflight <non-secret target.json> (no hosted writes)");
            var target = JsonSerializer.Deserialize<Target>(File.ReadAllText(args[1])) ?? throw new InvalidOperationException("Invalid target document");
            ValidateTarget(target);
            var subject = args[0] == "preflight" ? ValidateToken(Environment.GetEnvironmentVariable("GP_HOSTED_DEV_ACCESS_TOKEN")) : null;
            var manifest = new Manifest(Project, Endpoint, Issuer, target.BackendNamespace!, target.AppId!.Value,
                target.DeployedSourceSha!, target.SchemaRegistrySha256!, subject, "NOT_RUN", "No hosted writes or cleanup occurred");
            Console.WriteLine(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Hosted-dev preflight refused: " + error.Message);
            return 2;
        }
    }

    private static void ValidateTarget(Target target)
    {
        Require(target.ProjectRef == Project, "wrong project reference");
        Require(target.Endpoint == Endpoint, "wrong function endpoint");
        Require(target.Issuer == Issuer, "wrong issuer");
        Require(target.Audience == "authenticated", "wrong JWT audience");
        Require(target.BackendNamespace is { Length: > 0 and <= 128 } && target.BackendNamespace.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'), "invalid namespace");
        Require(target.AppId is { } app && app != Guid.Empty && app.ToString("D") == target.AppIdText, "invalid canonical app ID");
        Require(target.FixtureOwnership == "run-owned-disposable", "fixture ownership has not been established");
        Require(target.DeployedSourceSha is { Length: 40 } && target.DeployedSourceSha.All(Uri.IsHexDigit), "missing deployed source identity");
        Require(target.SchemaRegistrySha256 is { Length: 64 } && target.SchemaRegistrySha256.All(Uri.IsHexDigit), "missing schema inventory digest");
        _ = new BackendHttpConfiguration(new Uri(target.Endpoint!), new BackendNamespace(target.BackendNamespace!));
    }

    // A local claim check is only a preflight. The hosted gateway must verify the signature.
    private static string ValidateToken(string? token)
    {
        Require(!string.IsNullOrWhiteSpace(token), "access token missing");
        var parts = token!.Split('.');
        Require(parts.Length == 3 && parts.All(p => p.Length is > 0 and < 8192), "invalid JWT shape");
        using var header = JsonDocument.Parse(Decode(parts[0]));
        Require(header.RootElement.TryGetProperty("alg", out var algorithm) && algorithm.GetString() == "ES256", "unexpected JWT algorithm");
        using var payload = JsonDocument.Parse(Decode(parts[1]));
        var claims = payload.RootElement;
        Require(Claim(claims, "iss") == Issuer, "wrong token issuer");
        Require(Claim(claims, "aud") == "authenticated", "wrong token audience");
        Require(Claim(claims, "role") == "authenticated", "wrong token role");
        var sub = Claim(claims, "sub");
        Require(Guid.TryParseExact(sub, "D", out var subject) && subject != Guid.Empty && subject.ToString("D") == sub, "invalid token subject");
        Require(claims.TryGetProperty("exp", out var expiration) && expiration.TryGetInt64(out var exp) && exp > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 120, "token expired or near expiry");
        return sub!;
    }

    private static string? Claim(JsonElement claims, string name) => claims.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '=');
        return Convert.FromBase64String(padded);
    }
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }

    private static void SelfTest()
    {
        var good = new Target(Project, Endpoint, Issuer, "authenticated", "dev_namespace", "018f1700-0000-7000-8000-000000000001", "run-owned-disposable", new string('a', 40), new string('b', 64));
        ValidateTarget(good);
        foreach (var bad in new[] { good with { ProjectRef = "other" }, good with { Endpoint = Endpoint + "/other" }, good with { Issuer = "https://other/auth/v1" }, good with { AppIdText = Guid.Empty.ToString("D") }, good with { FixtureOwnership = "existing" }, good with { SchemaRegistrySha256 = "" } })
        {
            var refused = false;
            try { ValidateTarget(bad); } catch (InvalidOperationException) { refused = true; }
            Require(refused, "negative target guard failed");
        }
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"iss\":\"https://other/auth/v1\",\"aud\":\"authenticated\",\"role\":\"authenticated\",\"sub\":\"018f1700-0000-7000-8000-000000000001\",\"exp\":9999999999}"));
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"ES256\"}"));
        var rejected = false;
        try { ValidateToken(header + "." + payload + ".sig"); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "wrong issuer token guard failed");
    }

    private sealed record Target(string? ProjectRef, string? Endpoint, string? Issuer, string? Audience, string? BackendNamespace, string? AppIdText, string? FixtureOwnership, string? DeployedSourceSha, string? SchemaRegistrySha256)
    {
        public Guid? AppId => Guid.TryParse(AppIdText, out var value) ? value : null;
    }
    private sealed record Manifest(string ProjectRef, string Endpoint, string Issuer, string BackendNamespace, Guid AppId, string DeployedSourceSha, string SchemaRegistrySha256, string? SubjectId, string Status, string CleanupStatus);
}
