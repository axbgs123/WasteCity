using NUnit.Framework;
using WasteCity.Persistence;
using WasteCity.Legacy;
using WasteCity.Economy;
namespace WasteCity.Tests
{
    public sealed class FormalSaveTests
    {
        [Test] public void SaveCodecRoundTrips(){var d=FormalSaveCodec.Decode(FormalSaveCodec.Encode(new FormalSaveData{worldSeed=42,cityX=3.5f,iron=19,alloy=12,ammunition=7,population=180,observation=44,legacyPathId="core.legacy.void-debt"}));Assert.That(d.worldSeed,Is.EqualTo(42));Assert.That(d.cityX,Is.EqualTo(3.5f));Assert.That(d.alloy,Is.EqualTo(12));Assert.That(d.ammunition,Is.EqualTo(7));Assert.That(d.population,Is.EqualTo(180));Assert.That(d.observation,Is.EqualTo(44));Assert.That(d.legacyPathId,Is.EqualTo("core.legacy.void-debt"));}
        [Test] public void VersionOneSaveRemainsReadable(){var d=FormalSaveCodec.Decode("{\"schema\":1,\"worldSeed\":8128,\"iron\":5}");Assert.That(d,Is.Not.Null);Assert.That(d.schema,Is.EqualTo(1));Assert.That(d.iron,Is.EqualTo(5));}
        [Test] public void RewindKeepsCurrentAttentionAndAddsThree(){Assert.That(RewindAnchorRules.AttentionAfterLoad(42f),Is.EqualTo(45f));Assert.That(RewindAnchorRules.AttentionAfterLoad(99f),Is.EqualTo(100f));}
        [Test] public void DebtResourceCanBeRestoredNegative(){var i=new ResourceInventory(150);i.SetDebtLimit(100);i.Restore(ResourceIds.Iron,-40);Assert.That(i.Get(ResourceIds.Iron),Is.EqualTo(-40));}
        [Test] public void CorruptSaveReturnsNull()=>Assert.That(FormalSaveCodec.Decode("{broken"),Is.Null);
    }
}
