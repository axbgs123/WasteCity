using System;
using System.Text.RegularExpressions;

namespace WasteCity.Content
{
    public readonly struct StableId : IEquatable<StableId>
    {
        private static readonly Regex Pattern = new Regex("^[a-z0-9]+(?:[.-][a-z0-9]+){2,}$", RegexOptions.Compiled);
        public string Value { get; }
        public StableId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !Pattern.IsMatch(value))
                throw new ArgumentException("Stable IDs require at least three lowercase segments.", nameof(value));
            Value = value;
        }
        public bool Equals(StableId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is StableId other && Equals(other);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public override string ToString() => Value;
    }
}
