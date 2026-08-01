using NUnit.Framework;
using WasteCity.Persistence;
namespace WasteCity.Tests
{
    public sealed class FormalSaveTests
    {
        [Test] public void SaveCodecRoundTrips(){var d=FormalSaveCodec.Decode(FormalSaveCodec.Encode(new FormalSaveData{worldSeed=42,cityX=3.5f,iron=19,legacyPathId="core.legacy.void-debt"}));Assert.That(d.worldSeed,Is.EqualTo(42));Assert.That(d.cityX,Is.EqualTo(3.5f));Assert.That(d.legacyPathId,Is.EqualTo("core.legacy.void-debt"));}
        [Test] public void CorruptSaveReturnsNull()=>Assert.That(FormalSaveCodec.Decode("{broken"),Is.Null);
    }
}
