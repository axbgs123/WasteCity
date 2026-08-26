using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class PocketUniverseFateEffectTests
    {
        private const string Namespace = "WasteCity.Progression.";
        private static Type EffectType => RequireType(
            Namespace + "PocketUniverseFateEffect");
        private static Type CandidateType => RequireType(
            Namespace + "PocketUniverseBuildingCandidate");
        private static Type SnapshotType => RequireType(
            Namespace + "PocketUniverseFateSnapshot");
        private static Type FlagshipType => RequireType(
            Namespace + "PocketUniverseFlagshipState");
        private static Type CommandType => RequireType(
            Namespace + "PocketUniverseCollapseCommand");

        [Test]
        public void IDEA0020_EligibleCategoriesComeOnlyFromFormalMachineCatalog()
        {
            object effect = Activator.CreateInstance(EffectType);
            string[] expected = FormalProductionDefinitionCatalog.All
                .Select(value => value.BuildingId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.That(StringSequence(
                effect,
                "EligibleBuildingDefinitionIds"), Is.EqualTo(expected));
            Assert.That(expected, Is.Not.Empty);
        }

        [Test]
        public void IDEA0020_FirstCompletedOwnedStableIdWinsAndNeverReselects()
        {
            object effect = Activator.CreateInstance(EffectType);
            string[] definitions = StringSequence(
                effect,
                "EligibleBuildingDefinitionIds");
            Array candidates = CandidateArray(definitions, includeInvalid: true);

            Assert.That(SelectFlagships(effect, candidates),
                Is.EqualTo(definitions.Length));
            object selected = Capture(effect);
            object[] flagships = Sequence(selected, "Flagships").ToArray();
            Assert.That(flagships, Has.Length.EqualTo(definitions.Length));
            for (var index = 0; index < definitions.Length; index++)
            {
                Assert.That(Read<string>(flagships[index],
                    "BuildingDefinitionId"), Is.EqualTo(definitions[index]));
                Assert.That(Read<string>(flagships[index],
                    "StableInstanceId"), Is.EqualTo(StableId(index, 1)));
                Assert.That(OutputMultiplier(effect, StableId(index, 1)),
                    Is.EqualTo(2));
            }
            Assert.That(OutputMultiplier(effect, "building.instance.999999"),
                Is.EqualTo(1));

            Array later = Array.CreateInstance(CandidateType, definitions.Length);
            for (var index = 0; index < definitions.Length; index++)
            {
                later.SetValue(NewCandidate(
                    StableId(index, 0),
                    definitions[index],
                    completed: true,
                    playerOwned: true), index);
            }
            Assert.That(SelectFlagships(effect, later), Is.Zero);
            Assert.That(Capture(effect), Is.SameAs(selected));

            Assert.That(TrySetLevel(effect, 2, out string error), Is.True,
                error);
            Assert.That(OutputMultiplier(effect, StableId(0, 1)),
                Is.EqualTo(4));
            object levelTwo = Capture(effect);
            Assert.That(TrySetLevel(effect, 3, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(Capture(effect), Is.SameAs(levelTwo));
        }

        [Test]
        public void IDEA0020_FlagshipDestructionEmitsOneSizedCollapseCommand()
        {
            object levelOne = Activator.CreateInstance(EffectType);
            string definition = StringSequence(
                levelOne,
                "EligibleBuildingDefinitionIds")[0];
            string stableId = StableId(0, 1);
            SelectOne(levelOne, stableId, definition);

            Assert.That(TryCollapse(
                levelOne,
                stableId,
                12,
                15,
                out object first), Is.True);
            Assert.That(Read<int>(first, "CenterX"), Is.EqualTo(12));
            Assert.That(Read<int>(first, "CenterY"), Is.EqualTo(15));
            Assert.That(Read<int>(first, "Size"), Is.EqualTo(3));
            Assert.That(Read<string>(first, "StableInstanceId"),
                Is.EqualTo(stableId));
            Assert.That(TryCollapse(
                levelOne,
                stableId,
                12,
                15,
                out _), Is.False);
            Assert.That(TryCollapse(
                levelOne,
                "building.instance.999999",
                0,
                0,
                out _), Is.False);

            object levelTwo = Activator.CreateInstance(EffectType);
            Assert.That(TrySetLevel(levelTwo, 2, out _), Is.True);
            SelectOne(levelTwo, stableId, definition);
            Assert.That(TryCollapse(
                levelTwo,
                stableId,
                4,
                6,
                out object upgraded), Is.True);
            Assert.That(Read<int>(upgraded, "Size"), Is.EqualTo(4));
        }

        [Test]
        public void IDEA0020_CaptureRestoreIsDeepAtomicAndDoesNotReselect()
        {
            object source = Activator.CreateInstance(EffectType);
            string definition = StringSequence(
                source,
                "EligibleBuildingDefinitionIds")[0];
            string stableId = StableId(0, 1);
            SelectOne(source, stableId, definition);
            Assert.That(TryCollapse(source, stableId, 2, 3, out _), Is.True);
            object snapshot = Capture(source);

            object restored = Activator.CreateInstance(EffectType);
            Assert.That(TryRestore(restored, snapshot, out string error),
                Is.True, error);
            Assert.That(OutputMultiplier(restored, stableId), Is.EqualTo(2));
            Assert.That(SelectFlagships(
                restored,
                SingleCandidate(StableId(0, 0), definition)), Is.Zero);
            Assert.That(TryCollapse(restored, stableId, 2, 3, out _), Is.False);

            object beforeInvalid = Capture(restored);
            object invalidLevel = NewSnapshot(
                level: 3,
                revision: 9ul,
                new[] { NewFlagship(definition, stableId) },
                new[] { stableId });
            Assert.That(TryRestore(restored, invalidLevel, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(Capture(restored), Is.SameAs(beforeInvalid));

            object invalidCollapse = NewSnapshot(
                level: 1,
                revision: 9ul,
                new[] { NewFlagship(definition, stableId) },
                new[] { "building.instance.999999" });
            Assert.That(TryRestore(restored, invalidCollapse, out error),
                Is.False);
            Assert.That(Capture(restored), Is.SameAs(beforeInvalid));
        }

        [Test]
        public void IDEA0020_FirstFlagshipProductionCommitsOneStableAttentionEvent()
        {
            object effect = Activator.CreateInstance(EffectType);
            string definition = StringSequence(
                effect,
                "EligibleBuildingDefinitionIds")[0];
            string stableId = StableId(0, 1);
            SelectOne(effect, stableId, definition);

            MethodInfo method = EffectType.GetMethod(
                "TryCommitFirstProduction",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(string),
                    typeof(string).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null);
            object[] firstArguments = { stableId, null };
            Assert.That((bool)method.Invoke(effect, firstArguments), Is.True);
            Assert.That(firstArguments[1], Is.EqualTo(
                "pocket-universe-first-production:" + stableId));
            Assert.That(Read<string>(Capture(effect),
                "FirstProductionFlagshipId"), Is.EqualTo(stableId));

            object[] repeatedArguments = { stableId, null };
            Assert.That((bool)method.Invoke(effect, repeatedArguments),
                Is.False);
            object[] nonFlagship = { "building.instance.999999", null };
            Assert.That((bool)method.Invoke(effect, nonFlagship), Is.False);

            object restored = Activator.CreateInstance(EffectType);
            Assert.That(TryRestore(
                restored,
                Capture(effect),
                out string error), Is.True, error);
            object[] afterRestore = { stableId, null };
            Assert.That((bool)method.Invoke(restored, afterRestore), Is.False);
        }

        private static Array CandidateArray(
            IReadOnlyList<string> definitions,
            bool includeInvalid)
        {
            int stride = includeInvalid ? 4 : 2;
            Array result = Array.CreateInstance(
                CandidateType,
                definitions.Count * stride);
            for (var index = 0; index < definitions.Count; index++)
            {
                int offset = index * stride;
                result.SetValue(NewCandidate(
                    StableId(index, 2), definitions[index], true, true),
                    offset);
                result.SetValue(NewCandidate(
                    StableId(index, 1), definitions[index], true, true),
                    offset + 1);
                if (!includeInvalid) continue;
                result.SetValue(NewCandidate(
                    StableId(index, 0), definitions[index], false, true),
                    offset + 2);
                result.SetValue(NewCandidate(
                    StableId(index, 0), definitions[index], true, false),
                    offset + 3);
            }
            return result;
        }

        private static Array SingleCandidate(
            string stableId,
            string definitionId)
        {
            Array result = Array.CreateInstance(CandidateType, 1);
            result.SetValue(NewCandidate(
                stableId,
                definitionId,
                completed: true,
                playerOwned: true), 0);
            return result;
        }

        private static void SelectOne(
            object effect,
            string stableId,
            string definitionId)
        {
            Assert.That(SelectFlagships(
                effect,
                SingleCandidate(stableId, definitionId)), Is.EqualTo(1));
        }

        private static object NewCandidate(
            string stableId,
            string definitionId,
            bool completed,
            bool playerOwned)
        {
            return Activator.CreateInstance(CandidateType, new object[]
            {
                stableId,
                definitionId,
                completed,
                playerOwned,
            });
        }

        private static object NewFlagship(string definitionId, string stableId)
        {
            return Activator.CreateInstance(FlagshipType, new object[]
            {
                definitionId,
                stableId,
            });
        }

        private static object NewSnapshot(
            int level,
            ulong revision,
            object[] flagships,
            string[] collapsedIds)
        {
            Array typedFlagships = Array.CreateInstance(
                FlagshipType,
                flagships.Length);
            for (var index = 0; index < flagships.Length; index++)
                typedFlagships.SetValue(flagships[index], index);
            return Activator.CreateInstance(SnapshotType, new object[]
            {
                level,
                revision,
                typedFlagships,
                collapsedIds,
            });
        }

        private static int SelectFlagships(object effect, Array candidates)
        {
            MethodInfo method = EffectType.GetMethod(
                "SelectFlagships",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { candidates.GetType() },
                null);
            Assert.That(method, Is.Not.Null);
            return (int)method.Invoke(effect, new object[] { candidates });
        }

        private static int OutputMultiplier(object effect, string stableId)
        {
            MethodInfo method = EffectType.GetMethod(
                "OutputMultiplier",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(method, Is.Not.Null);
            return (int)method.Invoke(effect, new object[] { stableId });
        }

        private static bool TrySetLevel(
            object effect,
            int level,
            out string error)
        {
            MethodInfo method = EffectType.GetMethod(
                "TrySetLevel",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(int), typeof(string).MakeByRefType() },
                null);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { level, null };
            bool result = (bool)method.Invoke(effect, arguments);
            error = arguments[1] as string;
            Assert.That(error, Is.Not.Null);
            return result;
        }

        private static bool TryCollapse(
            object effect,
            string stableId,
            int centerX,
            int centerY,
            out object command)
        {
            MethodInfo method = EffectType.GetMethod(
                "TryCreateCollapseCommand",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    CommandType.MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { stableId, centerX, centerY, null };
            bool result = (bool)method.Invoke(effect, arguments);
            command = arguments[3];
            return result;
        }

        private static object Capture(object effect)
        {
            MethodInfo method = EffectType.GetMethod(
                "Capture",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(effect, null);
        }

        private static bool TryRestore(
            object effect,
            object snapshot,
            out string error)
        {
            MethodInfo method = EffectType.GetMethod(
                "TryRestore",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { SnapshotType, typeof(string).MakeByRefType() },
                null);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { snapshot, null };
            bool result = (bool)method.Invoke(effect, arguments);
            error = arguments[1] as string;
            Assert.That(error, Is.Not.Null);
            return result;
        }

        private static IEnumerable<object> Sequence(
            object owner,
            string propertyName)
        {
            object value = Read<object>(owner, propertyName);
            Assert.That(value, Is.InstanceOf<IEnumerable>());
            return ((IEnumerable)value).Cast<object>();
        }

        private static string[] StringSequence(
            object owner,
            string propertyName) =>
            Sequence(owner, propertyName)
                .Select(value => value.ToString())
                .ToArray();

        private static T Read<T>(object owner, string propertyName)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null,
                owner.GetType().FullName + "." + propertyName);
            return (T)property.GetValue(owner);
        }

        private static Type RequireType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static string StableId(int definitionIndex, int candidate)
        {
            int ordinal = definitionIndex * 10 + candidate;
            return "building.instance." + ordinal.ToString("D6");
        }
    }
}
