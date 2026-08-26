using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace WasteCity.Tests
{
    public sealed class FormalAttentionCatalogTests
    {
        private const string CatalogTypeName =
            "WasteCity.Progression.FormalAttentionCatalog, WasteCity.Game";
        private const string DefinitionTypeName =
            "WasteCity.Progression.FormalAttentionReasonDefinition, " +
            "WasteCity.Game";
        private const string PolicyTypeName =
            "WasteCity.Progression.FormalAttentionRepeatPolicy, " +
            "WasteCity.Game";

        private static readonly IReadOnlyDictionary<string, ExpectedReason>
            Expected = new Dictionary<string, ExpectedReason>(
                StringComparer.Ordinal)
            {
                { "core.attention.fate.first-activation", Once(5) },
                { "core.attention.scan.safe-mining-zone", Once(2) },
                { "core.attention.scan.crystal-rift", Once(5) },
                { "core.attention.city.first-deployment", Once(5) },
                { "core.attention.building.first-mining-station", Once(2) },
                { "core.attention.building.first-smelter", Once(3) },
                { "core.attention.building.first-assembler", Once(4) },
                { "core.attention.building.machine-gun-turret", Event(5) },
                { "core.attention.research.automated-machinery", Once(3) },
                { "core.attention.research.precision-assembly", Once(4) },
                { "core.attention.research.automated-defense", Once(5) },
                { "core.attention.research.reinforced-structures", Once(5) },
                { "core.attention.research.legacy-analysis", Once(12) },
                { "core.attention.rescue.ruins", Event(2) },
                { "core.attention.rescue.cen-jin", Once(5) },
                { "core.attention.combat.first-directed-attack-defeated", Once(8) },
                { "core.attention.fate.rewind-anchor-used", Event(12) },
                { "core.attention.fate.void-debt-periodic", Event(1) },
                { "core.attention.fate.pocket-universe-activated", Once(4) },
                { "core.attention.escape.locked-region", Once(-8) },
                { "core.attention.ruins.optional-interference", Event(-5) },
                { "core.attention.civilization.advanced", Event(25) },
            };

        [Test]
        public void IDEA0020_CatalogExposesBoundedAttentionContract()
        {
            Type catalog = RequireType(CatalogTypeName);

            Assert.That(ReadConstant(catalog, "InitialValue"), Is.EqualTo(10));
            Assert.That(ReadConstant(catalog, "MinimumValue"), Is.Zero);
            Assert.That(ReadConstant(catalog, "MaximumValue"), Is.EqualTo(100));
            Assert.That(ReadConstant(catalog, "HistoryCapacity"), Is.EqualTo(128));
            Assert.That(ReadConstant(catalog, "RecentReasonCapacity"), Is.EqualTo(3));
            Assert.That(
                ReadIntSequence(catalog, "Thresholds"),
                Is.EqualTo(new[] { 30, 60, 90 }));
        }

        [Test]
        public void IDEA0020_CatalogContainsEveryA166SourceExactlyOnce()
        {
            Type catalog = RequireType(CatalogTypeName);
            Type definition = RequireType(DefinitionTypeName);
            Type policy = RequireType(PolicyTypeName);
            RequireProperty(definition, "Id");
            RequireProperty(definition, "Delta", typeof(int));
            RequireProperty(definition, "RepeatPolicy", policy);
            RequireProperty(definition, "LocalizationKey", typeof(string));

            object[] all = ReadSequence(catalog, "All").Cast<object>().ToArray();
            Assert.That(all, Has.Length.EqualTo(22));
            var actual = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (object item in all)
            {
                Assert.That(item, Is.TypeOf(definition));
                string id = ReadStableId(item, "Id");
                Assert.That(actual.ContainsKey(id), Is.False, id);
                actual.Add(id, item);
            }
            Assert.That(actual.Keys, Is.EquivalentTo(Expected.Keys));

            foreach (KeyValuePair<string, ExpectedReason> pair in Expected)
            {
                object item = actual[pair.Key];
                Assert.That(Read<int>(item, "Delta"),
                    Is.EqualTo(pair.Value.Delta), pair.Key);
                Assert.That(Read<object>(item, "RepeatPolicy").ToString(),
                    Is.EqualTo(pair.Value.Policy), pair.Key);
                Assert.That(Read<string>(item, "LocalizationKey"),
                    Is.EqualTo("attention.reason." +
                        pair.Key.Substring("core.attention.".Length)
                            .Replace('.', '-')),
                    pair.Key);
            }
        }

        [Test]
        public void IDEA0020_FindUsesStableIdAndUnknownDoesNotFallback()
        {
            Type catalog = RequireType(CatalogTypeName);
            MethodInfo find = catalog.GetMethod(
                "Find",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(find, Is.Not.Null);
            foreach (string id in Expected.Keys)
            {
                object found = find.Invoke(null, new object[] { id });
                Assert.That(found, Is.Not.Null, id);
                Assert.That(ReadStableId(found, "Id"), Is.EqualTo(id));
            }
            Assert.That(find.Invoke(null, new object[] { "unknown.attention" }),
                Is.Null);
            Assert.That(find.Invoke(null, new object[] { null }), Is.Null);
        }

        private static ExpectedReason Once(int delta) =>
            new ExpectedReason(delta, "OncePerSession");

        private static ExpectedReason Event(int delta) =>
            new ExpectedReason(delta, "OncePerStableEvent");

        private static Type RequireType(string name)
        {
            Type type = Type.GetType(name, false);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private static int ReadConstant(Type owner, string name)
        {
            FieldInfo field = owner.GetField(
                name,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, owner.FullName + "." + name);
            Assert.That(field.IsLiteral, Is.True, name);
            return (int)field.GetRawConstantValue();
        }

        private static IEnumerable ReadSequence(Type owner, string name)
        {
            PropertyInfo property = owner.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(property, Is.Not.Null, owner.FullName + "." + name);
            object value = property.GetValue(null);
            Assert.That(value, Is.InstanceOf<IEnumerable>());
            return (IEnumerable)value;
        }

        private static int[] ReadIntSequence(Type owner, string name) =>
            ReadSequence(owner, name).Cast<object>()
                .Select(value => Convert.ToInt32(value))
                .ToArray();

        private static PropertyInfo RequireProperty(
            Type owner,
            string name,
            Type expected = null)
        {
            PropertyInfo property = owner.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, owner.FullName + "." + name);
            if (expected != null)
                Assert.That(property.PropertyType, Is.EqualTo(expected), name);
            return property;
        }

        private static T Read<T>(object owner, string name) =>
            (T)RequireProperty(owner.GetType(), name).GetValue(owner);

        private static string ReadStableId(object owner, string name)
        {
            object stableId = RequireProperty(owner.GetType(), name)
                .GetValue(owner);
            Assert.That(stableId, Is.Not.Null);
            PropertyInfo value = stableId.GetType().GetProperty("Value");
            Assert.That(value, Is.Not.Null);
            return (string)value.GetValue(stableId);
        }

        private readonly struct ExpectedReason
        {
            public ExpectedReason(int delta, string policy)
            {
                Delta = delta;
                Policy = policy;
            }

            public int Delta { get; }
            public string Policy { get; }
        }
    }
}
