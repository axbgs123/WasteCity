using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Content;

namespace WasteCity.Economy
{
    public enum CraftingQueueBlockReason
    {
        None,
        OutputFull,
        MissingContent,
        ResearchRequired,
    }

    public class CraftingQueueRestoreEntry
    {
        private readonly IReadOnlyList<ResourceAmount> reservedInputs;

        public CraftingQueueRestoreEntry(
            string stableExecutionId,
            string recipeId,
            IReadOnlyList<ResourceAmount> reservedInputs)
        {
            StableExecutionId = stableExecutionId;
            RecipeId = recipeId;
            this.reservedInputs = Copy(reservedInputs);
        }

        public string StableExecutionId { get; }
        public string RecipeId { get; }
        public IReadOnlyList<ResourceAmount> ReservedInputs => reservedInputs;

        private static IReadOnlyList<ResourceAmount> Copy(
            IReadOnlyList<ResourceAmount> values)
        {
            if (values == null) return null;
            var copy = new ResourceAmount[values.Count];
            for (var index = 0; index < values.Count; index++)
                copy[index] = values[index];
            return new ReadOnlyCollection<ResourceAmount>(copy);
        }
    }

    public sealed class CraftingQueueExecutionSnapshot :
        CraftingQueueRestoreEntry
    {
        public CraftingQueueExecutionSnapshot(
            string stableExecutionId,
            string recipeId,
            IReadOnlyList<ResourceAmount> reservedInputs)
            : base(stableExecutionId, recipeId, reservedInputs)
        {
        }
    }

    public sealed class CraftingQueueRestorePlan
    {
        internal CraftingQueueRestorePlan(
            CraftingQueueModel owner,
            ulong preparedRevision,
            CraftingQueueExecutionSnapshot[] executions,
            int nextQueueOrdinal,
            float activeProgressSeconds,
            CraftingQueueBlockReason blockReason)
        {
            Owner = owner;
            PreparedRevision = preparedRevision;
            Executions = executions;
            NextQueueOrdinal = nextQueueOrdinal;
            ActiveProgressSeconds = activeProgressSeconds;
            BlockReason = blockReason;
        }

        public ulong PreparedRevision { get; }

        internal CraftingQueueModel Owner { get; }
        internal CraftingQueueExecutionSnapshot[] Executions { get; }
        internal int NextQueueOrdinal { get; }
        internal float ActiveProgressSeconds { get; }
        internal CraftingQueueBlockReason BlockReason { get; }
        internal bool committed;
    }

    public sealed class CraftingQueueModel
    {
        public const int MaximumQueuedExecutions = 20;
        private const int MaximumStableExecutionOrdinal = 999999;
        private const string StableExecutionPrefix = "craft.execution.";

        private readonly PlayerBackpackModel backpack;
        private readonly Func<string, bool> isResearchCompleted;
        private readonly List<CraftingExecution> queue =
            new List<CraftingExecution>(MaximumQueuedExecutions);
        private float activeProgressSeconds;
        private int nextQueueOrdinal = 1;
        private ulong revision;

        public CraftingQueueModel(
            PlayerBackpackModel backpack,
            Func<string, bool> isResearchCompleted)
        {
            this.backpack = backpack ??
                throw new ArgumentNullException(nameof(backpack));
            this.isResearchCompleted = isResearchCompleted ??
                throw new ArgumentNullException(nameof(isResearchCompleted));
        }

        public int QueuedExecutionCount => queue.Count;
        public string ActiveRecipeId => queue.Count > 0
            ? queue[0].RecipeId
            : null;
        public float ActiveProgressSeconds => activeProgressSeconds;
        public CraftingQueueBlockReason BlockReason { get; private set; }
        public int NextQueueOrdinal => nextQueueOrdinal;

        public string QueuedRecipeIdAt(int executionIndex)
        {
            return executionIndex >= 0 && executionIndex < queue.Count
                ? queue[executionIndex].RecipeId
                : null;
        }

