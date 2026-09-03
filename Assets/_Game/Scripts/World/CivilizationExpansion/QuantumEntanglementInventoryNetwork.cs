using System;
using System.Collections.Generic;
using WasteCity.Economy;
using WasteCity.Progression;

namespace WasteCity.World.CivilizationExpansion
{
    internal sealed class QuantumEntanglementInventoryNetwork
    {
        private readonly QuantumEntanglementRuntime runtime;
        private readonly Func<string, ISettlementInventoryEndpoint>
            resolveLocalEndpoint;
        private readonly Func<string, bool> isCommunicationActive;
        private readonly Func<IReadOnlyList<string>> settlementIds;

        public QuantumEntanglementInventoryNetwork(
            QuantumEntanglementRuntime runtime,
            Func<string, ISettlementInventoryEndpoint> resolveLocalEndpoint,
            Func<string, bool> isCommunicationActive,
            Func<IReadOnlyList<string>> settlementIds)
        {
            this.runtime = runtime ??
                throw new ArgumentNullException(nameof(runtime));
            this.resolveLocalEndpoint = resolveLocalEndpoint ??
                throw new ArgumentNullException(nameof(resolveLocalEndpoint));
            this.isCommunicationActive = isCommunicationActive ??
                throw new ArgumentNullException(nameof(isCommunicationActive));
            this.settlementIds = settlementIds ??
                throw new ArgumentNullException(nameof(settlementIds));
        }

        public ISettlementInventoryEndpoint CreateEndpoint(
            string settlementId,
            ISettlementInventoryEndpoint localEndpoint)
        {
            return new RoutedEndpoint(this, settlementId, localEndpoint);
        }

        private int GetAmount(
            string requesterId,
            ISettlementInventoryEndpoint localEndpoint,
            string resourceId)
        {
            if (!UsesSharedRoute(requesterId, resourceId))
                return localEndpoint.GetAmount(resourceId);
            Member[] members = BuildConnectedMembers();
            long total = 0;
            for (var index = 0; index < members.Length; index++)
            {
                total += Math.Max(
                    0,
                    members[index].Endpoint.GetAmount(resourceId));
                if (total >= int.MaxValue) return int.MaxValue;
            }
            return (int)total;
        }

        private int GetAcceptableSpace(
            string requesterId,
            ISettlementInventoryEndpoint localEndpoint)
        {
            if (!runtime.Connected || !isCommunicationActive(requesterId))
                return localEndpoint.AcceptableSpace;
            Member[] members = BuildConnectedMembers();
            long total = 0;
            for (var index = 0; index < members.Length; index++)
            {
                total += Math.Max(0, members[index].Endpoint.AcceptableSpace);
                if (total >= int.MaxValue) return int.MaxValue;
            }
            return (int)total;
        }

        private bool TryExtract(
            string requesterId,
            ISettlementInventoryEndpoint localEndpoint,
            IReadOnlyList<ResourceAmount> amounts)
        {
            if (!TryNormalize(
                    amounts,
                    out SortedDictionary<string, int> aggregate))
                return false;
            if (!runtime.Connected || !isCommunicationActive(requesterId))
                return localEndpoint.TryExtract(ToAmounts(aggregate));

            Member[] members = BuildConnectedMembers();
            int requesterIndex = FindMember(members, requesterId);
            if (requesterIndex < 0) return false;
            var plans = new Plan[members.Length];
            foreach (KeyValuePair<string, int> item in aggregate)
            {
                if (!UsesSharedRoute(requesterId, item.Key))
                {
                    if (members[requesterIndex].Endpoint.GetAmount(item.Key) <
                        item.Value)
                        return false;
                    Add(plans, members, requesterIndex, item.Key, item.Value);
                    continue;
                }

                int remaining = item.Value;
                for (var index = 0;
                     index < members.Length && remaining > 0;
                     index++)
                {
                    int available = Math.Max(
                        0,
                        members[index].Endpoint.GetAmount(item.Key));
                    int extracted = Math.Min(available, remaining);
                    if (extracted <= 0) continue;
                    Add(plans, members, index, item.Key, extracted);
                    remaining -= extracted;
                }
                if (remaining > 0) return false;
            }
            bool committed = Commit(
                plans,
                extracting: true,
                requesterId,
                out bool crossedSettlement);
            if (committed && crossedSettlement)
            {
                runtime.TryCommitSynchronization(
                    QuantumEntanglementRuntime.FirstSynchronizationKey);
            }
            return committed;
        }

