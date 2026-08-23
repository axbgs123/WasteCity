using System;
using System.Collections.Generic;
using WasteCity.Economy;
using WasteCity.Persistence.ThreeD;
using WasteCity.Research;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxEconomySaveAdapter3D
    {
        private readonly PlayerBackpackModel backpack;
        private readonly CraftingQueueModel crafting;
        private readonly DemoResearchRuntime demoResearch;
        private readonly FormalResearchRuntime formalResearch;

        public GrayboxEconomySaveAdapter3D(
            PlayerBackpackModel backpack,
            CraftingQueueModel crafting,
            DemoResearchRuntime research)
        {
            this.backpack = backpack ??
                throw new ArgumentNullException(nameof(backpack));
            this.crafting = crafting ??
                throw new ArgumentNullException(nameof(crafting));
            demoResearch = research ??
                throw new ArgumentNullException(nameof(research));
        }

        public GrayboxEconomySaveAdapter3D(
            PlayerBackpackModel backpack,
            CraftingQueueModel crafting,
            FormalResearchRuntime research)
        {
            this.backpack = backpack ??
                throw new ArgumentNullException(nameof(backpack));
            this.crafting = crafting ??
                throw new ArgumentNullException(nameof(crafting));
            formalResearch = research ??
                throw new ArgumentNullException(nameof(research));
        }

        public FormalThreeDBackpackSaveData CaptureBackpack()
        {
            PlayerBackpackRestoreSlot[] source =
                backpack.CaptureRestoreSlots();
            var slots = new FormalThreeDBackpackSlotSaveData[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                slots[index] = new FormalThreeDBackpackSlotSaveData
                {
                    slotIndex = source[index].SlotIndex,
                    resourceId = source[index].ResourceId,
                    amount = source[index].Amount,
                };
            }
            return new FormalThreeDBackpackSaveData { slots = slots };
        }

        public FormalThreeDCraftingSaveData CaptureCrafting()
        {
            CraftingQueueExecutionSnapshot[] source =
                crafting.CaptureExecutions();
            var executions = new FormalThreeDCraftingExecutionSaveData[
                source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                IReadOnlyList<ResourceAmount> reserved =
                    source[index].ReservedInputs;
                var amounts = new FormalThreeDResourceAmountSaveData[
                    reserved.Count];
                for (var amountIndex = 0;
                     amountIndex < reserved.Count;
                     amountIndex++)
                {
                    amounts[amountIndex] =
                        new FormalThreeDResourceAmountSaveData
                        {
                            resourceId = reserved[amountIndex].ResourceId,
                            amount = reserved[amountIndex].Amount,
                        };
                }
                executions[index] =
                    new FormalThreeDCraftingExecutionSaveData
                    {
                        stableExecutionId =
                            source[index].StableExecutionId,
                        recipeId = source[index].RecipeId,
                        reservedInputs = amounts,
                    };
            }
            return new FormalThreeDCraftingSaveData
            {
                nextQueueOrdinal = crafting.NextQueueOrdinal,
                activeProgressSeconds = crafting.ActiveProgressSeconds,
                executions = executions,
            };
        }

        public FormalThreeDResearchSaveData CaptureResearch()
        {
            ResearchPersistenceSnapshot snapshot =
                CaptureResearchSnapshot();
            var completed = new string[
                snapshot.CompletedResearchIds.Count];
            for (var index = 0; index < completed.Length; index++)
                completed[index] = snapshot.CompletedResearchIds[index];
            return new FormalThreeDResearchSaveData
            {
                completedResearchIds = completed,
                activeResearchId = snapshot.ActiveResearchId,
                remainingSeconds = snapshot.RemainingSeconds,
            };
        }

        public bool TryRestore(
            FormalThreeDBackpackSaveData backpackData,
            FormalThreeDCraftingSaveData craftingData,
            FormalThreeDResearchSaveData researchData,
            bool allowBackpackOverStack,
            out string error)
        {
            if (backpackData?.slots == null ||
                craftingData?.executions == null ||
                researchData?.completedResearchIds == null)
            {
                error = "背包、合成或科技存档数据不完整";
                return false;
            }

            if (!TryBuildBackpackSlots(
                    backpackData,
                    out List<PlayerBackpackRestoreSlot> backpackSlots,
                    out error) ||
                !backpack.TryPrepareRestore(
                    backpackSlots,
                    allowBackpackOverStack,
                    out PlayerBackpackRestorePlan backpackPlan,
                    out error) ||
                !TryPrepareResearchRestore(
                    researchData.completedResearchIds,
                    researchData.activeResearchId,
                    researchData.remainingSeconds,
                    out ResearchRestorePlan researchPlan,
                    out error) ||
                !TryBuildCraftingEntries(
                    craftingData,
                    out List<CraftingQueueRestoreEntry> craftingEntries,
                    out error) ||
                !crafting.TryPrepareRestore(
                    craftingEntries,
                    craftingData.nextQueueOrdinal,
                    craftingData.activeProgressSeconds,
                    out CraftingQueueRestorePlan craftingPlan,
                    out error))
            {
                return false;
            }

            if (!backpack.TryCommitRestore(backpackPlan, out error) ||
                !TryCommitResearchRestore(
                    researchPlan,
                    out error) ||
                !crafting.TryCommitRestore(craftingPlan, out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        private ResearchPersistenceSnapshot CaptureResearchSnapshot()
        {
            return formalResearch != null
                ? formalResearch.CaptureForPersistence()
                : demoResearch.CaptureForPersistence();
        }

        private bool TryPrepareResearchRestore(
            IReadOnlyList<string> completedResearchIds,
            string activeResearchId,
            float remainingSeconds,
            out ResearchRestorePlan plan,
            out string error)
        {
            return formalResearch != null
                ? formalResearch.TryPrepareRestoreForPersistence(
                    completedResearchIds,
                    activeResearchId,
                    remainingSeconds,
                    out plan,
                    out error)
                : demoResearch.TryPrepareRestoreForPersistence(
                    completedResearchIds,
                    activeResearchId,
                    remainingSeconds,
                    out plan,
                    out error);
        }

        private bool TryCommitResearchRestore(
            ResearchRestorePlan plan,
            out string error)
        {
            return formalResearch != null
                ? formalResearch.TryCommitRestoreForPersistence(
                    plan,
                    out error)
                : demoResearch.TryCommitRestoreForPersistence(
                    plan,
                    out error);
        }

        private static bool TryBuildBackpackSlots(
            FormalThreeDBackpackSaveData data,
            out List<PlayerBackpackRestoreSlot> slots,
            out string error)
        {
            slots = new List<PlayerBackpackRestoreSlot>(data.slots.Length);
            for (var index = 0; index < data.slots.Length; index++)
            {
                FormalThreeDBackpackSlotSaveData saved = data.slots[index];
                if (saved == null)
                {
                    error = "背包槽位记录不能为空";
                    return false;
                }
                slots.Add(new PlayerBackpackRestoreSlot(
                    saved.slotIndex,
                    saved.resourceId,
                    saved.amount));
            }
            error = string.Empty;
            return true;
        }

        private static bool TryBuildCraftingEntries(
            FormalThreeDCraftingSaveData data,
            out List<CraftingQueueRestoreEntry> entries,
            out string error)
        {
            entries = new List<CraftingQueueRestoreEntry>(
                data.executions.Length);
            for (var index = 0; index < data.executions.Length; index++)
            {
                FormalThreeDCraftingExecutionSaveData saved =
                    data.executions[index];
                if (saved?.reservedInputs == null)
                {
                    error = "合成执行项或预留输入记录不能为空";
                    return false;
                }
                var reserved = new ResourceAmount[
                    saved.reservedInputs.Length];
                for (var amountIndex = 0;
                     amountIndex < saved.reservedInputs.Length;
                     amountIndex++)
                {
                    FormalThreeDResourceAmountSaveData amount =
                        saved.reservedInputs[amountIndex];
                    if (amount == null)
                    {
                        error = "合成预留输入记录不能为空";
                        return false;
                    }
                    reserved[amountIndex] = new ResourceAmount(
                        amount.resourceId,
                        amount.amount);
                }
                entries.Add(new CraftingQueueRestoreEntry(
                    saved.stableExecutionId,
                    saved.recipeId,
                    reserved));
            }
            error = string.Empty;
            return true;
        }
    }
}
