using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace WasteCity.Tests
{
    public sealed class FormalFateCatalogTests
    {
        private const string CatalogTypeName =
            "WasteCity.Progression.FormalFateCatalog, WasteCity.Game";
        private const string DefinitionTypeName =
            "WasteCity.Progression.FormalFateDefinition, WasteCity.Game";

        private static readonly ExpectedFate[] Expected =
        {
            new ExpectedFate(
                "core.legacy.pocket-universe",
                "袖珍宇宙",
                "core.fate-effect.pocket-universe"),
            new ExpectedFate(
                "core.legacy.void-debt",
                "虚空债",
                "core.fate-effect.void-debt"),
            new ExpectedFate(
                "core.legacy.rewind-anchor",
                "回溯锚点",
                "core.fate-effect.rewind-anchor"),
        };

        [Test]
        public void IDEA0020_CatalogContainsExactlyThreeFixedFormalFates()
        {
            Type catalog = RequireType(CatalogTypeName);
            Type definition = RequireType(DefinitionTypeName);
            object[] all = ReadSequence(catalog, "All").Cast<object>().ToArray();
            object[] offers = ReadSequence(catalog, "FixedOffers")
                .Cast<object>()
                .ToArray();

            Assert.That(all, Has.Length.EqualTo(3));
            Assert.That(offers, Has.Length.EqualTo(3));
            Assert.That(offers, Is.EqualTo(all),
                "The formal offer order is the catalog order, not a random draw.");
            Assert.That(all.All(item => item.GetType() == definition), Is.True);
            Assert.That(all.Select(item => ReadStableId(item, "Id")),
                Is.EqualTo(Expected.Select(value => value.Id)));
        }

        [Test]
        public void IDEA0020_DefinitionsExposeCompletePlayerFacingAndAdapterData()
        {
            Type catalog = RequireType(CatalogTypeName);
            Type definition = RequireType(DefinitionTypeName);
            RequireProperty(definition, "Id");
            RequireProperty(definition, "DisplayName", typeof(string));
            RequireProperty(definition, "Brief", typeof(string));
            RequireProperty(definition, "EffectAdapterId", typeof(string));

            object[] all = ReadSequence(catalog, "All").Cast<object>().ToArray();
            for (var index = 0; index < Expected.Length; index++)
            {
                object actual = all[index];
                ExpectedFate expected = Expected[index];
                Assert.That(ReadStableId(actual, "Id"), Is.EqualTo(expected.Id));
                Assert.That(Read<string>(actual, "DisplayName"),
                    Is.EqualTo(expected.DisplayName));
                Assert.That(Read<string>(actual, "Brief"), Is.Not.Empty,
                    expected.Id);
                Assert.That(Read<string>(actual, "EffectAdapterId"),
                    Is.EqualTo(expected.EffectAdapterId));
            }
        }

        [Test]
        public void IDEA0020_CatalogKeepsEffectsClosedUntilAllAdaptersExist()
        {
            Type catalog = RequireType(CatalogTypeName);
            Assert.That(ReadStatic<bool>(catalog, "EffectsReady"), Is.False);
            Assert.That(ReadConstant(catalog, "MaximumLevel"), Is.EqualTo(9));
        }

        [Test]
        public void IDEA0020_FindUsesCoreLegacyStableStringsWithoutFallback()
        {
            Type catalog = RequireType(CatalogTypeName);
            MethodInfo find = catalog.GetMethod(
                "Find",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(find, Is.Not.Null);

            foreach (ExpectedFate expected in Expected)
            {
                object found = find.Invoke(null, new object[] { expected.Id });
                Assert.That(found, Is.Not.Null, expected.Id);
                Assert.That(ReadStableId(found, "Id"), Is.EqualTo(expected.Id));
            }
            Assert.That(find.Invoke(null, new object[] { null }), Is.Null);
            Assert.That(find.Invoke(null, new object[] { string.Empty }), Is.Null);
            Assert.That(find.Invoke(null, new object[]
                { "core.legacy.quantum-entanglement" }), Is.Null);
            Assert.That(find.Invoke(null, new object[] { "unknown.fate.id" }),
                Is.Null);
        }

        private static Type RequireType(string name)
        {
            Type type = Type.GetType(name, false);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private static IEnumerable ReadSequence(Type owner, string name)
        {
            object value = ReadStatic<object>(owner, name);
            Assert.That(value, Is.InstanceOf<IEnumerable>());
            return (IEnumerable)value;
        }

        private static T ReadStatic<T>(Type owner, string name)
        {
            PropertyInfo property = owner.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(property, Is.Not.Null, owner.FullName + "." + name);
            return (T)property.GetValue(null);
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

        private static PropertyInfo RequireProperty(
            Type owner,
            string name,
            Type expectedType = null)
        {
            PropertyInfo property = owner.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, owner.FullName + "." + name);
            if (expectedType != null)
                Assert.That(property.PropertyType, Is.EqualTo(expectedType));
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

        private readonly struct ExpectedFate
        {
            public ExpectedFate(
                string id,
                string displayName,
                string effectAdapterId)
            {
                Id = id;
                DisplayName = displayName;
                EffectAdapterId = effectAdapterId;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string EffectAdapterId { get; }
        }
    }
}