        private bool TryAccept(
            string requesterId,
            ISettlementInventoryEndpoint localEndpoint,
            IReadOnlyList<ResourceAmount> amounts)
        {
            if (!TryNormalize(
                    amounts,
                    out SortedDictionary<string, int> aggregate))
                return false;
            if (!runtime.Connected || !isCommunicationActive(requesterId))
                return localEndpoint.TryAccept(ToAmounts(aggregate));

            Member[] members = BuildConnectedMembers();
            int requesterIndex = FindMember(members, requesterId);
            if (requesterIndex < 0) return false;
            var plans = new Plan[members.Length];
            var spaces = new int[members.Length];
            for (var index = 0; index < members.Length; index++)
                spaces[index] = Math.Max(
                    0,
                    members[index].Endpoint.AcceptableSpace);

            foreach (KeyValuePair<string, int> item in aggregate)
            {
                if (!UsesSharedRoute(requesterId, item.Key))
                {
                    if (spaces[requesterIndex] < item.Value) return false;
                    Add(plans, members, requesterIndex, item.Key, item.Value);
                    spaces[requesterIndex] -= item.Value;
                    continue;
                }

                int remaining = item.Value;
                for (var index = 0;
                     index < members.Length && remaining > 0;
                     index++)
                {
                    int accepted = Math.Min(spaces[index], remaining);
                    if (accepted <= 0) continue;
                    Add(plans, members, index, item.Key, accepted);
                    spaces[index] -= accepted;
                    remaining -= accepted;
                }
                if (remaining > 0) return false;
            }
            bool committed = Commit(
                plans,
                extracting: false,
                requesterId,
                out bool crossedSettlement);
            if (committed && crossedSettlement)
            {
                runtime.TryCommitSynchronization(
                    QuantumEntanglementRuntime.FirstSynchronizationKey);
            }
            return committed;
        }

        private bool UsesSharedRoute(string requesterId, string resourceId)
        {
            return isCommunicationActive(requesterId) &&
                runtime.ProjectRoute(resourceId).Policy ==
                    QuantumEntanglementRoutePolicy.SharedNetwork;
        }

        private Member[] BuildConnectedMembers()
        {
            IReadOnlyList<string> ids = settlementIds();
            var members = new List<Member>(ids.Count);
            for (var index = 0; index < ids.Count; index++)
            {
                string id = ids[index];
                if (!isCommunicationActive(id)) continue;
                ISettlementInventoryEndpoint endpoint =
                    resolveLocalEndpoint(id);
                if (endpoint != null)
                    members.Add(new Member(id, endpoint));
            }
            return members.ToArray();
        }

        private static int FindMember(Member[] members, string settlementId)
        {
            for (var index = 0; index < members.Length; index++)
            {
                if (string.Equals(
                        members[index].SettlementId,
                        settlementId,
                        StringComparison.Ordinal))
                    return index;
            }
            return -1;
        }

        private static void Add(
            Plan[] plans,
            Member[] members,
            int index,
            string resourceId,
            int amount)
        {
            if (plans[index] == null)
                plans[index] = new Plan(
                    members[index].SettlementId,
                    members[index].Endpoint);
            plans[index].Add(resourceId, amount);
        }

