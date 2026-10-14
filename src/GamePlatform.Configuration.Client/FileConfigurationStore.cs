using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Configuration.Client
{
    public interface IFileConfigurationFaults
    {
        void At(string boundary);
    }
    /// <summary>Config-only per-namespace objects, cross-process CAS and atomic recoverable index.</summary>
    public sealed class FileConfigurationStore : IConfigurationStore
    {
        private readonly string root;
        private readonly IFileConfigurationFaults? faults;
        public FileConfigurationStore(string rootDirectory, IFileConfigurationFaults? faults = null)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("root_required", nameof(rootDirectory));
            root = Path.GetFullPath(rootDirectory);
            this.faults = faults;
        }
        private string DirectoryFor(CacheNamespace space)
        {
            string path = Path.GetFullPath(Path.Combine(root, space.Key));
            if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("namespace_escape");
            return path;
        }
        private static async Task<byte[]?> Read(string path, int limit, CancellationToken token)
        {
            if (!File.Exists(path))
                return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 8192, true);
            if (stream.Length < 1 || stream.Length > limit)
                throw new InvalidConfigurationException("stored_bounds");
            var bytes = new byte[checked((int)stream.Length)];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int count = await stream.ReadAsync(bytes, offset, bytes.Length - offset, token).ConfigureAwait(false);
                if (count == 0)
                    throw new IOException("partial_object");
                offset += count;
            }
            return bytes;
        }
        private async Task<StoreSnapshot?> Index(CacheNamespace space, string directory, CancellationToken token)
        {
            foreach (string name in new[] { "index.json", "index.backup.json" })
            {
                try
                {
                    byte[]? bytes = await Read(Path.Combine(directory, name), ConfigurationLimits.Bytes, token).ConfigureAwait(false);
                    if (bytes != null)
                    {
                        var state = ProtocolReader.Cache(bytes, space);
                        return new StoreSnapshot(state.Generation, bytes);
                    }
                }
                catch (InvalidConfigurationException) { }
                catch (IOException) { }
            }
            return null;
        }
        public Task<StoreSnapshot?> LoadAsync(CacheNamespace space, CancellationToken cancellationToken)
        {
            if (space == null)
                throw new ArgumentNullException(nameof(space));
            cancellationToken.ThrowIfCancellationRequested();
            return Index(space, DirectoryFor(space), cancellationToken);
        }
        public async Task<byte[]?> ReadObjectAsync(CacheNamespace space, string representationSha, int maximumDecodedBytes, CancellationToken cancellationToken)
        {
            if (space == null)
                throw new ArgumentNullException(nameof(space));
            Wire.Digest(representationSha);
            if (maximumDecodedBytes < 1 || maximumDecodedBytes > ConfigurationLimits.Bytes)
                throw new ArgumentOutOfRangeException(nameof(maximumDecodedBytes));
            byte[]? bytes = await Read(Path.Combine(DirectoryFor(space), "objects", representationSha + ".json"), maximumDecodedBytes, cancellationToken).ConfigureAwait(false);
            if (bytes != null && CanonicalJson.Sha256(bytes) != representationSha)
                throw new InvalidConfigurationException("stored_integrity");
            return bytes;
        }
        private static async Task Write(string path, byte[] bytes, CancellationToken token)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, true);
            await stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
            stream.Flush(true);
        }
        public async Task<StoreCommitOutcome> CommitAsync(CacheNamespace space, long expectedGeneration, byte[] metadata, IReadOnlyDictionary<string, byte[]> objects, CancellationToken cancellationToken)
        {
            if (space == null || metadata == null || objects == null)
                throw new ArgumentNullException(space == null ? nameof(space) : metadata == null ? nameof(metadata) : nameof(objects));
            var candidate = ProtocolReader.Cache(metadata, space);
            if (space.Owner != null && candidate.OwnerGeneration != space.Owner.Generation)
                throw new InvalidConfigurationException("owner_changed");
            if (candidate.Generation != checked(expectedGeneration + 1))
                throw new InvalidConfigurationException("store_generation");
            string directory = DirectoryFor(space), objectDirectory = Path.Combine(directory, "objects");
            string? stagedIndex = null;
            bool committed = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(objectDirectory);
                FileStream lease;
                try
                {
                    lease = new FileStream(Path.Combine(directory, "commit.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException error) when ((error.HResult & 65535) == 32 || (error.HResult & 65535) == 33) { return StoreCommitOutcome.Conflict; }
                using var heldLease = lease;
                var current = await Index(space, directory, cancellationToken).ConfigureAwait(false);
                if ((current?.Generation ?? 0) != expectedGeneration)
                    return StoreCommitOutcome.Conflict;
                if (current != null && space.Owner != null && ProtocolReader.Cache(current.Metadata, space).OwnerGeneration > candidate.OwnerGeneration)
                    throw new InvalidConfigurationException("owner_changed");
                faults?.At("before_objects");
                foreach (var pair in objects)
                {
                    Wire.Digest(pair.Key);
                    if (!candidate.Objects.TryGetValue(pair.Key, out var record) || pair.Value.Length != record.Bytes || CanonicalJson.Sha256(pair.Value) != pair.Key)
                        throw new InvalidConfigurationException("object_invalid");
                    string target = Path.Combine(objectDirectory, pair.Key + ".json");
                    if (File.Exists(target))
                    {
                        byte[]? existing = await Read(target, ConfigurationLimits.Bytes, cancellationToken).ConfigureAwait(false);
                        if (existing == null || CanonicalJson.Sha256(existing) != pair.Key)
                            throw new InvalidConfigurationException("immutable_replacement");
                    }
                    else
                    {
                        string staging = target + "." + Guid.NewGuid().ToString("N") + ".pending";
                        try
                        {
                            await Write(staging, pair.Value, cancellationToken).ConfigureAwait(false);
                            File.Move(staging, target);
                        }
                        finally { if (File.Exists(staging)) File.Delete(staging); }
                    }
                }
                foreach (var pair in candidate.Objects)
                {
                    var file = new FileInfo(Path.Combine(objectDirectory, pair.Key + ".json"));
                    if (!file.Exists || file.Length != pair.Value.Bytes)
                        throw new InvalidConfigurationException("object_missing");
                }
                faults?.At("after_objects");
                stagedIndex = Path.Combine(directory, "index." + Guid.NewGuid().ToString("N") + ".pending");
                await Write(stagedIndex, metadata, cancellationToken).ConfigureAwait(false);
                faults?.At("before_commit");
                cancellationToken.ThrowIfCancellationRequested();
                string index = Path.Combine(directory, "index.json"), backup = Path.Combine(directory, "index.backup.json");
                if (File.Exists(index))
                {
                    bool oldValid = false;
                    try
                    {
                        var old = await Read(index, ConfigurationLimits.Bytes, cancellationToken).ConfigureAwait(false);
                        if (old != null)
                        {
                            ProtocolReader.Cache(old, space);
                            oldValid = true;
                        }
                    }
                    catch (InvalidConfigurationException) { }
                    File.Replace(stagedIndex, index, oldValid ? backup : null);
                }
                else
                    File.Move(stagedIndex, index);
                stagedIndex = null;
                committed = true;
                faults?.At("after_commit");
                // Preserve current and fallback index reachability; never visit another namespace.
                var protectedObjects = new HashSet<string>(candidate.Objects.Keys, StringComparer.Ordinal);
                try
                {
                    byte[]? previous = await Read(backup, ConfigurationLimits.Bytes, CancellationToken.None).ConfigureAwait(false);
                    if (previous != null)
                        foreach (string sha in ProtocolReader.Cache(previous, space).Objects.Keys)
                            protectedObjects.Add(sha);
                }
                catch (InvalidConfigurationException) { return StoreCommitOutcome.Committed; }
                foreach (string path in Directory.GetFiles(objectDirectory, "*.json"))
                    if (!protectedObjects.Contains(Path.GetFileNameWithoutExtension(path)))
                        File.Delete(path);
                return StoreCommitOutcome.Committed;
            }
            catch (OperationCanceledException) { if (committed) return StoreCommitOutcome.Unknown; throw; }
            catch (IOException) { return committed ? StoreCommitOutcome.Unknown : StoreCommitOutcome.Unavailable; }
            catch (UnauthorizedAccessException) { return committed ? StoreCommitOutcome.Unknown : StoreCommitOutcome.Unavailable; }
            finally { if (stagedIndex != null && File.Exists(stagedIndex)) File.Delete(stagedIndex); }
        }
    }
}
