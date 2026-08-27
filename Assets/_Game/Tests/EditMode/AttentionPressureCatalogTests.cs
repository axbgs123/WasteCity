using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace WasteCity.Tests
{
    public sealed class AttentionPressureCatalogTests
    {
        private const string CatalogTypeName =
            "WasteCity.Progression.AttentionPressureCatalog";
        private const string DefinitionTypeName =
            "WasteCity.Progression.AttentionPressureDefinition";

        [Test]
        public void IDEA0020_CatalogDefinesExactThirtySixtyNinetyPressureEvents()
        {
            Type catalog = RequireType(CatalogTypeName);
            Type definition = RequireType(DefinitionTypeName);
            FieldInfo capacity = catalog.GetField(
                "QueueCapacity",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(capacity, Is.Not.Null);
            Assert.That(capacity.GetRawConstantValue(), Is.EqualTo(3));
            object[] all = ReadStaticSequence(catalog, "All");
            Assert.That(all, Has.Length.EqualTo(3));

            AssertDefinition(all[0], definition, 0, 30,
                "core.attention-threshold.echo",
                "core.attention-encounter.directional-attack",
                "定向攻击", 60f);
            AssertDefinition(all[1], definition, 1, 60,
                "core.attention-threshold.high-risk",
                "core.attention-encounter.high-risk-attack",
                "高风险攻击", 75f);
            AssertDefinition(all[2], definition, 2, 90,
                "core.attention-threshold.locked",
                "core.attention-encounter.crystalline-broodmother",
                "晶壳母体", 90f);
        }

        [Test]
        public void IDEA0020_FindByThresholdHasNoUnknownFallback()
        {
            Type catalog = RequireType(CatalogTypeName);
            MethodInfo find = catalog.GetMethod(
                "FindByThreshold",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int) },
                null);
            Assert.That(find, Is.Not.Null);
            foreach (int threshold in new[] { 30, 60, 90 })
                Assert.That(find.Invoke(null, new object[] { threshold }),
                    Is.Not.Null, threshold.ToString());
            Assert.That(find.Invoke(null, new object[] { 29 }), Is.Null);
            Assert.That(find.Invoke(null, new object[] { 100 }), Is.Null);
        }

        private static void AssertDefinition(
            object value,
            Type definition,
            int order,
            int threshold,
            string thresholdId,
            string encounterId,
            string displayName,
            float warningSeconds)
        {
            Assert.That(value, Is.TypeOf(definition));
            Assert.That(Read<int>(value, "Order"), Is.EqualTo(order));
            Assert.That(Read<int>(value, "Threshold"), Is.EqualTo(threshold));
            Assert.That(ReadStableId(value, "ThresholdId"),
                Is.EqualTo(thresholdId));
            Assert.That(ReadStableId(value, "EncounterId"),
                Is.EqualTo(encounterId));
            Assert.That(Read<string>(value, "DisplayName"),
                Is.EqualTo(displayName));
            Assert.That(Read<float>(value, "WarningSeconds"),
                Is.EqualTo(warningSeconds));
        }

        private static object[] ReadStaticSequence(Type owner, string name)
        {
            PropertyInfo property = owner.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(property, Is.Not.Null);
            return ((IEnumerable)property.GetValue(null)).Cast<object>()
                .ToArray();
        }

        private static T Read<T>(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null,
                owner.GetType().FullName + "." + name);
            return (T)property.GetValue(owner);
        }

        private static string ReadStableId(object owner, string name)
        {
            object stableId = Read<object>(owner, name);
            return (string)stableId.GetType().GetProperty("Value")
                .GetValue(stableId);
        }

        private static Type RequireType(string name)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(name, false))
                .FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }
    }
}
