using NUnit.Framework;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class EnemyQualityTests
    {
        [Test] public void ExcellentQualityAppliesDocumentedStatMultipliers(){var profile=EnemyQualityCatalog.For(EnemyQuality.Excellent);Assert.That(profile.HealthMultiplier,Is.EqualTo(2f));Assert.That(profile.DamageMultiplier,Is.EqualTo(1.5f));}
        [Test] public void CivilizationAdvanceDoublesRareRollWindow(){Assert.That(EnemyQualityRoller.FromRoll(45,1,false),Is.EqualTo(EnemyQuality.Excellent));Assert.That(EnemyQualityRoller.FromRoll(45,2,false),Is.EqualTo(EnemyQuality.Rare));}
        [Test] public void QualityRollIsDeterministic(){Assert.That(EnemyQualityRoller.ForSpawn(12,120,2),Is.EqualTo(EnemyQualityRoller.ForSpawn(12,120,2)));}
    }
}