        private static bool Commit(
            Plan[] plans,
            bool extracting,
            string requesterId,
            out bool crossedSettlement)
        {
            crossedSettlement = false;
            var committed = new List<Plan>();
            for (var index = 0; index < plans.Length; index++)
            {
                Plan plan = plans[index];
                if (plan == null) continue;
                ResourceAmount[] amounts = plan.ToAmounts();
                bool changed = extracting
                    ? plan.Endpoint.TryExtract(amounts)
                    : plan.Endpoint.TryAccept(amounts);
                if (changed)
                {
                    committed.Add(plan);
                    continue;
                }
                for (var rollback = committed.Count - 1;
                     rollback >= 0;
                     rollback--)
                {
                    ResourceAmount[] prior = committed[rollback].ToAmounts();
                    if (extracting)
                        committed[rollback].Endpoint.TryAccept(prior);
                    else
                        committed[rollback].Endpoint.TryExtract(prior);
                }
                crossedSettlement = false;
                return false;
            }
            for (var index = 0; index < committed.Count; index++)
            {
                if (!string.Equals(
                        committed[index].SettlementId,
                        requesterId,
                        StringComparison.Ordinal))
                {
                    crossedSettlement = true;
                    break;
                }
            }
            return committed.Count > 0;
        }

        private static bool TryNormalize(
            IReadOnlyList<ResourceAmount> source,
            out SortedDictionary<string, int> aggregate)
        {
            return SettlementInventory.TryAggregate(
                source,
                out aggregate,
                out int total) && total > 0;
        }

        private static ResourceAmount[] ToAmounts(
            SortedDictionary<string, int> source)
        {
            var result = new ResourceAmount[source.Count];
            var index = 0;
            foreach (KeyValuePair<string, int> item in source)
                result[index++] = new ResourceAmount(item.Key, item.Value);
            return result;
        }

        private readonly struct Member
        {
            public Member(
                string settlementId,
                ISettlementInventoryEndpoint endpoint)
            {
                SettlementId = settlementId;
                Endpoint = endpoint;
            }

            public string SettlementId { get; }
            public ISettlementInventoryEndpoint Endpoint { get; }
        }

        private sealed class Plan
        {
            private readonly SortedDictionary<string, int> amounts =
                new SortedDictionary<string, int>(StringComparer.Ordinal);

            public Plan(
                string settlementId,
                ISettlementInventoryEndpoint endpoint)
            {
                SettlementId = settlementId;
                Endpoint = endpoint;
            }

            public string SettlementId { get; }
            public ISettlementInventoryEndpoint Endpoint { get; }

            public void Add(string resourceId, int amount)
            {
                amounts[resourceId] = amounts.TryGetValue(
                    resourceId,
                    out int before)
                    ? before + amount
                    : amount;
            }

            public ResourceAmount[] ToAmounts() =>
                QuantumEntanglementInventoryNetwork.ToAmounts(amounts);
        }

        private sealed class RoutedEndpoint : ISettlementInventoryEndpoint
        {
            private readonly QuantumEntanglementInventoryNetwork network;
            private readonly ISettlementInventoryEndpoint localEndpoint;

            public RoutedEndpoint(
                QuantumEntanglementInventoryNetwork network,
                string settlementId,
                ISettlementInventoryEndpoint localEndpoint)
            {
                this.network = network;
                StableSettlementId = settlementId;
                this.localEndpoint = localEndpoint;
            }

            public string StableSettlementId { get; }
            public int GetAmount(string resourceId) => network.GetAmount(
                StableSettlementId,
                localEndpoint,
                resourceId);
            public int AcceptableSpace => network.GetAcceptableSpace(
                StableSettlementId,
                localEndpoint);
            public bool TryExtract(IReadOnlyList<ResourceAmount> amounts) =>
                network.TryExtract(
                    StableSettlementId,
                    localEndpoint,
                    amounts);
            public bool TryAccept(IReadOnlyList<ResourceAmount> amounts) =>
                network.TryAccept(
                    StableSettlementId,
                    localEndpoint,
                    amounts);
        }
    }
}
