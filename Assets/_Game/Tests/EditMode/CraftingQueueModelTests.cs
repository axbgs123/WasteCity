using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class CraftingQueueModelTests
    {
        [Test]
        public void CatalogContainsTheExactTwoApprovedEmergencyRecipes()
        {
            ResourceRecipeDefinition[] crafting = ResourceRecipeCatalog.All
                .Where(definition =>
                    definition.Kind == ResourceRecipeKind.ManualCrafting)
                .ToArray();

            Assert.That(crafting, Has.Length.EqualTo(2));
            Assert.That(ResourceRecipeCatalog.DisplayName(crafting[0].Id),
                Is.EqualTo("应急合金"));
            Assert.That(ResourceRecipeCatalog.DisplayName(crafting[1].Id),
                Is.EqualTo("应急弹药"));
            AssertRecipe(
                crafting[0],
                "core.crafting.field-alloy",
                DemoResearchCatalog.BasicMetallurgyId,
                ResourceIds.Iron,
                4,
                ResourceIds.Alloy,
                1,
                12f);
            AssertRecipe(
                crafting[1],
                "core.crafting.field-ammunition",
                DemoResearchCatalog.AmmunitionAssemblyId,
                ResourceIds.Alloy,
                4,
                ResourceIds.Ammunition,
                2,
                12f);

            Assert.That(ResourceRecipeCatalog.TryGet(
                FormalProductionDefinitionCatalog.Smelting.Id,
                out ResourceRecipeDefinition machine), Is.True);
            Assert.That(machine.Kind, Is.EqualTo(ResourceRecipeKind.Machine));
        }

        [Test]
        public void CatalogPreservesAllMachineRecipesIncludingDynamicNodeOutput()
        {
            Assert.That(ResourceRecipeCatalog.TryGet(
                FormalProductionDefinitionCatalog.Extraction.Id,
                out ResourceRecipeDefinition extraction), Is.True);
            Assert.That(extraction.Kind, Is.EqualTo(ResourceRecipeKind.Machine));
            Assert.That(extraction.DurationSeconds, Is.EqualTo(3f));
            Assert.That(extraction.Inputs, Is.Empty);
            Assert.That(extraction.Outputs, Is.Empty);
            Assert.That(extraction.UsesBoundResourceNode, Is.True);
            Assert.That(extraction.BoundResourceNodeOutputAmount, Is.EqualTo(1));
            Assert.That(extraction.RequiredResearchId,
                Is.EqualTo(DemoResearchCatalog.ScrapProcessingId));

            Assert.That(ResourceRecipeCatalog.TryGet(
                FormalProductionDefinitionCatalog.Smelting.Id,
                out ResourceRecipeDefinition smelting), Is.True);
            Assert.That(smelting.UsesBoundResourceNode, Is.False);
            Assert.That(smelting.BoundResourceNodeOutputAmount, Is.Zero);
            AssertRecipe(
                smelting,
                FormalProductionDefinitionCatalog.Smelting.Id,
                DemoResearchCatalog.BasicMetallurgyId,
                ResourceIds.Iron,
                2,
                ResourceIds.Alloy,
                1,
                6f);

            Assert.That(ResourceRecipeCatalog.TryGet(
                FormalProductionDefinitionCatalog.Assembly.Id,
                out ResourceRecipeDefinition assembly), Is.True);
            Assert.That(assembly.UsesBoundResourceNode, Is.False);
            Assert.That(assembly.BoundResourceNodeOutputAmount, Is.Zero);
            AssertRecipe(
                assembly,
                FormalProductionDefinitionCatalog.Assembly.Id,
                DemoResearchCatalog.AmmunitionAssemblyId,
                ResourceIds.Alloy,
                2,
                ResourceIds.Ammunition,
                2,
                6f);
        }

        [Test]
        public void QueueUsesCompletedResearchAndBackpackOnly()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 8);
            var city = new ResourceInventory(500);
            city.Add(ResourceIds.Iron, 100);
            var buildingCache = new ResourceInventory(20);
            buildingCache.Add(ResourceIds.Iron, 20);
            var research = new DemoResearchRuntime(new ResearchModel());
            var queue = new CraftingQueueModel(backpack, research.IsCompleted);

            Assert.That(queue.TryEnqueue(
                "core.crafting.field-alloy",
                1), Is.False);
            Assert.That(BackpackAmount(backpack, ResourceIds.Iron),
                Is.EqualTo(8));

            var researchCost = new ResourceInventory(100);
            researchCost.Add(ResourceIds.Iron, 10);
            Assert.That(research.TryStart(
                DemoResearchCatalog.BasicMetallurgyId,
                researchCost,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(research.Tick(
                20f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.True);

            Assert.That(queue.TryEnqueue(
                "core.crafting.field-alloy",
                1), Is.True);
            Assert.That(BackpackAmount(backpack, ResourceIds.Iron),
                Is.EqualTo(4));
            Assert.That(queue.TryEnqueue(
                "core.crafting.field-ammunition",
                1), Is.False);
            Assert.That(queue.TryEnqueue(
                FormalProductionDefinitionCatalog.Smelting.Id,
                1), Is.False);

            var emptyBackpack = new PlayerBackpackModel();
            var unlockedQueue = new CraftingQueueModel(
                emptyBackpack,
                _ => true);
            Assert.That(unlockedQueue.TryEnqueue(
                "core.crafting.field-alloy",
                1), Is.False);
            Assert.That(city.Get(ResourceIds.Iron), Is.EqualTo(100));
            Assert.That(buildingCache.Get(ResourceIds.Iron), Is.EqualTo(20));
        }

        [Test]
        public void AmmunitionCraftingRequiresCompletedAssemblyResearch()
        {
            var research = new DemoResearchRuntime(new ResearchModel());
            var researchInventory = new ResourceInventory(100);
            researchInventory.Add(ResourceIds.Iron, 10);
            researchInventory.Add(ResourceIds.Alloy, 10);
            Assert.That(research.TryStart(
                DemoResearchCatalog.BasicMetallurgyId,
                researchInventory,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(research.Tick(
                20f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.True);

            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Alloy, 4);
            var queue = new CraftingQueueModel(
                backpack,
                research.IsCompleted);
            Assert.That(queue.TryEnqueue(
                ResourceRecipeCatalog.FieldAmmunitionId,
                1), Is.False);
            Assert.That(BackpackAmount(backpack, ResourceIds.Alloy),
                Is.EqualTo(4));

            Assert.That(research.TryStart(
                DemoResearchCatalog.AmmunitionAssemblyId,
                researchInventory,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(research.Tick(
                30f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(queue.TryEnqueue(
                ResourceRecipeCatalog.FieldAmmunitionId,
                1), Is.True);
            Assert.That(BackpackAmount(backpack, ResourceIds.Alloy), Is.Zero);
        }

        [Test]
        public void ExactFiveIsAllOrNothingAndMaximumUsesResourcesAndExecutionCapacity()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 19);
            var queue = new CraftingQueueModel(backpack, _ => true);

            Assert.That(queue.TryEnqueue(
                "core.crafting.field-alloy",
                5), Is.False);
            Assert.That(queue.QueuedExecutionCount, Is.Zero);
            Assert.That(BackpackAmount(backpack, ResourceIds.Iron),
                Is.EqualTo(19));

            backpack.Add(ResourceIds.Iron, 61);
            Assert.That(queue.TryEnqueue(
                "core.crafting.field-alloy",
                5), Is.True);
            Assert.That(queue.QueuedExecutionCount, Is.EqualTo(5));
            Assert.That(BackpackAmount(backpack, ResourceIds.Iron),
                Is.EqualTo(60));

            Assert.That(queue.EnqueueMaximum(
                "core.crafting.field-alloy"), Is.EqualTo(15));
            Assert.That(queue.QueuedExecutionCount, Is.EqualTo(20));
            Assert.That(BackpackAmount(backpack, ResourceIds.Iron), Is.Zero);
            Assert.That(queue.TryEnqueue(
                "core.crafting.field-alloy",
                1), Is.False);
            Assert.That(queue.EnqueueMaximum(
                "core.crafting.field-alloy"), Is.Zero);
        }

        [Test]
        public void PendingOutputsCannotFundAutomaticChaining()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 8);
            var queue = new CraftingQueueModel(backpack, _ => true);

            Assert.That(queue.TryEnqueue(
                "core.crafting.field-alloy",
                2), Is.True);
            Assert.That(queue.TryEnqueue(
                "core.crafting.field-ammunition",
                1), Is.False);
            Assert.That(queue.QueuedExecutionCount, Is.EqualTo(2));
            Assert.That(BackpackAmount(backpack, ResourceIds.Iron), Is.Zero);
            Assert.That(BackpackAmount(backpack, ResourceIds.Alloy), Is.Zero);
        }

        [Test]
        public void FifoLargeDeltaMatchesSplitTicksWithoutParallelProgress()
        {
            PlayerBackpackModel combinedBackpack = BackpackForTwoRecipes();
            PlayerBackpackModel splitBackpack = BackpackForTwoRecipes();
            var combined = new CraftingQueueModel(combinedBackpack, _ => true);
            var split = new CraftingQueueModel(splitBackpack, _ => true);
            EnqueueTwoDifferentRecipes(combined);
            EnqueueTwoDifferentRecipes(split);

            Assert.That(combined.ActiveRecipeId,
                Is.EqualTo(ResourceRecipeCatalog.FieldAlloyId));
            Assert.That(combined.QueuedRecipeIdAt(0),
                Is.EqualTo(ResourceRecipeCatalog.FieldAlloyId));
            Assert.That(combined.QueuedRecipeIdAt(1),
                Is.EqualTo(ResourceRecipeCatalog.FieldAmmunitionId));
            Assert.That(combined.QueuedRecipeIdAt(2), Is.Null);

            combined.Tick(24f, globallyPaused: false);
            split.Tick(12f, globallyPaused: false);

            Assert.That(split.QueuedExecutionCount, Is.EqualTo(1));
            Assert.That(BackpackAmount(splitBackpack, ResourceIds.Alloy),
                Is.EqualTo(1));
            Assert.That(BackpackAmount(splitBackpack, ResourceIds.Ammunition),
                Is.Zero);

            split.Tick(12f, globallyPaused: false);
            AssertQueueSnapshotsEqual(
                combined,
                combinedBackpack,
                split,
                splitBackpack);
            Assert.That(combined.QueuedExecutionCount, Is.Zero);
            Assert.That(BackpackAmount(combinedBackpack, ResourceIds.Alloy),
                Is.EqualTo(1));
            Assert.That(BackpackAmount(
                combinedBackpack,
                ResourceIds.Ammunition), Is.EqualTo(2));
        }

        [Test]
        public void FullBackpackBlocksCompletedOutputWithoutLossOrDuplication()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 4);
            var queue = new CraftingQueueModel(backpack, _ => true);
            Assert.That(queue.TryEnqueue(
                "core.crafting.field-alloy",
                1), Is.True);
            Assert.That(backpack.Add(ResourceIds.Stone, 3000),
                Is.EqualTo(3000));

            queue.Tick(12f, globallyPaused: false);
            Assert.That(queue.QueuedExecutionCount, Is.EqualTo(1));
            Assert.That(queue.ActiveProgressSeconds, Is.EqualTo(12f));
            Assert.That(queue.BlockReason,
                Is.EqualTo(CraftingQueueBlockReason.OutputFull));
            Assert.That(BackpackAmount(backpack, ResourceIds.Alloy), Is.Zero);

            queue.Tick(120f, globallyPaused: false);
            Assert.That(queue.QueuedExecutionCount, Is.EqualTo(1));
            Assert.That(BackpackAmount(backpack, ResourceIds.Alloy), Is.Zero);

            Assert.That(backpack.Remove(ResourceIds.Stone, 100),
                Is.EqualTo(100));
            queue.Tick(.1f, globallyPaused: false);
            Assert.That(queue.QueuedExecutionCount, Is.Zero);
            Assert.That(queue.BlockReason,
                Is.EqualTo(CraftingQueueBlockReason.None));
            Assert.That(BackpackAmount(backpack, ResourceIds.Alloy),
                Is.EqualTo(1));
        }

        [Test]
        public void CancelReturnsOnlyThatExecutionAndRejectsAnAtomicRefundWithoutSpace()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 8);
            var queue = new CraftingQueueModel(backpack, _ => true);
            Assert.That(queue.TryEnqueue(
                "core.crafting.field-alloy",
                2), Is.True);

            Assert.That(queue.TryCancelAt(1), Is.True);
            Assert.That(queue.QueuedExecutionCount, Is.EqualTo(1));
            Assert.That(BackpackAmount(backpack, ResourceIds.Iron),
                Is.EqualTo(4));

            Assert.That(backpack.Add(ResourceIds.Iron, 96), Is.EqualTo(96));
            Assert.That(backpack.Add(ResourceIds.Stone, 2900),
                Is.EqualTo(2900));
            Assert.That(queue.TryCancelAt(0), Is.False);
            Assert.That(queue.QueuedExecutionCount, Is.EqualTo(1));
            Assert.That(BackpackAmount(backpack, ResourceIds.Iron),
                Is.EqualTo(100));

            Assert.That(backpack.Remove(ResourceIds.Stone, 100),
                Is.EqualTo(100));
            Assert.That(queue.TryCancelAt(0), Is.True);
            Assert.That(queue.QueuedExecutionCount, Is.Zero);
            Assert.That(BackpackAmount(backpack, ResourceIds.Iron),
                Is.EqualTo(104));
        }

        [Test]
        public void GlobalPauseFreezesProgressAndResumeHasNoCatchUp()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 4);
            var queue = new CraftingQueueModel(backpack, _ => true);
            Assert.That(queue.TryEnqueue(
                "core.crafting.field-alloy",
                1), Is.True);

            queue.Tick(6f, globallyPaused: false);
            Assert.That(queue.ActiveProgressSeconds, Is.EqualTo(6f));
            queue.Tick(120f, globallyPaused: true);
            Assert.That(queue.ActiveProgressSeconds, Is.EqualTo(6f));
            Assert.That(queue.QueuedExecutionCount, Is.EqualTo(1));

            queue.Tick(6f, globallyPaused: false);
            Assert.That(queue.QueuedExecutionCount, Is.Zero);
            Assert.That(BackpackAmount(backpack, ResourceIds.Alloy),
                Is.EqualTo(1));
        }

        [Test]
        public void PersistentExecutionsOwnStableIdsReservationsAndHighWater()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 12);
            var queue = new CraftingQueueModel(backpack, _ => true);

            Assert.That(queue.TryEnqueue(
                ResourceRecipeCatalog.FieldAlloyId,
                2), Is.True);
            CraftingQueueExecutionSnapshot[] first =
                queue.CaptureExecutions();

            Assert.That(first, Has.Length.EqualTo(2));
            Assert.That(first[0].StableExecutionId,
                Is.EqualTo("craft.execution.000001"));
            Assert.That(first[1].StableExecutionId,
                Is.EqualTo("craft.execution.000002"));
            Assert.That(first[0].RecipeId,
                Is.EqualTo(ResourceRecipeCatalog.FieldAlloyId));
            Assert.That(first[0].ReservedInputs, Has.Count.EqualTo(1));
            Assert.That(first[0].ReservedInputs[0].ResourceId,
                Is.EqualTo(ResourceIds.Iron));
            Assert.That(first[0].ReservedInputs[0].Amount, Is.EqualTo(4));
            Assert.That(queue.NextQueueOrdinal, Is.EqualTo(3));

            first[0] = null;
            Assert.That(queue.CaptureExecutions()[0], Is.Not.Null,
                "Capture must not expose the authoritative execution list.");
            Assert.That(queue.TryCancelAt(0), Is.True);
            Assert.That(queue.TryEnqueue(
                ResourceRecipeCatalog.FieldAlloyId,
                1), Is.True);
            CraftingQueueExecutionSnapshot[] second =
                queue.CaptureExecutions();
            Assert.That(second[1].StableExecutionId,
                Is.EqualTo("craft.execution.000003"));
            Assert.That(queue.NextQueueOrdinal, Is.EqualTo(4));
        }

        [Test]
        public void PrepareIsZeroWriteAndCommitRestoresWithoutDeductingBackpack()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Stone, 9);
            var queue = new CraftingQueueModel(backpack, _ => true);
            var entries = new[]
            {
                Execution(
                    "craft.execution.000007",
                    ResourceRecipeCatalog.FieldAlloyId,
                    new ResourceAmount(ResourceIds.Iron, 4)),
            };

            Assert.That(queue.TryPrepareRestore(
                entries,
                restoredNextQueueOrdinal: 42,
                restoredActiveProgressSeconds: 6f,
                out CraftingQueueRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(queue.QueuedExecutionCount, Is.Zero);
            Assert.That(queue.NextQueueOrdinal, Is.EqualTo(1));
            Assert.That(BackpackAmount(backpack, ResourceIds.Stone),
                Is.EqualTo(9));

            Assert.That(queue.TryCommitRestore(plan, out error), Is.True,
                error);
            Assert.That(queue.QueuedExecutionCount, Is.EqualTo(1));
            Assert.That(queue.ActiveProgressSeconds, Is.EqualTo(6f));
            Assert.That(queue.NextQueueOrdinal, Is.EqualTo(42));
            Assert.That(BackpackAmount(backpack, ResourceIds.Stone),
                Is.EqualTo(9));
            Assert.That(BackpackAmount(backpack, ResourceIds.Iron), Is.Zero,
                "Restore must not reserve the same input a second time.");
        }

        [Test]
        public void UnknownRecipeRestoresPausedAndCancelsItsSavedReservation()
        {
            var backpack = new PlayerBackpackModel();
            var queue = new CraftingQueueModel(backpack, _ => true);
            var entries = new[]
            {
                Execution(
                    "craft.execution.000009",
                    "mod.crafting.missing-recipe",
                    new ResourceAmount(ResourceIds.Iron, 7)),
                Execution(
                    "craft.execution.000010",
                    ResourceRecipeCatalog.FieldAlloyId,
                    new ResourceAmount(ResourceIds.Iron, 4)),
            };

            Assert.That(queue.TryPrepareRestore(
                entries,
                50,
                3.5f,
                out CraftingQueueRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(queue.TryCommitRestore(plan, out error), Is.True,
                error);
            Assert.That(queue.BlockReason,
                Is.EqualTo(CraftingQueueBlockReason.MissingContent));

            queue.Tick(100f, globallyPaused: false);
            Assert.That(queue.ActiveProgressSeconds, Is.EqualTo(3.5f));
            Assert.That(queue.QueuedExecutionCount, Is.EqualTo(2));
            Assert.That(queue.TryCancelAt(0), Is.True);
            Assert.That(BackpackAmount(backpack, ResourceIds.Iron),
                Is.EqualTo(7));
            Assert.That(queue.ActiveRecipeId,
                Is.EqualTo(ResourceRecipeCatalog.FieldAlloyId));
            Assert.That(queue.ActiveProgressSeconds, Is.Zero);
            Assert.That(queue.BlockReason,
                Is.EqualTo(CraftingQueueBlockReason.None));
        }

        [Test]
        public void UnknownRecipeCancellationRefundsUnknownReservationIntoOrphanSlot()
        {
            const string unknownRecipeId = "mod.crafting.missing-recipe";
            const string unknownResourceId = "mod.resource.dark-matter";
            var backpack = new PlayerBackpackModel();
            var queue = new CraftingQueueModel(backpack, _ => true);
            var entries = new[]
            {
                Execution(
                    "craft.execution.000011",
                    unknownRecipeId,
                    new ResourceAmount(unknownResourceId, 7)),
            };

            Assert.That(queue.TryPrepareRestore(
                entries,
                12,
                1f,
                out CraftingQueueRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(queue.TryCommitRestore(plan, out error), Is.True,
                error);
            Assert.That(queue.TryCancelAt(0), Is.True,
                "A missing recipe must return its preserved unknown input.");
            Assert.That(queue.QueuedExecutionCount, Is.Zero);
            Assert.That(backpack.GetSlot(0).ResourceId,
                Is.EqualTo(unknownResourceId));
            Assert.That(backpack.GetSlot(0).Amount, Is.EqualTo(7));
            Assert.That(backpack.Add(unknownResourceId, 1), Is.Zero);
            Assert.That(backpack.Remove(unknownResourceId, 1), Is.Zero);
            Assert.That(backpack.GetSlot(0).Amount, Is.EqualTo(7),
                "Unknown refunded content remains an inert orphan stack.");
        }

        [Test]
        public void RestoreValidationRejectsInvalidQueueWithoutMutation()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 4);
            var queue = new CraftingQueueModel(backpack, _ => true);
            Assert.That(queue.TryEnqueue(
                ResourceRecipeCatalog.FieldAlloyId,
                1), Is.True);
            CraftingQueueExecutionSnapshot[] before =
                queue.CaptureExecutions();
            int highWaterBefore = queue.NextQueueOrdinal;

            var tooMany = new List<CraftingQueueExecutionSnapshot>();
            for (int index = 1;
                 index <= CraftingQueueModel.MaximumQueuedExecutions + 1;
                 index++)
            {
                tooMany.Add(Execution(
                    $"craft.execution.{index:000000}",
                    "mod.crafting.missing-recipe"));
            }
            AssertPrepareRejected(queue, tooMany, 30, 0f);
            AssertPrepareRejected(queue, new[]
            {
                Execution("bad.execution.000001",
                    ResourceRecipeCatalog.FieldAlloyId,
                    new ResourceAmount(ResourceIds.Iron, 4)),
            }, 2, 0f);
            AssertPrepareRejected(queue, new[]
            {
                Execution("craft.execution.000002",
                    ResourceRecipeCatalog.FieldAlloyId,
                    new ResourceAmount(ResourceIds.Iron, 4)),
                Execution("craft.execution.000002",
                    ResourceRecipeCatalog.FieldAlloyId,
                    new ResourceAmount(ResourceIds.Iron, 4)),
            }, 3, 0f);
            AssertPrepareRejected(queue, new[]
            {
                Execution("craft.execution.000002",
                    ResourceRecipeCatalog.FieldAlloyId,
                    new ResourceAmount(ResourceIds.Iron, 4)),
            }, 2, 0f);
            AssertPrepareRejected(queue, new[]
            {
                Execution("craft.execution.000002",
                    ResourceRecipeCatalog.FieldAlloyId,
                    new ResourceAmount(ResourceIds.Iron, -1)),
            }, 3, 0f);
            AssertPrepareRejected(queue,
                new CraftingQueueExecutionSnapshot[0], 3, 1f);
            AssertPrepareRejected(queue, new[]
            {
                Execution("craft.execution.000002",
                    FormalProductionDefinitionCatalog.Smelting.Id),
            }, 3, 0f);

            Assert.That(queue.NextQueueOrdinal, Is.EqualTo(highWaterBefore));
            Assert.That(queue.CaptureExecutions()[0].StableExecutionId,
                Is.EqualTo(before[0].StableExecutionId));
            Assert.That(queue.QueuedExecutionCount, Is.EqualTo(1));
            Assert.That(BackpackAmount(backpack, ResourceIds.Iron), Is.Zero);
        }

        [Test]
        public void RestoreRecomputesOutputFullWithoutCompletingTheExecution()
        {
            var backpack = new PlayerBackpackModel();
            Assert.That(backpack.Add(ResourceIds.Stone, 3000),
                Is.EqualTo(3000));
            var queue = new CraftingQueueModel(backpack, _ => true);
            var entries = new[]
            {
                Execution(
                    "craft.execution.000004",
                    ResourceRecipeCatalog.FieldAlloyId,
                    new ResourceAmount(ResourceIds.Iron, 4)),
            };

            Assert.That(queue.TryPrepareRestore(
                entries,
                8,
                12f,
                out CraftingQueueRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(queue.TryCommitRestore(plan, out error), Is.True,
                error);
            Assert.That(queue.BlockReason,
                Is.EqualTo(CraftingQueueBlockReason.OutputFull));
            Assert.That(queue.QueuedExecutionCount, Is.EqualTo(1));
            Assert.That(BackpackAmount(backpack, ResourceIds.Alloy), Is.Zero);

            backpack.Remove(ResourceIds.Stone, 100);
            queue.Tick(.1f, globallyPaused: false);
            Assert.That(queue.QueuedExecutionCount, Is.Zero);
            Assert.That(BackpackAmount(backpack, ResourceIds.Alloy),
                Is.EqualTo(1));
        }

        [Test]
        public void RestoredKnownRecipeWaitsForItsCurrentResearchRequirement()
        {
            var researchCompleted = false;
            var backpack = new PlayerBackpackModel();
            var queue = new CraftingQueueModel(
                backpack,
                _ => researchCompleted);
            var entries = new[]
            {
                Execution(
                    "craft.execution.000005",
                    ResourceRecipeCatalog.FieldAlloyId,
                    new ResourceAmount(ResourceIds.Iron, 4)),
            };

            Assert.That(queue.TryPrepareRestore(
                entries,
                9,
                2f,
                out CraftingQueueRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(queue.TryCommitRestore(plan, out error), Is.True,
                error);
            Assert.That(queue.BlockReason,
                Is.EqualTo(CraftingQueueBlockReason.ResearchRequired));
            queue.Tick(5f, globallyPaused: false);
            Assert.That(queue.ActiveProgressSeconds, Is.EqualTo(2f));

            researchCompleted = true;
            queue.Tick(5f, globallyPaused: false);
            Assert.That(queue.ActiveProgressSeconds, Is.EqualTo(7f));
            Assert.That(queue.BlockReason,
                Is.EqualTo(CraftingQueueBlockReason.None));
        }

        private static PlayerBackpackModel BackpackForTwoRecipes()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 4);
            backpack.Add(ResourceIds.Alloy, 4);
            return backpack;
        }

        private static CraftingQueueExecutionSnapshot Execution(
            string stableExecutionId,
            string recipeId,
            params ResourceAmount[] reservedInputs)
        {
            return new CraftingQueueExecutionSnapshot(
                stableExecutionId,
                recipeId,
                reservedInputs);
        }

        private static void AssertPrepareRejected(
            CraftingQueueModel queue,
            IReadOnlyList<CraftingQueueExecutionSnapshot> entries,
            int nextOrdinal,
            float progress)
        {
            Assert.That(queue.TryPrepareRestore(
                entries,
                nextOrdinal,
                progress,
                out CraftingQueueRestorePlan plan,
                out string error), Is.False);
            Assert.That(plan, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        private static void EnqueueTwoDifferentRecipes(
            CraftingQueueModel queue)
        {
            Assert.That(queue.TryEnqueue(
                "core.crafting.field-alloy",
                1), Is.True);
            Assert.That(queue.TryEnqueue(
                "core.crafting.field-ammunition",
                1), Is.True);
        }

        private static void AssertQueueSnapshotsEqual(
            CraftingQueueModel left,
            PlayerBackpackModel leftBackpack,
            CraftingQueueModel right,
            PlayerBackpackModel rightBackpack)
        {
            Assert.That(left.QueuedExecutionCount,
                Is.EqualTo(right.QueuedExecutionCount));
            Assert.That(left.ActiveProgressSeconds,
                Is.EqualTo(right.ActiveProgressSeconds).Within(.0001f));
            Assert.That(left.BlockReason, Is.EqualTo(right.BlockReason));
            foreach (string resourceId in ResourceIds.All)
            {
                Assert.That(
                    BackpackAmount(leftBackpack, resourceId),
                    Is.EqualTo(BackpackAmount(rightBackpack, resourceId)),
                    resourceId);
            }
        }

        private static void AssertRecipe(
            ResourceRecipeDefinition actual,
            string id,
            string requiredResearchId,
            string inputResourceId,
            int inputAmount,
            string outputResourceId,
            int outputAmount,
            float durationSeconds)
        {
            Assert.That(actual.Id, Is.EqualTo(id));
            Assert.That(actual.RequiredResearchId,
                Is.EqualTo(requiredResearchId));
            Assert.That(actual.DurationSeconds,
                Is.EqualTo(durationSeconds));
            Assert.That(actual.Inputs, Has.Count.EqualTo(1));
            Assert.That(actual.Inputs[0].ResourceId,
                Is.EqualTo(inputResourceId));
            Assert.That(actual.Inputs[0].Amount, Is.EqualTo(inputAmount));
            Assert.That(actual.Outputs, Has.Count.EqualTo(1));
            Assert.That(actual.Outputs[0].ResourceId,
                Is.EqualTo(outputResourceId));
            Assert.That(actual.Outputs[0].Amount, Is.EqualTo(outputAmount));
        }

        private static int BackpackAmount(
            PlayerBackpackModel backpack,
            string resourceId)
        {
            int total = 0;
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
            return total;
        }
    }
}
