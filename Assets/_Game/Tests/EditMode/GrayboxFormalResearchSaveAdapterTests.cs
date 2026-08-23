using System;
using NUnit.Framework;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence.ThreeD;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalResearchSaveAdapterTests
    {
        private const string Root = "core.research.scrap-processing";
        private const string SpiritSensing =
            "core.research.spirit-sensing";
        private const string AdaptiveTissue =
            "core.research.adaptive-tissue";
        private const string MindResonance =
            "core.research.mind-resonance";

        [Test]
        public void SchemaThirtyOneRoundTripPreservesFormalCompletedActiveAndProgress()
        {
            var sourceBackpack = new PlayerBackpackModel();
            var sourceRuntime = new FormalResearchRuntime(
                new ResearchModel());
            var sourceQueue = new CraftingQueueModel(
                sourceBackpack,
                sourceRuntime.IsCompleted);
            var source = new GrayboxEconomySaveAdapter3D(
                sourceBackpack,
                sourceQueue,
                sourceRuntime);
            var schemaThirtyOne = new FormalThreeDResearchSaveData
            {
                completedResearchIds = new[] { Root, SpiritSensing },
                activeResearchId = AdaptiveTissue,
                remainingSeconds = 9.5f,
            };

            Assert.That(source.TryRestore(
                EmptyBackpack(),
                EmptyCrafting(),
                schemaThirtyOne,
                allowBackpackOverStack: false,
                out string sourceError), Is.True, sourceError);
            FormalThreeDResearchSaveData captured =
                source.CaptureResearch();

            var restoredBackpack = new PlayerBackpackModel();
            var restoredModel = new ResearchModel();
            var restoredRuntime = new FormalResearchRuntime(restoredModel);
            var restoredQueue = new CraftingQueueModel(
                restoredBackpack,
                restoredRuntime.IsCompleted);
            var restored = new GrayboxEconomySaveAdapter3D(
                restoredBackpack,
                restoredQueue,
                restoredRuntime);
            Assert.That(restored.TryRestore(
                EmptyBackpack(),
                EmptyCrafting(),
                captured,
                allowBackpackOverStack: false,
                out string restoredError), Is.True, restoredError);

            Assert.That(restoredRuntime.IsCompleted(SpiritSensing), Is.True);
            Assert.That(restoredModel.Active,
                Is.SameAs(ResearchCatalog.Find(AdaptiveTissue)));
            Assert.That(restoredModel.Remaining, Is.EqualTo(9.5f));
            Assert.That(restoredRuntime.Tick(
                9.5f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(restoredRuntime.IsCompleted(AdaptiveTissue), Is.True);
        }

        [Test]
        public void UnknownActiveStaysFrozenAndRestoreRepairsMissingRoot()
        {
            const string unknownCompleted =
                "mod.example.completed-research";
            const string unknownActive = "mod.example.active-research";
            var backpack = new PlayerBackpackModel();
            var model = new ResearchModel();
            var runtime = new FormalResearchRuntime(model);
            var queue = new CraftingQueueModel(
                backpack,
                runtime.IsCompleted);
            var adapter = new GrayboxEconomySaveAdapter3D(
                backpack,
                queue,
                runtime);
            var schemaThirtyOne = new FormalThreeDResearchSaveData
            {
                completedResearchIds = new[] { unknownCompleted },
                activeResearchId = unknownActive,
                remainingSeconds = 17f,
            };

            Assert.That(adapter.TryRestore(
                EmptyBackpack(),
                EmptyCrafting(),
                schemaThirtyOne,
                allowBackpackOverStack: false,
                out string error), Is.True, error);

            Assert.That(runtime.IsCompleted(Root), Is.True,
                "Schema 31 restore must repair the initially completed root.");
            Assert.That(runtime.IsCompleted(unknownCompleted), Is.False,
                "Unknown completed content must remain inert.");
            Assert.That(runtime.HasMissingActiveResearch, Is.True);
            Assert.That(runtime.MissingActiveResearchId,
                Is.EqualTo(unknownActive));
            Assert.That(runtime.MissingActiveRemainingSeconds,
                Is.EqualTo(17f));
            Assert.That(model.Active, Is.Null);
            Assert.That(runtime.Tick(
                100f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.False);

            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.EnergyCrystal, 8);
            inventory.Add(ResourceIds.Water, 6);
            Assert.That(runtime.TryStart(
                MindResonance,
                inventory,
                hasEligibleResearchStation: true), Is.False);
            Assert.That(runtime.TryCancel(
                inventory,
                new ResourceCapacityPolicy(),
                activeWarehouseCount: 0), Is.False);
            Assert.That(inventory.Get(ResourceIds.EnergyCrystal),
                Is.EqualTo(8));
            Assert.That(inventory.Get(ResourceIds.Water), Is.EqualTo(6));

            FormalThreeDResearchSaveData recaptured =
                adapter.CaptureResearch();
            Assert.That(recaptured.completedResearchIds, Is.EqualTo(new[]
            {
                Root,
                unknownCompleted,
            }));
            Assert.That(recaptured.activeResearchId,
                Is.EqualTo(unknownActive));
            Assert.That(recaptured.remainingSeconds, Is.EqualTo(17f));
        }

        private static FormalThreeDBackpackSaveData EmptyBackpack()
        {
            var slots = new FormalThreeDBackpackSlotSaveData[30];
            for (var index = 0; index < slots.Length; index++)
            {
                slots[index] = new FormalThreeDBackpackSlotSaveData
                {
                    slotIndex = index,
                };
            }
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
    }
}
