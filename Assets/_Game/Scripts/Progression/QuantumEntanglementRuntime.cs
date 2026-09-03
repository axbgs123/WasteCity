using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Progression
{
    public enum QuantumEntanglementRoutePolicy
    {
        LocalInventoryOnly,
        SharedNetwork,
    }

    public sealed class QuantumEntanglementRouteProjection
    {
        internal QuantumEntanglementRouteProjection(
            string resourceId,
            bool isSharedResource,
            bool connectionAvailable,
            QuantumEntanglementRoutePolicy policy)
        {
            ResourceId = resourceId ?? string.Empty;
            IsSharedResource = isSharedResource;
            ConnectionAvailable = connectionAvailable;
            Policy = policy;
        }

        public string ResourceId { get; }
        public bool IsSharedResource { get; }
        public bool ConnectionAvailable { get; }
        public QuantumEntanglementRoutePolicy Policy { get; }
    }

    public sealed class QuantumEntanglementSnapshot
    {
        private readonly ReadOnlyCollection<string> sharedResourceIds;
        private readonly ReadOnlyCollection<string>
            committedSynchronizationKeys;

        public QuantumEntanglementSnapshot(
            bool connected,
            ulong revision,
            string[] sharedResourceIds)
            : this(
                connected,
                revision,
                sharedResourceIds,
                Array.Empty<string>())
        {
        }

        public QuantumEntanglementSnapshot(
            bool connected,
            ulong revision,
            string[] sharedResourceIds,
            string[] committedSynchronizationKeys)
        {
            Connected = connected;
            Revision = revision;
            this.sharedResourceIds = Array.AsReadOnly(
                sharedResourceIds == null
                    ? Array.Empty<string>()
                    : (string[])sharedResourceIds.Clone());
            this.committedSynchronizationKeys = Array.AsReadOnly(
                committedSynchronizationKeys == null
                    ? Array.Empty<string>()
                    : (string[])committedSynchronizationKeys.Clone());
        }

        public bool Connected { get; }
        public ulong Revision { get; }
        public IReadOnlyList<string> SharedResourceIds => sharedResourceIds;
        public IReadOnlyList<string> CommittedSynchronizationKeys =>
            committedSynchronizationKeys;
    }

    public sealed class QuantumEntanglementRuntime
    {
        public const string FirstSynchronizationKey =
            "core.fate.quantum.first-synchronization";
        private const string SynchronizationKeyPrefix = "core.fate.quantum.";

        private readonly HashSet<string> sharedResourceIds;
        private readonly HashSet<string> committedSynchronizationKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private bool connected = true;
        private ulong revision;
        private QuantumEntanglementSnapshot cachedSnapshot;

        public QuantumEntanglementRuntime(IEnumerable<string> sharedResourceIds)
        {
            if (sharedResourceIds == null)
                throw new ArgumentNullException(nameof(sharedResourceIds));
            this.sharedResourceIds = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (string resourceId in sharedResourceIds)
            {
                if (string.IsNullOrWhiteSpace(resourceId))
                    throw new ArgumentException(
                        "Shared resource IDs cannot be blank.",
                        nameof(sharedResourceIds));
                this.sharedResourceIds.Add(resourceId);
            }
            RebuildSnapshot();
        }

        public bool Connected => connected;
        public ulong Revision => revision;

        public bool TrySetConnected(bool value)
        {
            if (connected == value) return false;
            connected = value;
            unchecked { revision++; }
            RebuildSnapshot();
            return true;
        }

        public bool TryCommitSynchronization(string stableKey)
        {
            if (!IsSynchronizationKey(stableKey) ||
                !committedSynchronizationKeys.Add(stableKey))
                return false;
            unchecked { revision++; }
            RebuildSnapshot();
            return true;
        }

        public QuantumEntanglementRouteProjection ProjectRoute(
            string resourceId)
        {
            bool shared = !string.IsNullOrWhiteSpace(resourceId) &&
                sharedResourceIds.Contains(resourceId);
            return new QuantumEntanglementRouteProjection(
                resourceId,
                shared,
                connected,
                shared && connected
                    ? QuantumEntanglementRoutePolicy.SharedNetwork
                    : QuantumEntanglementRoutePolicy.LocalInventoryOnly);
        }

        public QuantumEntanglementSnapshot Capture() => cachedSnapshot;

        public bool TryRestore(
            QuantumEntanglementSnapshot snapshot,
            out string error)
        {
            if (snapshot == null)
            {
                error = "Quantum entanglement snapshot is required.";
                return false;
            }

            var restoredIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < snapshot.SharedResourceIds.Count; index++)
            {
                string resourceId = snapshot.SharedResourceIds[index];
                if (string.IsNullOrWhiteSpace(resourceId) ||
                    !restoredIds.Add(resourceId))
                {
                    error = "Shared resource IDs must be non-blank and unique.";
                    return false;
                }
            }
            if (restoredIds.Count != sharedResourceIds.Count ||
                !restoredIds.SetEquals(sharedResourceIds))
            {
                error = "Shared resource IDs do not match formal configuration.";
                return false;
            }
            var restoredKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0;
                 index < snapshot.CommittedSynchronizationKeys.Count;
                 index++)
            {
                string stableKey =
                    snapshot.CommittedSynchronizationKeys[index];
                if (!IsSynchronizationKey(stableKey) ||
                    !restoredKeys.Add(stableKey))
                {
                    error = "Synchronization keys must be valid and unique.";
                    return false;
                }
            }
            if (!snapshot.Connected && snapshot.Revision == 0)
            {
                error = "A disconnected network must have a positive revision.";
                return false;
            }
            if (restoredKeys.Count > 0 && snapshot.Revision == 0)
            {
                error = "Committed synchronization requires a revision.";
                return false;
            }

            committedSynchronizationKeys.Clear();
            committedSynchronizationKeys.UnionWith(restoredKeys);
            connected = snapshot.Connected;
            revision = snapshot.Revision;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        private static bool IsSynchronizationKey(string stableKey)
        {
            return !string.IsNullOrWhiteSpace(stableKey) &&
                stableKey.StartsWith(
                    SynchronizationKeyPrefix,
                    StringComparison.Ordinal);
        }

        private void RebuildSnapshot()
        {
            var ids = new string[sharedResourceIds.Count];
            sharedResourceIds.CopyTo(ids);
            Array.Sort(ids, StringComparer.Ordinal);
            var keys = new string[committedSynchronizationKeys.Count];
            committedSynchronizationKeys.CopyTo(keys);
            Array.Sort(keys, StringComparer.Ordinal);
            cachedSnapshot = new QuantumEntanglementSnapshot(
                connected,
                revision,
                ids,
                keys);
        }
    }
}
