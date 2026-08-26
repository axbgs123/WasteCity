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
        private const string StageTypeName =
            "WasteCity.Progression.FormalAttentionStageDefinition, " +
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

        private static readonly IReadOnlyDictionary<string, string>
            ExpectedDisplayNames = new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                { "core.attention.fate.first-activation", "选择命轨" },
                { "core.attention.scan.safe-mining-zone", "扫描安全矿区" },
                { "core.attention.scan.crystal-rift", "扫描结晶裂谷" },
                { "core.attention.city.first-deployment", "城市首次展开" },
                { "core.attention.building.first-mining-station", "首座采矿站完工" },
                { "core.attention.building.first-smelter", "首座冶炼厂完工" },
                { "core.attention.building.first-assembler", "首座装配厂完工" },
                { "core.attention.building.machine-gun-turret", "机枪塔完工" },
                { "core.attention.research.automated-machinery", "完成基础冶金" },
                { "core.attention.research.precision-assembly", "完成精密装配" },
                { "core.attention.research.automated-defense", "完成自动防御架构" },
                { "core.attention.research.reinforced-structures", "完成加固结构" },
                { "core.attention.research.legacy-analysis", "完成遗产解析" },
                { "core.attention.rescue.ruins", "废墟救援" },
                { "core.attention.rescue.cen-jin", "营救岑烬" },
                { "core.attention.combat.first-directed-attack-defeated", "首次击退定向攻击" },
                { "core.attention.fate.rewind-anchor-used", "使用回溯锚点" },
                { "core.attention.fate.void-debt-periodic", "虚空债结算" },
                { "core.attention.fate.pocket-universe-activated", "袖珍宇宙旗舰启动" },
                { "core.attention.escape.locked-region", "离开锁定观测区域" },
                { "core.attention.ruins.optional-interference", "完成可选干扰遗迹" },
                { "core.attention.civilization.advanced", "文明升阶" },
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
            RequireProperty(definition, "DisplayName", typeof(string));

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
                Assert.That(Read<string>(item, "DisplayName"),
                    Is.EqualTo(ExpectedDisplayNames[pair.Key]),
                    pair.Key);
            }
        }

        [Test]
        public void IDEA0020_DisplayNameResolverUsesChineseFallbackForUnknown()
        {
            Type catalog = RequireType(CatalogTypeName);
            MethodInfo displayName = catalog.GetMethod(
                "DisplayNameForReason",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(displayName, Is.Not.Null);
            foreach (KeyValuePair<string, string> pair in ExpectedDisplayNames)
            {
                Assert.That(displayName.Invoke(null, new object[] { pair.Key }),
                    Is.EqualTo(pair.Value), pair.Key);
            }
            foreach (string unknown in new[]
                {
                    null,
                    string.Empty,
                    "removed.attention.reason",
                })
            {
                string actual = (string)displayName.Invoke(
                    null,
                    new object[] { unknown });
                Assert.That(actual, Is.EqualTo("未知历史原因"));
                if (!string.IsNullOrEmpty(unknown))
                    Assert.That(actual, Does.Not.Contain(unknown).IgnoreCase);
            }
        }

        [Test]
        public void IDEA0020_CatalogDefinesExactFourSemanticStages()
        {
            Type catalog = RequireType(CatalogTypeName);
            Type stageType = RequireType(StageTypeName);
            RequireProperty(stageType, "MinimumInclusive", typeof(int));
            RequireProperty(stageType, "MaximumInclusive", typeof(int));
            RequireProperty(stageType, "DisplayName", typeof(string));
            RequireProperty(stageType, "LocalizationKey", typeof(string));

            object[] stages = ReadSequence(catalog, "Stages")
                .Cast<object>()
                .ToArray();
            Assert.That(stages, Has.Length.EqualTo(4));
            AssertStage(stages[0], 0, 29, "未锁定", "attention.stage.unlocked");
            AssertStage(stages[1], 30, 59, "异常回波", "attention.stage.echo");
            AssertStage(stages[2], 60, 89, "定向观测", "attention.stage.directed");
            AssertStage(stages[3], 90, 100, "坐标锁定", "attention.stage.locked");

            MethodInfo stageFor = catalog.GetMethod(
                "StageFor",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int) },
                null);
            Assert.That(stageFor, Is.Not.Null);
            Assert.That(stageFor.ReturnType, Is.EqualTo(stageType));
            foreach (var sample in new[]
                {
                    (0, 0), (29, 0), (30, 1), (59, 1),
                    (60, 2), (89, 2), (90, 3), (100, 3),
                })
            {
                Assert.That(stageFor.Invoke(null, new object[] { sample.Item1 }),
                    Is.SameAs(stages[sample.Item2]), sample.Item1.ToString());
            }
            Assert.That(stageFor.Invoke(null, new object[] { -1 }), Is.Null);
            Assert.That(stageFor.Invoke(null, new object[] { 101 }), Is.Null);
        }

        [Test]
        public void IDEA0020_NextThresholdSkipsEveryAlreadyLatchedThreshold()
        {
            AssertNextThreshold(10, Array.Empty<int>(), true, 30, 20);
            AssertNextThreshold(30, new[] { 30 }, true, 60, 30);
            AssertNextThreshold(20, new[] { 30 }, true, 60, 40);
            AssertNextThreshold(89, new[] { 30, 60 }, true, 90, 1);
            AssertNextThreshold(20, new[] { 30, 60, 90 }, false, 0, 0);
            AssertNextThreshold(90, new[] { 30, 60, 90 }, false, 0, 0);
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

        private static void AssertStage(
            object stage,
            int minimum,
            int maximum,
            string displayName,
            string localizationKey)
        {
            Assert.That(Read<int>(stage, "MinimumInclusive"),
                Is.EqualTo(minimum));
            Assert.That(Read<int>(stage, "MaximumInclusive"),
                Is.EqualTo(maximum));
            Assert.That(Read<string>(stage, "DisplayName"),
                Is.EqualTo(displayName));
            Assert.That(Read<string>(stage, "LocalizationKey"),
                Is.EqualTo(localizationKey));
        }

        private static void AssertNextThreshold(
            int value,
            IReadOnlyList<int> reached,
            bool expectedResult,
            int expectedThreshold,
            int expectedDistance)
        {
            Type catalog = RequireType(CatalogTypeName);
            MethodInfo method = catalog.GetMethod(
                "TryGetNextUnreachedThreshold",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(int),
                    typeof(IReadOnlyList<int>),
                    typeof(int).MakeByRefType(),
                    typeof(int).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { value, reached, 0, 0 };
            Assert.That((bool)method.Invoke(null, arguments),
                Is.EqualTo(expectedResult));
            Assert.That((int)arguments[2], Is.EqualTo(expectedThreshold));
            Assert.That((int)arguments[3], Is.EqualTo(expectedDistance));
        }

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
