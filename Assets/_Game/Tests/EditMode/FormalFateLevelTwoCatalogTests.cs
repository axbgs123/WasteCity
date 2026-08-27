using System;
using System.Reflection;
using NUnit.Framework;

namespace WasteCity.Tests
{
    public sealed class FormalFateLevelTwoCatalogTests
    {
        [Test]
        public void IDEA0020_LevelTwoRulesAreCentralAndExact()
        {
            Type type = typeof(WasteCity.Progression.FormalFateCatalog).Assembly
                .GetType("WasteCity.Progression.FormalFateLevelTwoCatalog");
            Assert.That(type, Is.Not.Null);
            Assert.That(Constant<int>(type, "PocketUniverseOutputMultiplier"),
                Is.EqualTo(4));
            Assert.That(Constant<int>(type, "PocketUniverseCollapseSize"),
                Is.EqualTo(4));
            Assert.That(Constant<double>(type, "VoidDebtSettlementSeconds"),
                Is.EqualTo(60d));
            Assert.That(Constant<int>(type, "RewindAnchorCapacity"),
                Is.EqualTo(2));
        }

        private static T Constant<T>(Type owner, string name)
        {
            FieldInfo field = owner.GetField(name,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetRawConstantValue();
        }
    }
}
