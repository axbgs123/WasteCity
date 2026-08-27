using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class CrystalBroodmotherCatalogTests
    {
        private const string TypeName =
            "WasteCity.Combat.CrystalBroodmotherCatalog";

        [Test]
        public void IDEA0020_CatalogOwnsFormalIdentityHealthAndMovement()
        {
            Assert.That(CrystalBroodmotherCatalog.Definition,
                Is.SameAs(EnemyCatalog.CrystalBroodmother));
            Assert.That(CrystalBroodmotherCatalog.StableArchetypeId,
                Is.EqualTo(EnemyCatalog.CrystalBroodmother.Id.Value));
            Assert.That(CrystalBroodmotherCatalog.MaximumHealth,
                Is.EqualTo(EnemyCatalog.CrystalBroodmother.MaximumHealth));
            Assert.That(
                CrystalBroodmotherCatalog.MovementSpeedCellsPerSecond,
                Is.EqualTo(EnemyCatalog.CrystalBroodmother.MoveSpeed));
            Assert.That(CrystalBroodmotherCatalog.DamagePerSecond,
                Is.EqualTo(EnemyCatalog.CrystalBroodmother.DamagePerSecond));
            Assert.That(CrystalBroodmotherCatalog.AttackRangeCells,
                Is.EqualTo(EnemyCatalog.CrystalBroodmother.AttackRange));
            Assert.That(CrystalBroodmotherCatalog.FixedStepSeconds,
                Is.EqualTo(.1f));
        }

        [Test]
        public void IDEA0020_CatalogOwnsSeventyAndThirtyFivePercentReinforcements()
        {
            Type type = RequireType(TypeName);
            object phases = type.GetProperty("Phases")?.GetValue(null);
            Assert.That(phases, Is.InstanceOf<IEnumerable>());
            object[] values = ((IEnumerable)phases).Cast<object>().ToArray();
            Assert.That(values, Has.Length.EqualTo(2));
            Assert.That(Read<float>(values[0], "HealthRatioThreshold"),
                Is.EqualTo(.7f));
            Assert.That(Read<float>(values[1], "HealthRatioThreshold"),
                Is.EqualTo(.35f));
            AssertSpawn(values[0], EnemyArchetype.CrystalBeast, 4);
            AssertSpawn(values[1], EnemyArchetype.Gnawer, 6);
            AssertSpawn(values[1], EnemyArchetype.Howler, 2);
        }

        private static void AssertSpawn(
            object phase,
            EnemyArchetype archetype,
            int count)
        {
            object[] spawns = ((IEnumerable)Read<object>(phase,
                    "Reinforcements"))
                .Cast<object>().ToArray();
            object match = spawns.Single(value =>
                Read<EnemyArchetype>(value, "Archetype") == archetype);
            Assert.That(Read<int>(match, "Count"), Is.EqualTo(count));
        }

        private static T Read<T>(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, name);
            return (T)property.GetValue(owner);
        }

        private static Type RequireType(string name)
        {
            Type type = typeof(EnemyCatalog).Assembly.GetType(name, false);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }
    }
}
