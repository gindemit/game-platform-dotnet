using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;
using GamePlatform.Features.Contracts;

namespace GamePlatform.Features.Progression
{

    /// <summary>Immutable game-neutral string extension for a completion command; the server remains authoritative.</summary>
    public sealed class ProgressionCommandExtension
    {
        private readonly ReadOnlyDictionary<string, string> value;
        public ProgressionCommandExtension(string schema, int version, IReadOnlyDictionary<string, string> value)
        {
            ProgressionValidation.Semantic(schema, nameof(schema));
            if (version < 1)
                throw new ArgumentOutOfRangeException(nameof(version));
            if (value == null || value.Count > 128)
                throw new ArgumentOutOfRangeException(nameof(value));
            var copied = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in value)
            {
                ProgressionValidation.Semantic(item.Key, nameof(value));
                if (item.Value == null || item.Value.Length > 4096)
                    throw new ArgumentOutOfRangeException(nameof(value));
                copied.Add(item.Key, item.Value);
            }
            Schema = schema;
            Version = version;
            this.value = new ReadOnlyDictionary<string, string>(copied);
        }
        public string Schema { get; }
        public int Version { get; }
        public IReadOnlyDictionary<string, string> Value => value;
    }
}
