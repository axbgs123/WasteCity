using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class SingleCityDefenseCampaignCatalogTests
    {
        private const string CampaignId = "campaign.single-city-defense.v1";

        private static readonly int[] Gnawers = { 8, 10, 12, 14, 16, 18, 20, 22, 24, 28 };
        private static readonly int[] CrystalBeasts = { 0, 0, 2, 3, 4, 5, 6, 8, 9, 10 };
        private static readonly int[] Howlers = { 0, 0, 0, 0, 2, 3, 4, 5, 7, 8 };
        private static readonly float[] WarningSeconds = { 15, 20, 20, 25, 25, 30, 30, 35, 40, 45 };
        private static readonly float[] SpawnSeconds = { 40, 45, 50, 50, 55, 55, 60, 60, 65, 75 };
        private static readonly string[][] Directions =
        {
            new[] { "East" },
            new[] { "East" },
            new[] { "East", "North" },
            new[] { "East", "North" },
            new[] { "East", "South" },
            new[] { "East", "North", "South" },
            new[] { "East", "North", "South", "West" },
            new[] { "East", "North", "South", "West" },
            new[] { "East", "North", "South", "West" },
            new[] { "East", "North", "South", "West" }
        };

        [Test]
        public void CampaignWaveCatalogExistsWithStableCampaignIdAndExactlyTenWaves()
        {
            MemberTarget campaign = RequireCampaign();
            Assert.That(ReadStableText(ReadRequiredMember(campaign, "Id", "CampaignId")), Is.EqualTo(CampaignId));
            Assert.That(ReadList(ReadRequiredMember(campaign, "Waves", "All")), Has.Count.EqualTo(10));
        }

        [Test]
        public void TenWavesMatchApprovedCompositionWarningSpawnAndDirectionTable()
        {
            IList<object> waves = ReadList(ReadRequiredMember(RequireCampaign(), "Waves", "All"));

            for (int index = 0; index < waves.Count; index++)
            {
                object wave = waves[index];
                int number = index + 1;
                Assert.That(ReadInt(wave, "Number", "WaveNumber", "Index"), Is.EqualTo(number), "第 {0} 波编号必须稳定。", number);
                Assert.That(ReadFloat(wave, "WarningSeconds", "WarningDurationSeconds"), Is.EqualTo(WarningSeconds[index]), "第 {0} 波预警秒数不符合 IDEA-0017。", number);
                Assert.That(ReadFloat(wave, "SpawnSeconds", "SpawnDurationSeconds"), Is.EqualTo(SpawnSeconds[index]), "第 {0} 波生成时长不符合 IDEA-0017。", number);
                Assert.That(ReadDirections(wave), Is.EqualTo(Directions[index]), "第 {0} 波出生方向或顺序不符合 IDEA-0017。", number);

                Dictionary<string, int> composition = ReadComposition(wave);
                Assert.That(CountFor(composition, "Gnawer"), Is.EqualTo(Gnawers[index]), "第 {0} 波啃噬者数量错误。", number);
                Assert.That(CountFor(composition, "CrystalBeast"), Is.EqualTo(CrystalBeasts[index]), "第 {0} 波晶壳兽数量错误。", number);
                Assert.That(CountFor(composition, "Howler"), Is.EqualTo(Howlers[index]), "第 {0} 波啸叫者数量错误。", number);
                Assert.That(composition.Keys, Is.SubsetOf(new[] { "Gnawer", "CrystalBeast", "Howler" }), "第 {0} 波引用了本阶段范围外敌人。", number);
            }
        }

        [Test]
        public void FinalWaveHasFortySixEnemiesAndCampaignContainsNoBossOrBurrower()
        {
            IList<object> waves = ReadList(ReadRequiredMember(RequireCampaign(), "Waves", "All"));
            Assert.That(ReadComposition(waves[9]).Values.Sum(), Is.EqualTo(46));

            string[] enemyKeys = waves.SelectMany(wave => ReadComposition(wave).Keys).Distinct().ToArray();
            Assert.That(enemyKeys, Has.No.Member("Burrower"));
            Assert.That(enemyKeys, Has.No.Member("CrystalBroodmother"));
            Assert.That(enemyKeys, Has.No.Member("Boss"));
        }

        [Test]
        public void ThreeFormalDefenseTowersEachHaveLocalCapacityThirty()
        {
            Assert.That(ReadInt(DefenseTowerCatalog.For(BuildingCatalog.MachineGunTurret.Id.Value), "LocalCapacity", "LocalConsumableCapacity"), Is.EqualTo(30));
            Assert.That(ReadInt(DefenseTowerCatalog.For(BuildingCatalog.LaserTower.Id.Value), "LocalCapacity", "LocalConsumableCapacity"), Is.EqualTo(30));
            Assert.That(ReadInt(DefenseTowerCatalog.For(BuildingCatalog.SporeTower.Id.Value), "LocalCapacity", "LocalConsumableCapacity"), Is.EqualTo(30));
        }

        [Test]
        public void ThreeFormalEnemiesExpressCoreWallsAndProductionInTheSharedCatalog()
        {
            Assert.That(Enum.GetNames(typeof(EnemyTargetPriority)), Does.Contain("Core"), "EnemyTargetPriority 必须直接表达 Core，不能给啃噬者写目录外特例。");
            Assert.That(EnemyCatalog.Gnawer.TargetPriority.ToString(), Is.EqualTo("Core"));
            Assert.That(EnemyCatalog.CrystalBeast.TargetPriority.ToString(), Is.EqualTo("Walls"));
            Assert.That(EnemyCatalog.Howler.TargetPriority.ToString(), Is.EqualTo("Production"));
        }

        private static MemberTarget RequireCampaign()
        {
            Type catalogType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("WasteCity.Combat.CampaignWaveCatalog", false))
                .FirstOrDefault(type => type != null);
            Assert.That(catalogType, Is.Not.Null, "IDEA-0017 RED：缺少 WasteCity.Combat.CampaignWaveCatalog 正式目录。");

            var staticTarget = new MemberTarget(catalogType, null);
            object staticId;
            if (TryReadMember(staticTarget, out staticId, "Id", "CampaignId") && ReadStableText(staticId) == CampaignId)
                return staticTarget;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            var candidates = new List<object>();
            candidates.AddRange(catalogType.GetFields(flags).Select(field => field.GetValue(null)));
            candidates.AddRange(catalogType.GetProperties(flags).Where(property => property.GetIndexParameters().Length == 0).Select(property => property.GetValue(null, null)));

            foreach (object candidate in candidates.SelectMany(Flatten))
            {
                if (candidate == null) continue;
                object id;
                if (TryReadMember(new MemberTarget(candidate.GetType(), candidate), out id, "Id", "CampaignId") && ReadStableText(id) == CampaignId)
                    return new MemberTarget(candidate.GetType(), candidate);
            }

            Assert.Fail("IDEA-0017 RED：CampaignWaveCatalog 中缺少稳定 ID " + CampaignId + " 的正式战役定义。");
            return default(MemberTarget);
        }

        private static IEnumerable<object> Flatten(object value)
        {
            if (value == null) yield break;
            var enumerable = value as IEnumerable;
            if (enumerable == null || value is string)
            {
                yield return value;
                yield break;
            }

            foreach (object item in enumerable) yield return item;
        }

        private static Dictionary<string, int> ReadComposition(object wave)
        {
            IList<object> entries = ReadList(ReadRequiredMember(wave, "Entries", "Composition"));
            var result = new Dictionary<string, int>();
            foreach (object entry in entries)
            {
                string key = NormalizeEnemy(ReadRequiredMember(entry, "Archetype", "EnemyArchetype", "EnemyId", "Enemy", "Key"));
                int count = Convert.ToInt32(ReadRequiredMember(entry, "Count", "Amount", "Value"));
                Assert.That(result.ContainsKey(key), Is.False, "同一波不得重复定义敌人目录项：" + key);
                result.Add(key, count);
            }
            return result;
        }

        private static int CountFor(IDictionary<string, int> composition, string enemy)
        {
            int count;
            return composition.TryGetValue(enemy, out count) ? count : 0;
        }

        private static string NormalizeEnemy(object value)
        {
            object nested;
            if (!(value is string) && !value.GetType().IsEnum &&
                TryReadMember(new MemberTarget(value.GetType(), value), out nested, "Id", "Archetype", "EnemyId"))
                value = nested;

            string text = ReadStableText(value) ?? string.Empty;
            if (text.IndexOf("gnawer", StringComparison.OrdinalIgnoreCase) >= 0) return "Gnawer";
            if (text.IndexOf("crystal-beast", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("CrystalBeast", StringComparison.OrdinalIgnoreCase) >= 0) return "CrystalBeast";
            if (text.IndexOf("howler", StringComparison.OrdinalIgnoreCase) >= 0) return "Howler";
            if (text.IndexOf("burrower", StringComparison.OrdinalIgnoreCase) >= 0) return "Burrower";
            if (text.IndexOf("broodmother", StringComparison.OrdinalIgnoreCase) >= 0) return "CrystalBroodmother";
            if (text.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0) return "Boss";
            return text;
        }

        private static string[] ReadDirections(object wave)
        {
            object raw = ReadRequiredMember(wave, "Directions", "SpawnDirections");
            var values = new List<string>();
            foreach (object value in Flatten(raw))
            {
                string text = ReadStableText(value) ?? string.Empty;
                values.AddRange(text.Split(new[] { ',', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(NormalizeDirection));
            }
            return values.ToArray();
        }

        private static string NormalizeDirection(string value)
        {
            if (value.Equals("E", StringComparison.OrdinalIgnoreCase) || value.Equals("East", StringComparison.OrdinalIgnoreCase) || value == "东") return "East";
            if (value.Equals("N", StringComparison.OrdinalIgnoreCase) || value.Equals("North", StringComparison.OrdinalIgnoreCase) || value == "北") return "North";
            if (value.Equals("S", StringComparison.OrdinalIgnoreCase) || value.Equals("South", StringComparison.OrdinalIgnoreCase) || value == "南") return "South";
            if (value.Equals("W", StringComparison.OrdinalIgnoreCase) || value.Equals("West", StringComparison.OrdinalIgnoreCase) || value == "西") return "West";
            return value;
        }

        private static IList<object> ReadList(object value)
        {
            var enumerable = value as IEnumerable;
            Assert.That(enumerable, Is.Not.Null, "目录集合必须实现 IEnumerable。");
            return enumerable.Cast<object>().ToList();
        }

        private static int ReadInt(object target, params string[] names)
        {
            return Convert.ToInt32(ReadRequiredMember(target, names));
        }

        private static float ReadFloat(object target, params string[] names)
        {
            return Convert.ToSingle(ReadRequiredMember(target, names));
        }

        private static object ReadRequiredMember(object target, params string[] names)
        {
            Assert.That(target, Is.Not.Null, "目录查询返回了 null。缺少正式配置：" + string.Join("/", names));
            return ReadRequiredMember(new MemberTarget(target.GetType(), target), names);
        }

        private static object ReadRequiredMember(MemberTarget target, params string[] names)
        {
            object value;
            if (TryReadMember(target, out value, names)) return value;
            Assert.Fail(target.Type.FullName + " 缺少正式目录字段：" + string.Join("/", names));
            return null;
        }

        private static bool TryReadMember(MemberTarget target, out object value, params string[] names)
        {
            BindingFlags flags = BindingFlags.Public | (target.Instance == null ? BindingFlags.Static : BindingFlags.Instance);
            foreach (string name in names)
            {
                PropertyInfo property = target.Type.GetProperty(name, flags | BindingFlags.IgnoreCase);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    value = property.GetValue(target.Instance, null);
                    return true;
                }

                FieldInfo field = target.Type.GetField(name, flags | BindingFlags.IgnoreCase);
                if (field != null)
                {
                    value = field.GetValue(target.Instance);
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static string ReadStableText(object value)
        {
            if (value == null) return null;
            var text = value as string;
            if (text != null) return text;

            object nested;
            if (TryReadMember(new MemberTarget(value.GetType(), value), out nested, "Value")) return nested as string ?? nested.ToString();
            return value.ToString();
        }

        private struct MemberTarget
        {
            public readonly Type Type;
            public readonly object Instance;
            public MemberTarget(Type type, object instance) { Type = type; Instance = instance; }
        }
    }
}
