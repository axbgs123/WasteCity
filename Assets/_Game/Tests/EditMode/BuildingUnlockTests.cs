using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;

namespace WasteCity.Tests
{
    public sealed class BuildingUnlockTests
    {
        [Test]
        public void ResearchStationHasNoPopulationRequirement()
        {
            BuildingDefinition definition = BuildingCatalog.ResearchStation;

            Assert.That(definition.MinimumPopulation, Is.Zero);
            Assert.That(
                BuildingUnlockModel.IsUnlocked(
                    definition,
                    0,
                    _ => false,
                    _ => 0,
                    out _),
                Is.True);
        }
        [Test] public void SmelterRequiresTechnologyResearch(){var d=BuildingCatalog.All[5];Assert.That(BuildingUnlockModel.IsUnlocked(d,200,_=>false,_=>0,out _),Is.False);Assert.That(BuildingUnlockModel.IsUnlocked(d,200,id=>id=="core.research.automated-machinery",_=>0,out _),Is.True);}
        [Test] public void AssemblerAndTurretRequireCompletedPredecessor(){Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.All[6],200,_=>true,id=>id=="core.building.smelter"?1:0,out _),Is.True);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.All[7],200,_=>true,_=>0,out _),Is.False);}
        [Test] public void RouteWorkshopRequiresResearchAndRouteFurnace(){Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.ArtifactWorkshop,200,id=>id=="core.research.artifact-crafting",_=>0,out _),Is.False);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.ArtifactWorkshop,200,id=>id=="core.research.artifact-crafting",id=>id==BuildingCatalog.SpiritFireFurnace.Id.Value?1:0,out _),Is.True);}
        [Test] public void RouteTowerRequiresItsWorkshop(){Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.MindSpire,200,_=>true,id=>id==BuildingCatalog.PsionicWorkshop.Id.Value?1:0,out _),Is.True);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.MindSpire,200,_=>true,_=>0,out _),Is.False);}
        [Test] public void TierThreeCombatBuildingsRequireResearchAndPredecessor(){Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.LaserTower,200,id=>id=="core.research.energy-weapons",id=>id==BuildingCatalog.Assembler.Id.Value?1:0,out _),Is.True);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.AcidTower,200,_=>false,_=>1,out _),Is.False);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.ShieldGenerator,200,id=>id=="core.research.mind-shield",id=>id==BuildingCatalog.PsionicWorkshop.Id.Value?1:0,out _),Is.True);}
        [Test] public void SpiritGatheringArrayRequiresPopulationAndResearch(){Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.SpiritGatheringArray,999,id=>id=="core.research.spirit-gathering",_=>0,out _),Is.False);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.SpiritGatheringArray,1000,id=>id=="core.research.spirit-gathering",_=>0,out _),Is.True);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.SpiritGatheringArray,1000,_=>false,_=>0,out _),Is.False);}
        [Test] public void AutomatedRepairBayRequiresUnmannedSystemsAndAssembler(){Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.AutomatedRepairBay,200,id=>id=="core.research.unmanned-systems",id=>id==BuildingCatalog.Assembler.Id.Value?1:0,out _),Is.True);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.AutomatedRepairBay,200,_=>true,_=>0,out _),Is.False);}
        [Test] public void AlchemyChamberRequiresAlchemyAndArtifactWorkshop(){Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.AlchemyChamber,200,id=>id=="core.research.alchemy",id=>id==BuildingCatalog.ArtifactWorkshop.Id.Value?1:0,out _),Is.True);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.AlchemyChamber,200,_=>true,_=>0,out _),Is.False);}
        [Test] public void PuppetWorkshopRequiresPuppetryAndArtifactWorkshop(){Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.PuppetWorkshop,200,id=>id=="core.research.puppetry",id=>id==BuildingCatalog.ArtifactWorkshop.Id.Value?1:0,out _),Is.True);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.PuppetWorkshop,200,_=>true,_=>0,out _),Is.False);}
        [Test] public void BehemothPenRequiresBreedingResearchAndChamber(){Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.BehemothPen,200,id=>id=="core.research.behemoth-breeding",id=>id==BuildingCatalog.BreedingChamber.Id.Value?1:0,out _),Is.True);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.BehemothPen,200,_=>true,_=>0,out _),Is.False);}

        [Test]
        public void WorkspaceApi_ReusesPrivateBuffersUntilItsNextEvaluation()
        {
            Type workspaceType = typeof(BuildingUnlockModel).Assembly.GetType(
                "WasteCity.Building.BuildingUnlockEvaluationWorkspace");
            Assert.That(workspaceType, Is.Not.Null);
            Assert.That(workspaceType.IsPublic, Is.True);
            Assert.That(workspaceType.GetConstructor(Type.EmptyTypes), Is.Not.Null);
            MethodInfo evaluate = typeof(BuildingUnlockModel).GetMethod(
                "Evaluate",
                new[]
                {
                    typeof(BuildingDefinition),
                    typeof(int),
                    typeof(Func<string, bool>),
                    typeof(Func<string, int>),
                    workspaceType
                });
            Assert.That(evaluate, Is.Not.Null);

            var definition = new BuildingDefinition(
                "test.building.workspace-unlock",
                "Workspace Unlock",
                1,
                1,
                WasteCity.Economy.ResourceIds.Alloy,
                1,
                minimumPopulation: 10,
                requiredResearchId: "test.research.workspace",
                requiredBuildingId: "test.building.required");
            object workspace = Activator.CreateInstance(workspaceType);
            object otherWorkspace = Activator.CreateInstance(workspaceType);
            var first = (BuildingUnlockEvaluation)evaluate.Invoke(
                null,
                new object[]
                {
                    definition,
                    0,
                    new Func<string, bool>(_ => false),
                    new Func<string, int>(_ => 0),
                    workspace
                });
            Assert.That(
                first.Failures,
                Is.EqualTo(new[]
                {
                    BuildingUnlockFailure.Population,
                    BuildingUnlockFailure.Research,
                    BuildingUnlockFailure.RequiredBuilding
                }));
            Assert.That(first.Reasons, Has.Count.EqualTo(3));

            evaluate.Invoke(
                null,
                new object[]
                {
                    definition,
                    20,
                    new Func<string, bool>(_ => true),
                    new Func<string, int>(_ => 1),
                    otherWorkspace
                });
            Assert.That(first.Failures, Has.Count.EqualTo(3));
            Assert.That(first.Reasons, Has.Count.EqualTo(3));

            evaluate.Invoke(
                null,
                new object[]
                {
                    definition,
                    20,
                    new Func<string, bool>(_ => true),
                    new Func<string, int>(_ => 1),
                    workspace
                });
            Assert.That(first.Failures, Is.Empty);
            Assert.That(first.Reasons, Is.Empty);

            AssertNoPublicMutableBufferOrReset(workspaceType);
            AssertNoStaticMutableBuffer(workspaceType);
        }

        [Test]
        public void LegacyEvaluate_ReturnsIndependentImmutableSnapshots()
        {
            var definition = new BuildingDefinition(
                "test.building.legacy-unlock-snapshot",
                "Legacy Unlock Snapshot",
                1,
                1,
                WasteCity.Economy.ResourceIds.Alloy,
                1,
                minimumPopulation: 10);
            BuildingUnlockEvaluation locked =
                BuildingUnlockModel.Evaluate(
                    definition,
                    0,
                    _ => true,
                    _ => 1);
            BuildingUnlockEvaluation unlocked =
                BuildingUnlockModel.Evaluate(
                    definition,
                    20,
                    _ => true,
                    _ => 1);

            Assert.That(
                locked.Failures,
                Is.EqualTo(new[] { BuildingUnlockFailure.Population }));
            Assert.That(locked.Reasons, Has.Count.EqualTo(1));
            Assert.That(unlocked.Failures, Is.Empty);
            Assert.That(unlocked.Reasons, Is.Empty);
            Assert.That(locked.Failures, Is.Not.SameAs(unlocked.Failures));
            Assert.That(locked.Reasons, Is.Not.SameAs(unlocked.Reasons));
        }

        private static void AssertNoPublicMutableBufferOrReset(Type type)
        {
            Assert.That(
                type.GetFields(BindingFlags.Public | BindingFlags.Instance),
                Is.Empty);
            Assert.That(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(property =>
                        property.PropertyType.IsArray ||
                        typeof(IList).IsAssignableFrom(property.PropertyType)),
                Is.Empty);
            Assert.That(
                type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(method =>
                        method.Name == "Clear" ||
                        method.Name == "Reset"),
                Is.Empty);
        }

        private static void AssertNoStaticMutableBuffer(Type type)
        {
            Assert.That(
                type.GetFields(
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic)
                    .Where(field =>
                        field.FieldType.IsArray ||
                        typeof(IList).IsAssignableFrom(field.FieldType)),
                Is.Empty);
        }
    }
}
