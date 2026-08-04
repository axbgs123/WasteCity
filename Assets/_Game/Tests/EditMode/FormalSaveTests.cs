using NUnit.Framework;
using WasteCity.Persistence;
using WasteCity.Legacy;
using WasteCity.Economy;
using WasteCity.Combat;
namespace WasteCity.Tests
{
    public sealed class FormalSaveTests
    {
        [Test] public void SaveCodecRoundTrips(){var d=FormalSaveCodec.Decode(FormalSaveCodec.Encode(new FormalSaveData{worldSeed=42,cityX=3.5f,iron=19,alloy=12,ammunition=7,population=180,observation=44,legacyPathId="core.legacy.void-debt",buildings=new[]{new WasteCity.Building.BuildingSnapshot{definitionId="core.building.housing",x=2,y=3,health=200,constructionRemaining=1.5f}}}));Assert.That(d.worldSeed,Is.EqualTo(42));Assert.That(d.cityX,Is.EqualTo(3.5f));Assert.That(d.alloy,Is.EqualTo(12));Assert.That(d.ammunition,Is.EqualTo(7));Assert.That(d.population,Is.EqualTo(180));Assert.That(d.observation,Is.EqualTo(44));Assert.That(d.buildings.Length,Is.EqualTo(1));Assert.That(d.buildings[0].definitionId,Is.EqualTo("core.building.housing"));Assert.That(d.legacyPathId,Is.EqualTo("core.legacy.void-debt"));}
        [Test] public void VersionOneSaveRemainsReadable(){var d=FormalSaveCodec.Decode("{\"schema\":1,\"worldSeed\":8128,\"iron\":5}");Assert.That(d,Is.Not.Null);Assert.That(d.schema,Is.EqualTo(1));Assert.That(d.iron,Is.EqualTo(5));}
        [Test] public void EnemyQualityRoundTrips(){var data=new FormalSaveData{enemies=new[]{new WasteCity.Combat.EnemySnapshot{archetype=1,quality=(int)WasteCity.Combat.EnemyQuality.Epic,health=321}}};var restored=FormalSaveCodec.Decode(FormalSaveCodec.Encode(data));Assert.That(restored.enemies[0].quality,Is.EqualTo((int)WasteCity.Combat.EnemyQuality.Epic));Assert.That(restored.enemies[0].health,Is.EqualTo(321));}
        [Test] public void ControlledEnemyStateRoundTrips(){var data=new FormalSaveData{enemies=new[]{new WasteCity.Combat.EnemySnapshot{archetype=0,quality=0,controlled=true,health=42}}};var restored=FormalSaveCodec.Decode(FormalSaveCodec.Encode(data));Assert.That(restored.enemies[0].controlled,Is.True);Assert.That(restored.enemies[0].health,Is.EqualTo(42));}
        [Test] public void RouteResourcesAndProductionProgressRoundTrip(){var restored=FormalSaveCodec.Decode(FormalSaveCodec.Encode(new FormalSaveData{spiritIron=7,flyingSword=3,boneSteel=9,biomassConcentrate=4,biologicalWeapon=2,resonanceMetal=8,psionicAmplifier=5,productionProgress=new[]{1f,2f,3f}}));Assert.That(restored.spiritIron,Is.EqualTo(7));Assert.That(restored.biologicalWeapon,Is.EqualTo(2));Assert.That(restored.psionicAmplifier,Is.EqualTo(5));Assert.That(restored.productionProgress,Is.EqualTo(new[]{1f,2f,3f}));}
        [Test] public void ElixirRoundTripsInCurrentSchema(){var restored=FormalSaveCodec.Decode(FormalSaveCodec.Encode(new FormalSaveData{elixir=4}));Assert.That(restored.schema,Is.EqualTo(25));Assert.That(restored.elixir,Is.EqualTo(4));}
        [Test] public void VersionTwentyTwoPuppetsRoundTrip(){var restored=FormalSaveCodec.Decode(FormalSaveCodec.Encode(new FormalSaveData{schema=22,puppetProgress=7.5f,puppets=new[]{new FriendlyUnitSnapshot{x=2f,y=-3f,health=91}}}));Assert.That(restored.schema,Is.EqualTo(22));Assert.That(restored.puppetProgress,Is.EqualTo(7.5f));Assert.That(restored.puppets.Length,Is.EqualTo(1));Assert.That(restored.puppets[0].health,Is.EqualTo(91));}
        [Test] public void VersionTwentyThreeBehemothsRoundTrip(){var restored=FormalSaveCodec.Decode(FormalSaveCodec.Encode(new FormalSaveData{schema=23,behemothProgress=11f,behemoths=new[]{new FriendlyUnitSnapshot{x=-4f,y=6f,health=515}}}));Assert.That(restored.schema,Is.EqualTo(23));Assert.That(restored.behemothProgress,Is.EqualTo(11f));Assert.That(restored.behemoths[0].health,Is.EqualTo(515));}
        [Test] public void VersionTwentyFourRallyAndFriendlyLossesRoundTrip()
        {
            var data = new FormalSaveData { schema = 24, rallyFixed = true, rallyX = 7.5f, rallyY = -2.5f, puppetLosses = 2, behemothLosses = 3, controlledLosses = 4 };

            var restored = FormalSaveCodec.Decode(FormalSaveCodec.Encode(data));

            Assert.That(restored.schema, Is.EqualTo(24));
            Assert.That(restored.rallyFixed, Is.True);
            Assert.That(restored.rallyX, Is.EqualTo(7.5f));
            Assert.That(restored.rallyY, Is.EqualTo(-2.5f));
            Assert.That(restored.puppetLosses, Is.EqualTo(2));
            Assert.That(restored.behemothLosses, Is.EqualTo(3));
            Assert.That(restored.controlledLosses, Is.EqualTo(4));
        }
        [Test] public void VersionTwentyThreeDefaultsToFollowCityWithNoFriendlyLosses()
        {
            var restored = FormalSaveCodec.Decode("{\"schema\":23,\"cityX\":4,\"cityY\":5}");

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.rallyFixed, Is.False);
            Assert.That(restored.puppetLosses, Is.Zero);
            Assert.That(restored.behemothLosses, Is.Zero);
            Assert.That(restored.controlledLosses, Is.Zero);
        }
        [Test] public void VersionTwentyFiveEnemyInfectionRoundTrips()
        {
            var data = new FormalSaveData
            {
                enemies = new[]
                {
                    new EnemySnapshot { infectionStacks = 7, infectionElapsed = .4f }
                }
            };

            var restored = FormalSaveCodec.Decode(FormalSaveCodec.Encode(data));

            Assert.That(restored.schema, Is.EqualTo(25));
            Assert.That(restored.enemies[0].infectionStacks, Is.EqualTo(7));
            Assert.That(restored.enemies[0].infectionElapsed, Is.EqualTo(.4f));
        }
        [Test] public void VersionTwentyFourDefaultsEnemyInfectionToZero()
        {
            var restored = FormalSaveCodec.Decode("{\"schema\":24,\"enemies\":[{\"health\":42}]}");

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.enemies[0].infectionStacks, Is.Zero);
            Assert.That(restored.enemies[0].infectionElapsed, Is.Zero);
        }
        [Test] public void RewindKeepsCurrentAttentionAndAddsThree(){Assert.That(RewindAnchorRules.AttentionAfterLoad(42f),Is.EqualTo(45f));Assert.That(RewindAnchorRules.AttentionAfterLoad(99f),Is.EqualTo(100f));}
        [Test] public void DebtResourceCanBeRestoredNegative(){var i=new ResourceInventory(150);i.SetDebtLimit(100);i.Restore(ResourceIds.Iron,-40);Assert.That(i.Get(ResourceIds.Iron),Is.EqualTo(-40));}
        [Test] public void CorruptSaveReturnsNull()=>Assert.That(FormalSaveCodec.Decode("{broken"),Is.Null);
    }
}