        public CraftingQueueExecutionSnapshot[] CaptureExecutions()
        {
            var result = new CraftingQueueExecutionSnapshot[queue.Count];
            for (var index = 0; index < queue.Count; index++)
            {
                CraftingExecution execution = queue[index];
                result[index] = new CraftingQueueExecutionSnapshot(
                    execution.StableExecutionId,
                    execution.RecipeId,
                    execution.ReservedInputs);
            }
            return result;
        }

        public bool TryPrepareRestore(
            IReadOnlyList<CraftingQueueRestoreEntry> executions,
            int restoredNextQueueOrdinal,
            float restoredActiveProgressSeconds,
            out CraftingQueueRestorePlan plan,
            out string error)
        {
            plan = null;
            error = string.Empty;
            if (executions == null ||
                executions.Count > MaximumQueuedExecutions)
            {
                error = "合成队列恢复数据为空或超过容量";
                return false;
            }
            if (restoredNextQueueOrdinal <= 0 ||
                restoredNextQueueOrdinal > MaximumStableExecutionOrdinal)
            {
                error = "合成执行高水位无效";
                return false;
            }
            if (float.IsNaN(restoredActiveProgressSeconds) ||
                float.IsInfinity(restoredActiveProgressSeconds) ||
                restoredActiveProgressSeconds < 0f ||
                executions.Count == 0 && restoredActiveProgressSeconds != 0f)
            {
                error = "合成活动进度无效";
                return false;
            }

            var restored = new CraftingQueueExecutionSnapshot[
                executions.Count];
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            var maximumOrdinal = 0;
            for (var index = 0; index < executions.Count; index++)
            {
                CraftingQueueRestoreEntry execution = executions[index];
                if (execution == null ||
                    !TryParseStableExecutionOrdinal(
                        execution.StableExecutionId,
                        out int ordinal) ||
                    !stableIds.Add(execution.StableExecutionId) ||
                    !IsStableId(execution.RecipeId) ||
                    !TryCopyValidReservations(
                        execution.ReservedInputs,
                        out ResourceAmount[] reservations,
                        out error))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "合成执行记录无效或重复";
                    return false;
                }
                maximumOrdinal = Math.Max(maximumOrdinal, ordinal);

                if (ResourceRecipeCatalog.TryGet(
                        execution.RecipeId,
                        out ResourceRecipeDefinition definition))
                {
                    if (definition.Kind != ResourceRecipeKind.ManualCrafting)
                    {
                        error = "合成队列不能恢复机器配方";
                        return false;
                    }
                }

                restored[index] = new CraftingQueueExecutionSnapshot(
                    execution.StableExecutionId,
                    execution.RecipeId,
                    reservations);
            }
            if (restoredNextQueueOrdinal <= maximumOrdinal)
            {
                error = "合成执行高水位必须大于全部现有执行序号";
                return false;
            }

            CraftingQueueBlockReason restoredBlockReason =
                ComputeRestoredBlockReason(
                    restored,
                    restoredActiveProgressSeconds);
            plan = new CraftingQueueRestorePlan(
                this,
                revision,
                restored,
                restoredNextQueueOrdinal,
                restoredActiveProgressSeconds,
                restoredBlockReason);
            error = string.Empty;
            return true;
        }

