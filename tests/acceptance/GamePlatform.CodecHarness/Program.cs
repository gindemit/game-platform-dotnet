using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using GamePlatform.Serialization.MessagePack;

namespace GamePlatform.CodecHarness;

internal static class Program
{
    private const string Version = "0.2.0-core-schema.1";
    private static readonly QualificationMessagePackCodec Codec = new();
    private static readonly TypedQualificationCodec Dtos = new();

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] == "--help")
            {
                Console.WriteLine("Codec qualification only; never production or live-service acceptance.");
                Console.WriteLine("self-test --root <repo>");
                Console.WriteLine("produce --root <repo> --output <new.json> --sdk-commit <sha> --backend-commit <sha> --unity-commit <sha>");
                Console.WriteLine("consume --root <repo> --input <peer.json> --sdk-commit <sha> --backend-commit <sha> --unity-commit <sha>");
                Console.WriteLine("encode|decode --root <repo> --schema <reviewed-schema-ref> --input <file> --output <new-file>");
                return 0;
            }
            var options = Options(args);
            string root = Path.GetFullPath(Required(options, "root"));
            var cases = LoadCases(root);
            switch (args[0])
            {
                case "self-test": SelfTest(root, cases); break;
                case "produce": Produce(root, cases, options); break;
                case "consume": Consume(root, cases, options); break;
                case "encode": Transcode(options, true); break;
                case "decode": Transcode(options, false); break;
                default: throw new InvalidDataException("unsupported_mode_live_service_unavailable");
            }
            return 0;
        }
        catch (Exception ex)
        {
            // Do not echo exception messages: parser errors may contain private input.
            Console.Error.WriteLine("FAIL codec_harness " + ex.GetType().Name);
            return 1;
        }
    }

    private static Dictionary<string, string> Options(string[] args)
    {
        if (args.Length % 2 != 1) throw new ArgumentException("option_pair");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || !result.TryAdd(args[i][2..], args[i + 1]))
                throw new ArgumentException("option_duplicate");
        }
        string[] allowed = { "root", "output", "input", "schema", "sdk-commit", "backend-commit", "unity-commit" };
        if (result.Keys.Any(k => !allowed.Contains(k))) throw new ArgumentException("unknown_option");
        return result;
    }

    private static string Required(Dictionary<string, string> options, string key) =>
        options.TryGetValue(key, out string? value) ? value : throw new ArgumentException("missing_option");

    private static string Revision(Dictionary<string, string> options, string key)
    {
        string value = Required(options, key + "-commit");
        if (!Regex.IsMatch(value, "\\A[0-9a-f]{40}\\z")) throw new ArgumentException("immutable_revision_required");
        return value;
    }

    private static JsonElement[] LoadCases(string root)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "tests/acceptance/core-corpus.json")));
        if (manifest.RootElement.GetProperty("contractVersion").GetString() != Version) throw new InvalidDataException("version");
        var cases = manifest.RootElement.GetProperty("validations").EnumerateArray().Select(x => x.Clone()).ToArray();
        if (cases.Length == 0 || cases.Select(Id).Distinct(StringComparer.Ordinal).Count() != cases.Length)
            throw new InvalidDataException("empty_or_duplicate_corpus");
        return cases;
    }

    private static string Id(JsonElement item) => item.GetProperty("id").GetString()!;
    private static string Schema(JsonElement item) =>
        Path.GetFileName(item.GetProperty("schemaPath").GetString()) + item.GetProperty("schemaFragment").GetString();

    private static byte[] FixtureBytes(string root, JsonElement item)
    {
        string relative = item.GetProperty("instancePath").GetString()!;
        string path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("fixture_path_escape");
        byte[] bytes = ReadBounded(path, 262144);
        if (Hex(SHA256.HashData(bytes)) != item.GetProperty("instanceSha256").GetString())
            throw new InvalidDataException("fixture_hash");
        return bytes;
    }

    private static QualificationValue Fixture(string root, JsonElement item) => DiagnosticJson.Parse(FixtureBytes(root, item));

    private static void SelfTest(string root, JsonElement[] cases)
    {
        int passed = 0;
        foreach (var item in cases)
        {
            bool valid = item.GetProperty("expectedValid").GetBoolean();
            byte[] fixtureBytes = FixtureBytes(root, item); // Drift is never an expected invalid-schema result.
            bool accepted;
            try
            {
                var expected = DiagnosticJson.Parse(fixtureBytes);
                var typed = Dtos.NormalizeDto(Schema(item), expected);
                byte[] encoded = Codec.Encode(Schema(item), typed);
                var actual = Codec.Decode(Schema(item), encoded);
                actual = Dtos.NormalizeDto(Schema(item), actual);
                if (!DiagnosticJson.Write(actual).SequenceEqual(DiagnosticJson.Write(expected)))
                    throw new InvalidDataException("roundtrip_mismatch");
                accepted = true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidDataException || ex is FormatException || ex is OverflowException)
            { accepted = false; }
            if (accepted != valid)
            {
                Console.Error.WriteLine("schema_case_failed " + Id(item));
                throw new InvalidDataException("schema_case_failed");
            }
            passed++;
        }
        using var fingerprints = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "contracts/v1/fixtures/semantic/fingerprint-vectors.json")));
        int fingerprintCount = 0;
        foreach (var item in fingerprints.RootElement.GetProperty("vectors").EnumerateArray())
        {
            bool valid = item.GetProperty("valid").GetBoolean();
            bool accepted;
            try
            {
                var input = DiagnosticJson.Read(item.GetProperty("input"));
                string hash = Codec.Fingerprint(input);
                string canonical = Hex(Codec.CanonicalBytes(input));
                accepted = true;
                if (valid && (hash != item.GetProperty("sha256").GetString() || canonical != item.GetProperty("canonicalHex").GetString()))
                    throw new InvalidOperationException("fingerprint_known_answer_mismatch");
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidDataException || ex is FormatException || ex is OverflowException)
            { accepted = false; }
            if (accepted != valid) throw new InvalidDataException("fingerprint_case_failed");
            fingerprintCount++;
        }
        if (fingerprintCount == 0) throw new InvalidDataException("empty_fingerprint_corpus");
        foreach (string hostile in new[] { "{\"x\":1,\"x\":2}", "{\"x\":1.0}", "{\"x\":1e0}", "{\"x\":-0}", "{\"x\":\"\\ud800\"}" })
        {
            bool rejected = false;
            try { DiagnosticJson.Parse(System.Text.Encoding.UTF8.GetBytes(hostile)); }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidDataException || ex is InvalidOperationException)
            { rejected = true; }
            if (!rejected) throw new InvalidDataException("hostile_json_accepted");
        }
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            mode = "self-test",
            schemaCases = passed,
            fingerprintCases = fingerprintCount,
            hostileJsonCases = 5,
            peerInteroperability = "unverified",
            production = "unavailable"
        }));
    }

    private static void Produce(string root, JsonElement[] cases, Dictionary<string, string> options)
    {
        var commits = new { sdk = Revision(options, "sdk"), backend = Revision(options, "backend"), unity = Revision(options, "unity") };
        var produced = cases.Where(x => x.GetProperty("expectedValid").GetBoolean()).Select(item =>
        {
            var value = Fixture(root, item);
            return new { id = Id(item), schema = Schema(item), hex = Hex(Codec.Encode(Schema(item), Dtos.NormalizeDto(Schema(item), value))) };
        }).ToArray();
        if (produced.Length == 0) throw new InvalidDataException("zero_cases");
        using var kat = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "contracts/v1/fixtures/semantic/fingerprint-vectors.json")));
        var fingerprints = kat.RootElement.GetProperty("vectors").EnumerateArray().Where(x => x.GetProperty("valid").GetBoolean()).Select(item =>
            new { id = item.GetProperty("name").GetString(), sha256 = Codec.Fingerprint(DiagnosticJson.Read(item.GetProperty("input"))) }).ToArray();
        byte[] output = JsonSerializer.SerializeToUtf8Bytes(new
        {
            formatVersion = 1,
            contractVersion = Version,
            producer = "csharp",
            commits,
            cases = produced,
            fingerprints
        }, new JsonSerializerOptions { WriteIndented = true });
        string path = Path.GetFullPath(Required(options, "output"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        stream.Write(output);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            mode = "produce",
            cases = produced.Length,
            fingerprints = fingerprints.Length,
            sha256 = Hex(SHA256.HashData(output)),
            peerInteroperability = "unverified"
        }));
    }

    private static void Consume(string root, JsonElement[] cases, Dictionary<string, string> options)
    {
        string sdk = Revision(options, "sdk"), backend = Revision(options, "backend"), unity = Revision(options, "unity");
        using var input = JsonDocument.Parse(ReadBounded(Required(options, "input"), 32 * 1024 * 1024));
        var peer = input.RootElement;
        if (peer.GetProperty("formatVersion").GetInt32() != 1 || peer.GetProperty("contractVersion").GetString() != Version ||
            peer.GetProperty("producer").GetString() != "typescript") throw new InvalidDataException("real_typescript_corpus_required");
        var commits = peer.GetProperty("commits");
        if (commits.GetProperty("sdk").GetString() != sdk || commits.GetProperty("backend").GetString() != backend ||
            commits.GetProperty("unity").GetString() != unity) throw new InvalidDataException("revision_mismatch");
        var expectedCases = cases.Where(x => x.GetProperty("expectedValid").GetBoolean()).ToDictionary(Id, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in peer.GetProperty("cases").EnumerateArray())
        {
            string id = Id(item);
            if (!seen.Add(id) || !expectedCases.TryGetValue(id, out var expected)) throw new InvalidDataException("unexpected_or_duplicate_case");
            string schema = Schema(expected);
            if (item.GetProperty("schema").GetString() != schema) throw new InvalidDataException("schema_mismatch");
            string hex = item.GetProperty("hex").GetString()!;
            if (hex.Length > 524288) throw new InvalidDataException("encoded_limit");
            var decoded = Dtos.NormalizeDto(schema, Codec.Decode(schema, Convert.FromHexString(hex)));
            if (!DiagnosticJson.Write(decoded).SequenceEqual(DiagnosticJson.Write(Fixture(root, expected))))
                throw new InvalidDataException("peer_semantic_mismatch");
        }
        if (seen.Count == 0 || seen.Count != expectedCases.Count) throw new InvalidDataException("missing_cases");
        using var kat = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "contracts/v1/fixtures/semantic/fingerprint-vectors.json")));
        var expectedFingerprints = kat.RootElement.GetProperty("vectors").EnumerateArray().Where(x => x.GetProperty("valid").GetBoolean())
            .ToDictionary(x => x.GetProperty("name").GetString()!, x => x.GetProperty("sha256").GetString()!, StringComparer.Ordinal);
        var fingerprintSeen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in peer.GetProperty("fingerprints").EnumerateArray())
        {
            string id = Id(item);
            if (!fingerprintSeen.Add(id) || !expectedFingerprints.TryGetValue(id, out string? expected) ||
                item.GetProperty("sha256").GetString() != expected) throw new InvalidDataException("peer_fingerprint_mismatch");
        }
        if (fingerprintSeen.Count == 0 || fingerprintSeen.Count != expectedFingerprints.Count) throw new InvalidDataException("missing_fingerprints");
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            mode = "consume",
            direction = "typescript_to_csharp",
            cases = seen.Count,
            fingerprints = fingerprintSeen.Count,
            sdk,
            backend,
            unity,
            reverseDirection = "requires_backend_consumer",
            gateG2 = "not_approved_by_this_command"
        }));
    }

    private static void Transcode(Dictionary<string, string> options, bool encode)
    {
        string schema = Required(options, "schema");
        byte[] input = ReadBounded(Required(options, "input"), 262144);
        byte[] output = encode
            ? Codec.Encode(schema, DiagnosticJson.Parse(input))
            : DiagnosticJson.Write(Codec.Decode(schema, input));
        string path = Path.GetFullPath(Required(options, "output"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        stream.Write(output);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            mode = encode ? "encode" : "decode",
            bytes = output.Length,
            acceptance = "single_conversion_only_no_peer_or_gate_verdict"
        }));
    }

    private static byte[] ReadBounded(string path, long maximum)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > maximum) throw new InvalidDataException("input_limit");
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }
    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
