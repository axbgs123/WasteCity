using NUnit.Framework;
using WasteCity.Combat;
using System.Linq;

namespace WasteCity.Tests
{
    public sealed class EnemyCatalogTests
    {
        [Test] public void EnemyIdsAreStableAndUnique(){Assert.That(EnemyCatalog.All.Select(value=>value.Id.Value).Distinct().Count(),Is.EqualTo(EnemyCatalog.All.Length));}
        [Test] public void GnawerMatchesFormalBaseline(){Assert.That(EnemyCatalog.Gnawer.MaximumHealth,Is.EqualTo(60));Assert.That(EnemyCatalog.Gnawer.MoveSpeed,Is.EqualTo(1.8f));Assert.That(EnemyCatalog.Gnawer.DamagePerSecond,Is.EqualTo(8));Assert.That(EnemyCatalog.Gnawer.BiomassDrop,Is.EqualTo(1));}
        [Test] public void CrystalBeastUsesHeavyArmorAndTargetsWalls(){Assert.That(EnemyCatalog.CrystalBeast.MaximumHealth,Is.EqualTo(220));Assert.That(EnemyCatalog.CrystalBeast.Armor,Is.EqualTo(ArmorType.Heavy));Assert.That(EnemyCatalog.CrystalBeast.TargetPriority,Is.EqualTo(EnemyTargetPriority.Walls));}
        [Test] public void HowlerIsRangedAndTargetsProduction(){Assert.That(EnemyCatalog.Howler.AttackRange,Is.EqualTo(7));Assert.That(EnemyCatalog.Howler.TargetPriority,Is.EqualTo(EnemyTargetPriority.Production));}
        [Test] public void BurrowerMatchesEliteBaseline(){Assert.That(EnemyCatalog.Burrower.MaximumHealth,Is.EqualTo(500));Assert.That(EnemyCatalog.Burrower.DamagePerSecond,Is.EqualTo(25));Assert.That(EnemyCatalog.Burrower.BiomassDrop,Is.EqualTo(8));}
    }
}
