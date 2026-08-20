using System;
using System.Reflection;
using NUnit.Framework;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.Persistence.ThreeD;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalSaveEconomyTests
    {
        private const string AdapterTypeName =
            "WasteCity.Graybox3D.Building.GrayboxEconomySaveAdapter3D, " +
            "WasteCity.Graybox3D.Building";
        private const string UnknownResourceId = "mod.resource.dark-matter";
        private const string UnknownRecipeId = "mod.crafting.lost-recipe";
        private const string UnknownCompletedResearchId =
            "mod.research.lost-completed";
        private const string UnknownActiveResearchId =
            "mod.research.lost-active";

        [Test]
        public void CaptureBackpackWritesAllThirtyOrderedSlotsAndDeepCopies()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 101);
            object adapter = CreateAdapter(
                backpack,
                new CraftingQueueModel(backpack, _ => true),
                new DemoResearchRuntime(new ResearchModel()));

            FormalThreeDBackpackSaveData saved = Invoke<
                FormalThreeDBackpackSaveData>(adapter, "CaptureBackpack");

            Assert.That(saved.slots, Has.Length.EqualTo(30));
            for (var index = 0; index < saved.slots.Length; index++)
                Assert.That(saved.slots[index].slotIndex, Is.EqualTo(index));
            AssertSlot(saved, 0, ResourceIds.Iron, 100);
            AssertSlot(saved, 1, ResourceIds.Iron, 1);
            AssertSlot(saved, 2, null, 0);

            saved.slots[0].amount = 0;
            FormalThreeDBackpackSaveData recaptured = Invoke<
                FormalThreeDBackpackSaveData>(adapter, "CaptureBackpack");
            AssertSlot(recaptured, 0, ResourceIds.Iron, 100);
        }

        [Test]
        public void RestorePreservesUnknownBackpackSlotAndUsesExplicitOverstackPolicy()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 7);
            object adapter = CreateAdapter(
                backpack,
                new CraftingQueueModel(backpack, _ => true),
                new DemoResearchRuntime(new ResearchModel()));
            FormalThreeDBackpackSaveData saved = EmptyBackpack();
            saved.slots[5].resourceId = UnknownResourceId;
            saved.slots[5].amount = 13;

            Assert.That(TryRestore(
                adapter,
                saved,
                EmptyCrafting(),
                RootResearch(),
                allowBackpackOverStack: false,
                out string error), Is.True, error);
            Assert.That(backpack.GetSlot(5).ResourceId,
                Is.EqualTo(UnknownResourceId));
            Assert.That(backpack.GetSlot(5).Amount, Is.EqualTo(13));
            Assert.That(backpack.Remove(UnknownResourceId, 1), Is.Zero);

            saved.slots[5].resourceId = ResourceIds.Iron;
            saved.slots[5].amount = 101;
            Assert.That(TryRestore(
                adapter,
                saved,
                EmptyCrafting(),
                RootResearch(),
                allowBackpackOverStack: false,
                out _), Is.False);
            Assert.That(backpack.GetSlot(5).ResourceId,
                Is.EqualTo(UnknownResourceId));
            Assert.That(TryRestore(
                adapter,
                saved,
                EmptyCrafting(),
                RootResearch(),
                allowBackpackOverStack: true,
                out error), Is.True, error);
            Assert.That(backpack.GetSlot(5).Amount, Is.EqualTo(101));
        }

        [Test]
        public void CraftingRoundTripPreservesReservationsProgressAndHighWaterWithoutSecondSpend()
        {
            var sourceBackpack = new PlayerBackpackModel();
            sourceBackpack.Add(ResourceIds.Iron, 8);
            var sourceQueue = new CraftingQueueModel(
                sourceBackpack,
                _ => true);
            Assert.That(sourceQueue.TryEnqueue(
                ResourceRecipeCatalog.FieldAlloyId,
                2), Is.True);
            sourceQueue.Tick(3f, globallyPaused: false);
            object source = CreateAdapter(
                sourceBackpack,
                sourceQueue,
                new DemoResearchRuntime(new ResearchModel()));
            FormalThreeDCraftingSaveData crafting = Invoke<
                FormalThreeDCraftingSaveData>(source, "CaptureCrafting");

            Assert.That(crafting.executions, Has.Length.EqualTo(2));
            Assert.That(crafting.executions[0].stableExecutionId,
                Is.EqualTo("craft.execution.000001"));
            Assert.That(crafting.executions[1].stableExecutionId,
                Is.EqualTo("craft.execution.000002"));
            Assert.That(crafting.nextQueueOrdinal, Is.EqualTo(3));
            Assert.That(crafting.activeProgressSeconds, Is.EqualTo(3f));
            AssertReservedIron(crafting.executions[0], 4);
            AssertReservedIron(crafting.executions[1], 4);

            var restoredBackpack = new PlayerBackpackModel();
            var restoredQueue = new CraftingQueueModel(
                restoredBackpack,
                _ => true);
            object restored = CreateAdapter(
                restoredBackpack,
                restoredQueue,
                new DemoResearchRuntime(new ResearchModel()));
            Assert.That(TryRestore(
                restored,
                Invoke<FormalThreeDBackpackSaveData>(
                    source,
                    "CaptureBackpack"),
                crafting,
                RootResearch(),
                allowBackpackOverStack: false,
                out string error), Is.True, error);
            Assert.That(BackpackAmount(restoredBackpack, ResourceIds.Iron),
                Is.Zero);
            restoredQueue.Tick(9f, globallyPaused: false);
            Assert.That(restoredQueue.QueuedExecutionCount, Is.EqualTo(1));
            Assert.That(BackpackAmount(restoredBackpack, ResourceIds.Alloy),
                Is.EqualTo(1));
            Assert.That(Invoke<FormalThreeDCraftingSaveData>(
                restored,
                "CaptureCrafting").nextQueueOrdinal, Is.EqualTo(3));
        }

        [Test]
        public void UnknownRecipePausesInPlaceAndCancelsUsingSavedReservation()
        {
            var backpack = new PlayerBackpackModel();
            var queue = new CraftingQueueModel(backpack, _ => true);
            object adapter = CreateAdapter(
                backpack,
                queue,
                new DemoResearchRuntime(new ResearchModel()));
            FormalThreeDCraftingSaveData crafting = EmptyCrafting();
            crafting.nextQueueOrdinal = 8;
            crafting.activeProgressSeconds = 2f;
            crafting.executions = new[]
            {
                new FormalThreeDCraftingExecutionSaveData
                {
                    stableExecutionId = "craft.execution.000007",
                    recipeId = UnknownRecipeId,
                    reservedInputs = new[]
                    {
                        Amount(ResourceIds.Iron, 4),
                    },
                },
            };

            Assert.That(TryRestore(
                adapter,
                EmptyBackpack(),
                crafting,
                RootResearch(),
                allowBackpackOverStack: false,
                out string error), Is.True, error);
            Assert.That(queue.BlockReason.ToString(),
                Is.EqualTo("MissingContent"));
            queue.Tick(100f, globallyPaused: false);
            Assert.That(queue.QueuedExecutionCount, Is.EqualTo(1));
            Assert.That(queue.ActiveProgressSeconds, Is.EqualTo(2f));
            FormalThreeDCraftingSaveData recaptured = Invoke<
                FormalThreeDCraftingSaveData>(adapter, "CaptureCrafting");
            Assert.That(recaptured.executions[0].recipeId,
                Is.EqualTo(UnknownRecipeId));
            Assert.That(recaptured.nextQueueOrdinal, Is.EqualTo(8));
            Assert.That(queue.TryCancelAt(0), Is.True);
            Assert.That(BackpackAmount(backpack, ResourceIds.Iron),
                Is.EqualTo(4));
        }

        [Test]
        public void ResearchRestoreUsesDemoDefinitionAndNeverSpendsThePaidCostAgain()
        {
            var backpack = new PlayerBackpackModel();
            var model = new ResearchModel();
            var runtime = new DemoResearchRuntime(model);
            object adapter = CreateAdapter(
                backpack,
                new CraftingQueueModel(backpack, runtime.IsCompleted),
                runtime);
            var city = new ResourceInventory(100);
            city.Add(ResourceIds.Iron, 10);
            Assert.That(runtime.TryStart(
                DemoResearchCatalog.BasicMetallurgyId,
                city,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(runtime.Tick(
                5f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.False);
            FormalThreeDResearchSaveData saved = Invoke<
                FormalThreeDResearchSaveData>(adapter, "CaptureResearch");

            var restoredBackpack = new PlayerBackpackModel();
            var restoredModel = new ResearchModel();
            var restoredRuntime = new DemoResearchRuntime(restoredModel);
            object restored = CreateAdapter(
                restoredBackpack,
                new CraftingQueueModel(
                    restoredBackpack,
                    restoredRuntime.IsCompleted),
                restoredRuntime);
            Assert.That(TryRestore(
                restored,
                EmptyBackpack(),
                EmptyCrafting(),
                saved,
                allowBackpackOverStack: false,
                out string error), Is.True, error);
            Assert.That(restoredModel.Active,
                Is.SameAs(DemoResearchCatalog.Find(
                    DemoResearchCatalog.BasicMetallurgyId)));
            Assert.That(restoredModel.Remaining, Is.EqualTo(15f));
            Assert.That(city.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(restoredRuntime.Tick(
                15f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.True);
        }

        [Test]
        public void UnknownResearchContentRoundTripsWithoutEffectAndPausesActiveItem()
        {
            var backpack = new PlayerBackpackModel();
            var runtime = new DemoResearchRuntime(new ResearchModel());
            object adapter = CreateAdapter(
                backpack,
                new CraftingQueueModel(backpack, runtime.IsCompleted),
                runtime);
            var research = new FormalThreeDResearchSaveData
            {
                completedResearchIds = new[]
                {
                    UnknownCompletedResearchId,
                    DemoResearchCatalog.ScrapProcessingId,
                },
                activeResearchId = UnknownActiveResearchId,
                remainingSeconds = 17f,
            };

            Assert.That(TryRestore(
                adapter,
                EmptyBackpack(),
                EmptyCrafting(),
                research,
                allowBackpackOverStack: false,
                out string error), Is.True, error);
            Assert.That(runtime.IsCompleted(UnknownCompletedResearchId),
                Is.False);
            Assert.That(ReadProperty<bool>(
                runtime,
                "HasMissingActiveResearch"), Is.True);
            Assert.That(ReadProperty<string>(
                runtime,
                "MissingActiveResearchId"),
                Is.EqualTo(UnknownActiveResearchId));
            Assert.That(ReadProperty<float>(
                runtime,
                "MissingActiveRemainingSeconds"), Is.EqualTo(17f));
            var city = new ResourceInventory(100);
            city.Add(ResourceIds.Iron, 10);
            Assert.That(runtime.TryStart(
                DemoResearchCatalog.BasicMetallurgyId,
                city,
                hasEligibleResearchStation: true), Is.False);
            Assert.That(city.Get(ResourceIds.Iron), Is.EqualTo(10));
            FormalThreeDResearchSaveData recaptured = Invoke<
                FormalThreeDResearchSaveData>(adapter, "CaptureResearch");
            Assert.That(recaptured.completedResearchIds,
                Is.EqualTo(new[]
                {
                    DemoResearchCatalog.ScrapProcessingId,
                    UnknownCompletedResearchId,
                }));
            Assert.That(recaptured.activeResearchId,
                Is.EqualTo(UnknownActiveResearchId));
            Assert.That(recaptured.remainingSeconds, Is.EqualTo(17f));
        }

        [Test]
        public void InvalidLateCraftingDomainLeavesAllThreeOwnersUnchanged()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 7);
            var runtime = new DemoResearchRuntime(new ResearchModel());
            var queue = new CraftingQueueModel(backpack, runtime.IsCompleted);
            object adapter = CreateAdapter(backpack, queue, runtime);
            FormalThreeDCraftingSaveData invalid = EmptyCrafting();
            invalid.activeProgressSeconds = 1f;

            Assert.That(TryRestore(
                adapter,
                EmptyBackpack(),
                invalid,
                new FormalThreeDResearchSaveData
                {
                    completedResearchIds = new[]
                    {
                        DemoResearchCatalog.ScrapProcessingId,
                        DemoResearchCatalog.BasicMetallurgyId,
                    },
                    activeResearchId = null,
                    remainingSeconds = 0f,
                },
                allowBackpackOverStack: false,
                out _), Is.False);
            Assert.That(BackpackAmount(backpack, ResourceIds.Iron),
                Is.EqualTo(7));
            Assert.That(queue.QueuedExecutionCount, Is.Zero);
            Assert.That(runtime.IsCompleted(
                DemoResearchCatalog.BasicMetallurgyId), Is.False);
        }

        private static object CreateAdapter(
            PlayerBackpackModel backpack,
            CraftingQueueModel crafting,
            DemoResearchRuntime research)
        {
            Type type = Type.GetType(AdapterTypeName, throwOnError: false);
            Assert.That(type, Is.Not.Null,
                "Task 6 requires the formal 3D economy save adapter.");
            return Activator.CreateInstance(type, backpack, crafting, research);
        }

        private static T Invoke<T>(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName);
            Assert.That(method, Is.Not.Null, methodName);
            return (T)method.Invoke(target, null);
        }

        private static bool TryRestore(
            object adapter,
            FormalThreeDBackpackSaveData backpack,
            FormalThreeDCraftingSaveData crafting,
            FormalThreeDResearchSaveData research,
            bool allowBackpackOverStack,
            out string error)
        {
            MethodInfo method = adapter.GetType().GetMethod("TryRestore");
            Assert.That(method, Is.Not.Null);
            object[] arguments =
            {
                backpack,
                crafting,
                research,
                allowBackpackOverStack,
                null,
            };
            bool result = (bool)method.Invoke(adapter, arguments);
            error = arguments[4] as string;
            return result;
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(target);
        }

        private static FormalThreeDBackpackSaveData EmptyBackpack()
        {
            var slots = new FormalThreeDBackpackSlotSaveData[30];
            for (var index = 0; index < slots.Length; index++)
                slots[index] = new FormalThreeDBackpackSlotSaveData
                {
                    slotIndex = index,
                };
            return new FormalThreeDBackpackSaveData { slots = slots };
        }

        private static FormalThreeDCraftingSaveData EmptyCrafting()
        {
            return new FormalThreeDCraftingSaveData
            {
                nextQueueOrdinal = 1,
                activeProgressSeconds = 0f,
                executions = Array.Empty<
                    FormalThreeDCraftingExecutionSaveData>(),
            };
        }

        private static FormalThreeDResearchSaveData RootResearch()
        {
            return new FormalThreeDResearchSaveData
            {
                completedResearchIds = new[]
                {
                    DemoResearchCatalog.ScrapProcessingId,
                },
                activeResearchId = null,
                remainingSeconds = 0f,
            };
        }

        private static FormalThreeDResourceAmountSaveData Amount(
            string resourceId,
            int amount)
        {
            return new FormalThreeDResourceAmountSaveData
            {
                resourceId = resourceId,
                amount = amount,
            };
        }

        private static void AssertSlot(
            FormalThreeDBackpackSaveData data,
            int index,
            string resourceId,
            int amount)
        {
            Assert.That(data.slots[index].resourceId, Is.EqualTo(resourceId));
            Assert.That(data.slots[index].amount, Is.EqualTo(amount));
        }

        private static void AssertReservedIron(
            FormalThreeDCraftingExecutionSaveData execution,
            int amount)
        {
            Assert.That(execution.reservedInputs, Has.Length.EqualTo(1));
            Assert.That(execution.reservedInputs[0].resourceId,
                Is.EqualTo(ResourceIds.Iron));
            Assert.That(execution.reservedInputs[0].amount,
                Is.EqualTo(amount));
        }

        private static int BackpackAmount(
            PlayerBackpackModel backpack,
            string resourceId)
        {
            var total = 0;
            for (var index = 0; index < backpack.SlotCount; index++)
            {
                BackpackSlot slot = backpack.GetSlot(index);
                if (string.Equals(
                        slot.ResourceId,
                        resourceId,
                        StringComparison.Ordinal))
                    total += slot.Amount;
            }
            return total;
        }
    }
}
