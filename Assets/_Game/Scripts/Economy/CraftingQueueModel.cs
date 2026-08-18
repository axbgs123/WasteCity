using System;
using System.Collections.Generic;

namespace WasteCity.Economy
{
    public enum CraftingQueueBlockReason
    {
        None,
        OutputFull,
    }

    public sealed class CraftingQueueModel
    {
        public const int MaximumQueuedExecutions = 20;

        private readonly PlayerBackpackModel backpack;
        private readonly Func<string, bool> isResearchCompleted;
        private readonly List<ResourceRecipeDefinition> queue =
            new List<ResourceRecipeDefinition>(MaximumQueuedExecutions);
        private float activeProgressSeconds;

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
            ? queue[0].Id
            : null;
        public float ActiveProgressSeconds => activeProgressSeconds;
        public CraftingQueueBlockReason BlockReason { get; private set; }

        public string QueuedRecipeIdAt(int executionIndex)
        {
            return executionIndex >= 0 && executionIndex < queue.Count
                ? queue[executionIndex].Id
                : null;
        }

        public bool TryEnqueue(string recipeId, int executionCount)
        {
            if (executionCount <= 0 ||
                queue.Count > MaximumQueuedExecutions - executionCount ||
                !TryGetUnlockedManualRecipe(recipeId, out var definition) ||
                !TryReserveInputs(definition, executionCount))
            {
                return false;
            }

            for (int index = 0; index < executionCount; index++)
                queue.Add(definition);
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
            ResourceRecipeDefinition definition = queue[executionIndex];
            if (!TryAddAll(definition.Inputs)) return false;

            queue.RemoveAt(executionIndex);
            if (executionIndex == 0)
            {
                activeProgressSeconds = 0f;
                BlockReason = CraftingQueueBlockReason.None;
            }
            return true;
        }

        public void Tick(float deltaSeconds, bool globallyPaused)
        {
            if (globallyPaused || queue.Count == 0) return;

            float remainingDelta = Math.Max(0f, deltaSeconds);
            while (queue.Count > 0)
            {
                ResourceRecipeDefinition active = queue[0];
                float required = Math.Max(0f,
                    active.DurationSeconds - activeProgressSeconds);
                float consumed = Math.Min(remainingDelta, required);
                activeProgressSeconds += consumed;
                remainingDelta -= consumed;

                if (activeProgressSeconds + .0001f <
                    active.DurationSeconds)
                {
                    BlockReason = CraftingQueueBlockReason.None;
                    return;
                }

                activeProgressSeconds = active.DurationSeconds;
                if (!TryAddAll(active.Outputs))
                {
                    BlockReason = CraftingQueueBlockReason.OutputFull;
                    return;
                }

                queue.RemoveAt(0);
                activeProgressSeconds = 0f;
                BlockReason = CraftingQueueBlockReason.None;
                if (remainingDelta <= 0f) return;
            }
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
    }
}
