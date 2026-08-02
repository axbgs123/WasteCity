using NUnit.Framework;
using WasteCity.Persistence;
using WasteCity.Legacy;
using WasteCity.Economy;
namespace WasteCity.Tests
{
    public sealed class FormalSaveTests
    {
        [Test] public void SaveCodecRoundTrips(){var d=FormalSaveCodec.Decode(FormalSaveCodec.Encode(new FormalSaveData{worldSeed=42,cityX=3.5f,iron=19,alloy=12,ammunition=7,population=180,observation=44,legacyPathId="core.legacy.void-debt",buildings=new[]{new WasteCity.Building.BuildingSnapshot{definitionId="core.building.housing",x=2,y=3,health=200,constructionRemaining=1.5f}}}));Assert.That(d.worldSeed,Is.EqualTo(42));Assert.That(d.cityX,Is.EqualTo(3.5f));Assert.That(d.alloy,Is.EqualTo(12));Assert.That(d.ammunition,Is.EqualTo(7));Assert.That(d.population,Is.EqualTo(180));Assert.That(d.observation,Is.EqualTo(44));Assert.That(d.buildings.Length,Is.EqualTo(1));Assert.That(d.buildings[0].definitionId,Is.EqualTo("core.building.housing"));Assert.That(d.legacyPathId,Is.EqualTo("core.legacy.void-debt"));}
        [Test] public void VersionOneSaveRemainsReadable(){var d=FormalSaveCodec.Decode("{\"schema\":1,\"worldSeed\":8128,\"iron\":5}");Assert.That(d,Is.Not.Null);Assert.That(d.schema,Is.EqualTo(1));Assert.That(d.iron,Is.EqualTo(5));}
        [Test] public void EnemyQualityRoundTrips(){var data=new FormalSaveData{enemies=new[]{new WasteCity.Combat.EnemySnapshot{archetype=1,quality=(int)WasteCity.Combat.EnemyQuality.Epic,health=321}}};var restored=FormalSaveCodec.Decode(FormalSaveCodec.Encode(data));Assert.That(restored.enemies[0].quality,Is.EqualTo((int)WasteCity.Combat.EnemyQuality.Epic));Assert.That(restored.enemies[0].health,Is.EqualTo(321));}
        [Test] public void RouteResourcesAndProductionProgressRoundTrip(){var restored=FormalSaveCodec.Decode(FormalSaveCodec.Encode(new FormalSaveData{spiritIron=7,flyingSword=3,boneSteel=9,biomassConcentrate=4,biologicalWeapon=2,resonanceMetal=8,psionicAmplifier=5,productionProgress=new[]{1f,2f,3f}}));Assert.That(restored.spiritIron,Is.EqualTo(7));Assert.That(restored.biologicalWeapon,Is.EqualTo(2));Assert.That(restored.psionicAmplifier,Is.EqualTo(5));Assert.That(restored.productionProgress,Is.EqualTo(new[]{1f,2f,3f}));}
        [Test] public void RewindKeepsCurrentAttentionAndAddsThree(){Assert.That(RewindAnchorRules.AttentionAfterLoad(42f),Is.EqualTo(45f));Assert.That(RewindAnchorRules.AttentionAfterLoad(99f),Is.EqualTo(100f));}
        [Test] public void DebtResourceCanBeRestoredNegative(){var i=new ResourceInventory(150);i.SetDebtLimit(100);i.Restore(ResourceIds.Iron,-40);Assert.That(i.Get(ResourceIds.Iron),Is.EqualTo(-40));}
        [Test] public void CorruptSaveReturnsNull()=>Assert.That(FormalSaveCodec.Decode("{broken"),Is.Null);
    }
}
