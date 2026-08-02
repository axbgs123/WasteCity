using NUnit.Framework;
using WasteCity.Building;

namespace WasteCity.Tests
{
    public sealed class BuildingUnlockTests
    {
        [Test] public void ResearchStationRequiresTwoHundredPopulation(){var d=BuildingCatalog.All[4];Assert.That(BuildingUnlockModel.IsUnlocked(d,199,_=>false,_=>0,out string reason),Is.False);Assert.That(reason,Does.Contain("200"));Assert.That(BuildingUnlockModel.IsUnlocked(d,200,_=>false,_=>0,out _),Is.True);}
        [Test] public void SmelterRequiresTechnologyResearch(){var d=BuildingCatalog.All[5];Assert.That(BuildingUnlockModel.IsUnlocked(d,200,_=>false,_=>0,out _),Is.False);Assert.That(BuildingUnlockModel.IsUnlocked(d,200,id=>id=="core.research.automated-machinery",_=>0,out _),Is.True);}
        [Test] public void AssemblerAndTurretRequireCompletedPredecessor(){Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.All[6],200,_=>true,id=>id=="core.building.smelter"?1:0,out _),Is.True);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.All[7],200,_=>true,_=>0,out _),Is.False);}
        [Test] public void RouteWorkshopRequiresResearchAndRouteFurnace(){Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.ArtifactWorkshop,200,id=>id=="core.research.artifact-crafting",_=>0,out _),Is.False);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.ArtifactWorkshop,200,id=>id=="core.research.artifact-crafting",id=>id==BuildingCatalog.SpiritFireFurnace.Id.Value?1:0,out _),Is.True);}
        [Test] public void RouteTowerRequiresItsWorkshop(){Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.MindSpire,200,_=>true,id=>id==BuildingCatalog.PsionicWorkshop.Id.Value?1:0,out _),Is.True);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.MindSpire,200,_=>true,_=>0,out _),Is.False);}
        [Test] public void TierThreeCombatBuildingsRequireResearchAndPredecessor(){Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.LaserTower,200,id=>id=="core.research.energy-weapons",id=>id==BuildingCatalog.Assembler.Id.Value?1:0,out _),Is.True);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.AcidTower,200,_=>false,_=>1,out _),Is.False);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.ShieldGenerator,200,id=>id=="core.research.mind-shield",id=>id==BuildingCatalog.PsionicWorkshop.Id.Value?1:0,out _),Is.True);}
        [Test] public void SpiritGatheringArrayRequiresItsResearch(){Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.SpiritGatheringArray,200,id=>id=="core.research.spirit-gathering",_=>0,out _),Is.True);Assert.That(BuildingUnlockModel.IsUnlocked(BuildingCatalog.SpiritGatheringArray,200,_=>false,_=>0,out _),Is.False);}
    }
}
