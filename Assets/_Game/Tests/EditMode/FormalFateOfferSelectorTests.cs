using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace WasteCity.Tests
{
    public sealed class FormalFateOfferSelectorTests
    {
        private const string SelectorTypeName =
            "WasteCity.Progression.FormalFateOfferSelector, WasteCity.Game";
        private const string CatalogTypeName =
            "WasteCity.Progression.FormalFateCatalog, WasteCity.Game";

        [Test]
        public void IDEA0028_SameSessionWorldAndVersionReturnsSameThree()
        {
            Type selector = RequireType(SelectorTypeName);
            string[] first = Select(selector, "session-alpha", 8128, 1);
            string[] second = Select(selector, "session-alpha", 8128, 1);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Has.Length.EqualTo(3));
            Assert.That(first.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(3));
        }

        [Test]
        public void IDEA0028_OffersAreUniqueMembersOfTheNineFateCatalog()
        {
            Type selector = RequireType(SelectorTypeName);
            Type catalog = RequireType(CatalogTypeName);
            string[] approved = ReadSequence(catalog, "All")
                .Cast<object>()
                .Select(item => ReadStableId(item, "Id"))
                .ToArray();
            var observed = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < 32; index++)
            {
                string[] offers = Select(
                    selector,
                    "session-" + index,
                    8128 + index,
                    1 + index % 3);
                Assert.That(offers, Has.Length.EqualTo(3));
                Assert.That(offers.Distinct(StringComparer.Ordinal).Count(),
                    Is.EqualTo(3));
                Assert.That(offers.All(approved.Contains), Is.True);
                foreach (string offer in offers)
                    observed.Add(offer);
            }

            Assert.That(observed, Is.EquivalentTo(approved),
                "The fixed selection sample must prove all nine are reachable.");
        }

        [Test]
        public void IDEA0028_SelectionTupleActuallyParticipatesInTheDraw()
        {
            Type selector = RequireType(SelectorTypeName);
            string baseline = Fingerprint(Select(
                selector, "session-alpha", 8128, 1));
            string[] variants =
            {
                Fingerprint(Select(selector, "session-beta", 8128, 1)),
                Fingerprint(Select(selector, "session-alpha", 8129, 1)),
                Fingerprint(Select(selector, "session-alpha", 8128, 2)),
            };

            Assert.That(variants.Any(value => value != baseline), Is.True,
                "Session, world seed and selector version must seed the draw.");
        }

        [Test]
        public void IDEA0028_BlankSessionOrInvalidVersionIsRejected()
        {
            Type selector = RequireType(SelectorTypeName);
            AssertInvocationThrows<ArgumentException>(
                () => Select(selector, "", 8128, 1));
            AssertInvocationThrows<ArgumentException>(
                () => Select(selector, "   ", 8128, 1));
            AssertInvocationThrows<ArgumentOutOfRangeException>(
                () => Select(selector, "session-alpha", 8128, 0));
        }

        private static string[] Select(
            Type selector,
            string sessionId,
            int worldSeed,
            int version)
        {
            MethodInfo method = selector.GetMethod(
                "Select",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(int), typeof(int) },
                null);
            Assert.That(method, Is.Not.Null);
            object value = method.Invoke(
                null,
                new object[] { sessionId, worldSeed, version });
            Assert.That(value, Is.InstanceOf<IEnumerable>());
            return ((IEnumerable)value).Cast<object>()
                .Select(item => item.ToString())
                .ToArray();
        }

        private static string Fingerprint(string[] values) =>
            string.Join("|", values);

        private static void AssertInvocationThrows<T>(TestDelegate action)
            where T : Exception
        {
            TargetInvocationException wrapper =
                Assert.Throws<TargetInvocationException>(action);
            Assert.That(wrapper.InnerException, Is.TypeOf<T>());
        }

        private static Type RequireType(string name)
        {
            Type type = Type.GetType(name, false);
            Assert.That(type, Is.Not.Null, name);
            return type;
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

        private static string ReadStableId(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null);
            object stableId = property.GetValue(owner);
            PropertyInfo value = stableId?.GetType().GetProperty("Value");
            Assert.That(value, Is.Not.Null);
            return (string)value.GetValue(stableId);
        }
    }
}