        public bool TryCommitRestore(
            CraftingQueueRestorePlan plan,
            out string error)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this))
            {
                error = "合成恢复计划不属于当前队列";
                return false;
            }
            if (plan.committed)
            {
                error = "合成恢复计划已经提交";
                return false;
            }
            if (plan.PreparedRevision != revision)
            {
                error = "合成队列已变化，请重新准备恢复计划";
                return false;
            }

            queue.Clear();
            for (var index = 0; index < plan.Executions.Length; index++)
            {
                CraftingQueueExecutionSnapshot execution =
                    plan.Executions[index];
                ResourceRecipeCatalog.TryGet(
                    execution.RecipeId,
                    out ResourceRecipeDefinition definition);
                queue.Add(new CraftingExecution(
                    execution.StableExecutionId,
                    execution.RecipeId,
                    execution.ReservedInputs,
                    definition));
            }
            nextQueueOrdinal = plan.NextQueueOrdinal;
            activeProgressSeconds = plan.ActiveProgressSeconds;
            RefreshBlockReason();
            plan.committed = true;
            AdvanceRevision();
            error = string.Empty;
            return true;
        }

        public bool TryEnqueue(string recipeId, int executionCount)
        {
            if (executionCount <= 0 ||
                queue.Count > MaximumQueuedExecutions - executionCount ||
                nextQueueOrdinal > MaximumStableExecutionOrdinal -
                    executionCount + 1 ||
                !TryGetUnlockedManualRecipe(recipeId, out var definition) ||
                !TryReserveInputs(definition, executionCount))
            {
                return false;
            }

            for (int index = 0; index < executionCount; index++)
            {
                queue.Add(new CraftingExecution(
                    CreateStableExecutionId(nextQueueOrdinal),
                    definition.Id,
                    definition.Inputs,
                    definition));
                nextQueueOrdinal++;
            }
            AdvanceRevision();
            return true;
        }

        public int EnqueueMaximum(string recipeId)
        {
            if (!TryGetUnlockedManualRecipe(recipeId, out var definition))
                return 0;

            int availableExecutions = AvailableExecutions(definition);

            return availableExecutions > 0 &&
                TryEnqueue(recipeId, availableExecutions)
                ? availableExecutions
                : 0;
        }

        public int MaximumEnqueueable(string recipeId)
        {
            return TryGetUnlockedManualRecipe(recipeId, out var definition)
                ? AvailableExecutions(definition)
                : 0;
        }

        public bool TryCancelAt(int executionIndex)
        {
            if (executionIndex < 0 || executionIndex >= queue.Count)
                return false;
            CraftingExecution execution = queue[executionIndex];
            if (!backpack.TryRefundReservedInputs(execution.ReservedInputs))
                return false;

            queue.RemoveAt(executionIndex);
            if (executionIndex == 0)
            {
                activeProgressSeconds = 0f;
                RefreshBlockReason();
            }
            AdvanceRevision();
            return true;
        }

        public void Tick(float deltaSeconds, bool globallyPaused)
        {
            if (globallyPaused || queue.Count == 0) return;

            float remainingDelta = Math.Max(0f, deltaSeconds);
            var changed = false;
            while (queue.Count > 0)
            {
                CraftingExecution execution = queue[0];
                ResourceRecipeDefinition active = execution.Definition;
                if (active == null)
                {
                    changed |= SetBlockReason(
                        CraftingQueueBlockReason.MissingContent);
                    if (changed) AdvanceRevision();
                    return;
                }
                if (!HasRequiredResearch(active))
                {
                    changed |= SetBlockReason(
                        CraftingQueueBlockReason.ResearchRequired);
                    if (changed) AdvanceRevision();
                    return;
                }
                float required = Math.Max(0f,
                    active.DurationSeconds - activeProgressSeconds);
                float consumed = Math.Min(remainingDelta, required);
                activeProgressSeconds += consumed;
                remainingDelta -= consumed;
                changed |= consumed > 0f;

                if (activeProgressSeconds + .0001f <
                    active.DurationSeconds)
                {
                    changed |= SetBlockReason(CraftingQueueBlockReason.None);
                    if (changed) AdvanceRevision();
                    return;
                }

                if (activeProgressSeconds != active.DurationSeconds)
                {
                    activeProgressSeconds = active.DurationSeconds;
                    changed = true;
                }
                if (!TryAddAll(active.Outputs))
                {
                    changed |= SetBlockReason(
                        CraftingQueueBlockReason.OutputFull);
                    if (changed) AdvanceRevision();
                    return;
                }

                queue.RemoveAt(0);
                activeProgressSeconds = 0f;
                BlockReason = CraftingQueueBlockReason.None;
                changed = true;
                if (remainingDelta <= 0f)
                {
                    RefreshBlockReason();
                    AdvanceRevision();
                    return;
                }
            }
            if (changed) AdvanceRevision();
        }

        private bool TryGetUnlockedManualRecipe(
            string recipeId,
            out ResourceRecipeDefinition definition)
        {
            return ResourceRecipeCatalog.TryGet(recipeId, out definition) &&
                definition.Kind == ResourceRecipeKind.ManualCrafting &&
                (string.IsNullOrWhiteSpace(definition.RequiredResearchId) ||
                 isResearchCompleted(definition.RequiredResearchId));
        }

        private bool TryReserveInputs(
            ResourceRecipeDefinition definition,
            int executionCount)
        {
            var required = new List<ResourceAmount>(definition.Inputs.Count);
            for (int index = 0; index < definition.Inputs.Count; index++)
            {
                ResourceAmount input = definition.Inputs[index];
                long total = (long)input.Amount * executionCount;
                if (total <= 0 || total > int.MaxValue) return false;
                required.Add(new ResourceAmount(input.ResourceId, (int)total));
            }

            BackpackSlot[] before = backpack.CaptureSlots();
            for (int index = 0; index < required.Count; index++)
            {
                ResourceAmount input = required[index];
                if (backpack.Remove(input.ResourceId, input.Amount) !=
                    input.Amount)
                {
                    backpack.RestoreSlots(before);
                    return false;
                }
            }
            return true;
        }

        private bool TryAddAll(IReadOnlyList<ResourceAmount> amounts)
        {
            BackpackSlot[] before = backpack.CaptureSlots();
            for (int index = 0; index < amounts.Count; index++)
            {
                ResourceAmount amount = amounts[index];
                if (backpack.Add(amount.ResourceId, amount.Amount) !=
                    amount.Amount)
                {
                    backpack.RestoreSlots(before);
                    return false;
                }
            }
            return true;
        }

        private CraftingQueueBlockReason ComputeRestoredBlockReason(
            IReadOnlyList<CraftingQueueExecutionSnapshot> executions,
            float progress)
        {
            if (executions.Count == 0) return CraftingQueueBlockReason.None;
            if (!ResourceRecipeCatalog.TryGet(
                    executions[0].RecipeId,
                    out ResourceRecipeDefinition definition))
                return CraftingQueueBlockReason.MissingContent;
            return progress + .0001f >= definition.DurationSeconds &&
                   !CanAddAll(definition.Outputs)
                ? CraftingQueueBlockReason.OutputFull
                : CraftingQueueBlockReason.None;
        }

        private void RefreshBlockReason()
        {
            if (queue.Count == 0)
            {
                BlockReason = CraftingQueueBlockReason.None;
                return;
            }
            CraftingExecution active = queue[0];
            if (active.Definition == null)
            {
                BlockReason = CraftingQueueBlockReason.MissingContent;
                return;
            }
            if (!HasRequiredResearch(active.Definition))
            {
                BlockReason = CraftingQueueBlockReason.ResearchRequired;
                return;
            }
            BlockReason = activeProgressSeconds + .0001f >=
                          active.Definition.DurationSeconds &&
                          !CanAddAll(active.Definition.Outputs)
                ? CraftingQueueBlockReason.OutputFull
                : CraftingQueueBlockReason.None;
        }

        private bool CanAddAll(IReadOnlyList<ResourceAmount> amountsToAdd)
        {
            BackpackSlot[] source = backpack.CaptureSlots();
            var resourceIds = new string[source.Length];
            var amounts = new int[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                resourceIds[index] = source[index].ResourceId;
                amounts[index] = source[index].Amount;
            }

            for (var amountIndex = 0;
                 amountIndex < amountsToAdd.Count;
                 amountIndex++)
            {
                ResourceAmount requested = amountsToAdd[amountIndex];
                if (!ResourceDefinitionCatalog.TryGet(
                        requested.ResourceId,
                        out ResourceDefinition definition) ||
                    requested.Amount <= 0)
                    return false;
                int remaining = requested.Amount;
                for (var index = 0;
                     index < source.Length && remaining > 0;
                     index++)
                {
                    if (!string.Equals(
                            resourceIds[index],
                            requested.ResourceId,
                            StringComparison.Ordinal))
                        continue;
                    int moved = Math.Min(
                        Math.Max(0, definition.StackLimit - amounts[index]),
                        remaining);
                    amounts[index] += moved;
                    remaining -= moved;
                }
                for (var index = 0;
                     index < source.Length && remaining > 0;
                     index++)
                {
                    if (amounts[index] != 0) continue;
                    int moved = Math.Min(definition.StackLimit, remaining);
                    resourceIds[index] = requested.ResourceId;
                    amounts[index] = moved;
                    remaining -= moved;
                }
                if (remaining > 0) return false;
            }
            return true;
        }

        private bool SetBlockReason(CraftingQueueBlockReason value)
        {
            if (BlockReason == value) return false;
            BlockReason = value;
            return true;
        }

        private bool HasRequiredResearch(
            ResourceRecipeDefinition definition)
        {
            return string.IsNullOrWhiteSpace(definition.RequiredResearchId) ||
                   isResearchCompleted(definition.RequiredResearchId);
        }

        private static bool TryCopyValidReservations(
            IReadOnlyList<ResourceAmount> source,
            out ResourceAmount[] copy,
            out string error)
        {
            copy = null;
            if (source == null)
            {
                error = "合成预留输入不能为空";
                return false;
            }
            copy = new ResourceAmount[source.Count];
            var resourceIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < source.Count; index++)
            {
                ResourceAmount amount = source[index];
                if (!IsStableId(amount.ResourceId) || amount.Amount <= 0 ||
                    !resourceIds.Add(amount.ResourceId))
                {
                    error = "合成预留输入无效或重复";
                    return false;
                }
                copy[index] = amount;
            }
            error = string.Empty;
            return true;
        }

        private static bool IsStableId(string value)
        {
            try
            {
                _ = new StableId(value);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool TryParseStableExecutionOrdinal(
            string stableExecutionId,
            out int ordinal)
        {
            ordinal = 0;
            if (string.IsNullOrEmpty(stableExecutionId) ||
                stableExecutionId.Length != StableExecutionPrefix.Length + 6 ||
                !stableExecutionId.StartsWith(
                    StableExecutionPrefix,
                    StringComparison.Ordinal))
                return false;
            for (var index = StableExecutionPrefix.Length;
                 index < stableExecutionId.Length;
                 index++)
            {
                char digit = stableExecutionId[index];
                if (digit < '0' || digit > '9') return false;
                ordinal = ordinal * 10 + digit - '0';
            }
            return ordinal > 0;
        }

        private static string CreateStableExecutionId(int ordinal)
        {
            return $"{StableExecutionPrefix}{ordinal:000000}";
        }

        private void AdvanceRevision()
        {
            revision++;
        }

        private int BackpackAmount(string resourceId)
        {
            long total = 0;
            for (int index = 0; index < backpack.SlotCount; index++)
            {
                BackpackSlot slot = backpack.GetSlot(index);
                if (string.Equals(
                        slot.ResourceId,
                        resourceId,
                        StringComparison.Ordinal))
                {
                    total += slot.Amount;
                }
            }
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }

        private int AvailableExecutions(ResourceRecipeDefinition definition)
        {
            int availableExecutions =
                MaximumQueuedExecutions - queue.Count;
            for (int index = 0; index < definition.Inputs.Count; index++)
            {
                ResourceAmount input = definition.Inputs[index];
                availableExecutions = Math.Min(
                    availableExecutions,
                    BackpackAmount(input.ResourceId) / input.Amount);
            }
            return Math.Max(0, availableExecutions);
        }

        private sealed class CraftingExecution
        {
            private readonly IReadOnlyList<ResourceAmount> reservedInputs;

            public CraftingExecution(
                string stableExecutionId,
                string recipeId,
                IReadOnlyList<ResourceAmount> reservedInputs,
                ResourceRecipeDefinition definition)
            {
                StableExecutionId = stableExecutionId;
                RecipeId = recipeId;
                var copy = new ResourceAmount[reservedInputs.Count];
                for (var index = 0; index < reservedInputs.Count; index++)
                    copy[index] = reservedInputs[index];
                this.reservedInputs =
                    new ReadOnlyCollection<ResourceAmount>(copy);
                Definition = definition;
            }

            public string StableExecutionId { get; }
            public string RecipeId { get; }
            public IReadOnlyList<ResourceAmount> ReservedInputs =>
                reservedInputs;
            public ResourceRecipeDefinition Definition { get; }
        }
    }
}
