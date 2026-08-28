using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WasteCity.Tests
{
    public sealed class Production2DCivilizationExpansionVisualPipelineTests
    {
        private const string ManifestPath =
            "Docs/Art/IDEA-0023/Manifests/" +
            "idea-0023-civilization-expansion-visual-assets.json";
        private const string UnitImporterType =
            "WasteCity.Editor.Production2DUnitImportPolicy, WasteCity.Editor";
        private const string VisualClassType =
            "WasteCity.Graybox3D.Production2DVisualClass, " +
            "WasteCity.Graybox3D";
        private const string VisualBuilderType =
            "WasteCity.Editor.Production2DVisualCatalogBuilder, " +
            "WasteCity.Editor";

        private static readonly ExpectedEntry[] Expected =
        {
            Unit(
                "cultivation.unit.combat-puppet",
                "战斗傀儡",
                "unit-combat-puppet",
                "灵铁骨架与合金关节组成可持续维护的傀儡道兵。"),
            Unit(
                "biological.unit.bred-behemoth",
                "培育巨兽",
                "unit-bred-behemoth",
                "培育巨兽以厚重生物壳承担单城市军队的正面冲击。"),
            Unit(
                "fusion.unit.psionic-mech",
                "灵能机甲",
                "unit-psionic-mech",
                "灵能机甲把控制芯片与精神护盾封装进敏捷机械躯体。"),
            Unit(
                "fusion.unit.bio-mechanical-behemoth",
                "半机械巨兽",
                "unit-bio-mechanical-behemoth",
                "半机械巨兽用骨钢承力结构约束活性组织与生物武器。"),
            Character(
                "core.character.lin-xi",
                "林溪",
                "character-lin-xi",
                "林溪以灵能研究和城市管理维持多城知识网络。"),
            Character(
                "core.character.han-gu",
                "韩骨",
                "character-han-gu",
                "韩骨是重视纪律与远征生存率的守备指挥者。"),
            Marker(
                "core.world-marker.secondary-city",
                "次城世界标记",
                "world-marker-secondary-city",
                1024, 1024, 256, 256,
                new[] { .08f, .08f, .84f, .84f },
                "次城标记用于区别可查看、可接管的第二座城市。"),
            Marker(
                "core.world-marker.outpost",
                "前哨世界标记",
                "world-marker-outpost",
                1024, 1024, 256, 256,
                new[] { .08f, .08f, .84f, .84f },
                "前哨标记以轻型信标轮廓表达通信与补给节点。"),
            Marker(
                "core.world-marker.convoy",
                "运输队世界标记",
                "world-marker-convoy",
                1024, 768, 256, 192,
                new[] { .08f, .10f, .84f, .80f },
                "运输队标记显示在途货物载体，路径与风险由运行时表达。"),
            Ui(
                "core.ui.tab.army",
                "军队页签图标",
                "ui-tab-army",
                "军队页签以小队编制轮廓进入制造、命令与远征信息。"),
            Ui(
                "core.ui.tab.world",
                "世界页签图标",
                "ui-tab-world",
                "世界页签以多节点网络轮廓进入城市、前哨与运输信息。"),
            Ui(
                "core.ui.tab.politics",
                "政务页签图标",
                "ui-tab-politics",
                "政务页签以议会与联络轮廓进入角色、继承和外交信息。"),
            Ui("core.ui.status.guard", "守卫状态徽记",
                "ui-status-guard", "守卫徽记表示小队正在保护指定区域。"),
            Ui("core.ui.status.follow", "跟随状态徽记",
                "ui-status-follow", "跟随徽记表示小队正在伴随领袖行动。"),
            Ui("core.ui.status.expedition", "远征状态徽记",
                "ui-status-expedition", "远征徽记表示小队已离城执行远征。"),
            Ui("core.ui.status.retreat", "撤退状态徽记",
                "ui-status-retreat", "撤退徽记表示小队正在返城脱离风险。"),
            Ui("core.ui.status.transport", "运输状态徽记",
                "ui-status-transport", "运输徽记表示实体运输队和在途货物。"),
            Ui("core.ui.status.communication", "通信状态徽记",
                "ui-status-communication", "通信徽记表示城市或前哨的联络状态。"),
            Ui("core.ui.status.loyalty", "忠诚状态徽记",
                "ui-status-loyalty", "忠诚徽记表示人物与派系的支持稳定度。"),
            Ui("core.ui.status.rescue", "救援状态徽记",
                "ui-status-rescue", "救援徽记表示倒地角色的限时救援。"),
        };

        [Test]
        public void IDEA0023_IncrementalManifestOwnsExactTwentyReviewedMappings()
        {
            VisualManifest manifest = LoadManifest();

            Assert.That(manifest.requirementId, Is.EqualTo("IDEA-0023"));
            Assert.That(manifest.unityVersion, Is.EqualTo("2022.3.62f1"));
            Assert.That(manifest.entries, Has.Length.EqualTo(20));
            Assert.That(manifest.entries.Select(value => value.contentId),
                Is.EqualTo(Expected.Select(value => value.Id)));
            Assert.That(manifest.entries.Select(value => value.contentId)
                    .Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(20));
            Assert.That(manifest.integrity, Has.Length.EqualTo(20));

            for (var index = 0; index < Expected.Length; index++)
            {
                ExpectedEntry expected = Expected[index];
                VisualEntry actual = manifest.entries[index];
                Assert.That(actual.displayNameZh,
                    Is.EqualTo(expected.DisplayName), expected.Id);
                Assert.That(actual.visualClass,
                    Is.EqualTo(expected.VisualClass), expected.Id);
                Assert.That(actual.unitySpritePath,
                    Is.EqualTo(expected.DeliveryPath), expected.Id);
                Assert.That(actual.sourceAssetPath,
                    Is.EqualTo(expected.MasterPath), expected.Id);
                Assert.That(actual.masterSizePx,
                    Is.EqualTo(expected.MasterSize), expected.Id);
                Assert.That(actual.deliverySizePx,
                    Is.EqualTo(expected.DeliverySize), expected.Id);
                Assert.That(actual.safeAreaNormalized,
                    Is.EqualTo(expected.SafeArea), expected.Id);
                Assert.That(actual.loreBriefZh,
                    Is.EqualTo(expected.LoreBrief), expected.Id);
                Assert.That(actual.useSummaryZh, Is.Not.Empty, expected.Id);
                Assert.That(actual.visualKeywordsZh, Is.Not.Empty, expected.Id);
                Assert.That(actual.forbiddenElementsZh,
                    Does.Contain("背景").And.Contain("文字"), expected.Id);
                Assert.That(actual.promptSummaryZh, Is.Not.Empty, expected.Id);
                Assert.That(actual.reviewState,
                    Is.EqualTo("integrated"), expected.Id);
                VisualIntegrity integrity = manifest.integrity.Single(
                    value => value.contentId == actual.contentId);
                Assert.That(integrity.unityGuid,
                    Is.EqualTo(AssetDatabase.AssetPathToGUID(
                        actual.unitySpritePath)), expected.Id);
                Assert.That(integrity.sourceSha256,
                    Is.EqualTo(Sha256(actual.sourceAssetPath)), expected.Id);
                Assert.That(integrity.deliverySha256,
                    Is.EqualTo(Sha256(actual.unitySpritePath)), expected.Id);
                Assert.That(integrity.pivotNormalized,
                    Is.EqualTo(new[] { .5f, .5f }), expected.Id);
                Assert.That(integrity.borderPx,
                    Is.EqualTo(new[] { 0, 0, 0, 0 }), expected.Id);
            }

            Assert.That(manifest.entries.Count(value =>
                value.visualClass == "unit"), Is.EqualTo(4));
            Assert.That(manifest.entries.Count(value =>
                value.visualClass == "character"), Is.EqualTo(2));
            Assert.That(manifest.entries.Count(value =>
                value.visualClass == "world-marker"), Is.EqualTo(3));
            Assert.That(manifest.entries.Count(value =>
                value.visualClass == "ui"), Is.EqualTo(11));
        }

        [Test]
        public void IDEA0023_MastersAndDeliveriesUseTrueAlphaExactSizesAndSafeArea()
        {
            VisualManifest manifest = LoadManifest();
            foreach (VisualEntry entry in manifest.entries)
            {
                AssertPng(entry.sourceAssetPath, entry.masterSizePx,
                    entry.contentId + " master");
                AssertPng(entry.unitySpritePath, entry.deliverySizePx,
                    entry.contentId + " delivery");
                AssertSafeArea(entry);
            }
        }

        [Test]
        public void IDEA0023_UnitImporterExistsAndOwnsOnlyTopLevelUnitPngs()
        {
            Type policy = Type.GetType(UnitImporterType);
            Assert.That(policy, Is.Not.Null,
                "IDEA-0023 requires an isolated Production2D unit importer.");
            FieldInfo root = policy.GetField(
                "Root",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo isOwned = policy.GetMethod(
                "IsOwnedPng",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.GetValue(null),
                Is.EqualTo("Assets/_Game/Art/Production2D/Units/"));
            Assert.That(isOwned, Is.Not.Null);
            Assert.That(isOwned.Invoke(null, new object[]
            {
                "Assets/_Game/Art/Production2D/Units/unit-combat-puppet.png",
            }), Is.True);
            Assert.That(isOwned.Invoke(null, new object[]
            {
                "Assets/_Game/Art/Production2D/UnitsExtra/unit-x.png",
            }), Is.False);
            Assert.That(isOwned.Invoke(null, new object[]
            {
                "Assets/_Game/Art/Production2D/Units/Nested/unit-x.png",
            }), Is.False);
            Assert.That(isOwned.Invoke(null, new object[]
            {
                "Assets/_Game/Art/Production2D/Characters/character-lin-xi.png",
            }), Is.False);
            Assert.That(isOwned.Invoke(null, new object[]
            {
                "Assets/_Game/Art/Production2D/Units/unit-combat-puppet.jpg",
            }), Is.False);

            foreach (ExpectedEntry expected in Expected.Where(value =>
                         value.VisualClass == "unit"))
            {
                var importer = AssetImporter.GetAtPath(expected.DeliveryPath)
                    as TextureImporter;
                Assert.That(importer, Is.Not.Null, expected.Id);
                Assert.That(importer.textureType,
                    Is.EqualTo(TextureImporterType.Sprite), expected.Id);
                Assert.That(importer.spriteImportMode,
                    Is.EqualTo(SpriteImportMode.Single), expected.Id);
                Assert.That(importer.alphaSource,
                    Is.EqualTo(TextureImporterAlphaSource.FromInput),
                    expected.Id);
                Assert.That(importer.alphaIsTransparency, Is.True, expected.Id);
                Assert.That(importer.mipmapEnabled, Is.False, expected.Id);
                Assert.That(importer.wrapMode,
                    Is.EqualTo(TextureWrapMode.Clamp), expected.Id);
                Assert.That(importer.filterMode,
                    Is.EqualTo(FilterMode.Bilinear), expected.Id);
                Assert.That(importer.maxTextureSize, Is.EqualTo(512),
                    expected.Id);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteMeshType,
                    Is.EqualTo(SpriteMeshType.FullRect), expected.Id);
            }
        }

        [Test]
        public void IDEA0023_AllDeliveriesUseApprovedSpriteImportContract()
        {
            foreach (ExpectedEntry expected in Expected)
            {
                var importer = AssetImporter.GetAtPath(expected.DeliveryPath)
                    as TextureImporter;
                Assert.That(importer, Is.Not.Null, expected.Id);
                Assert.That(importer.textureType,
                    Is.EqualTo(TextureImporterType.Sprite), expected.Id);
                Assert.That(importer.spriteImportMode,
                    Is.EqualTo(SpriteImportMode.Single), expected.Id);
                Assert.That(importer.alphaSource,
                    Is.EqualTo(TextureImporterAlphaSource.FromInput),
                    expected.Id);
                Assert.That(importer.alphaIsTransparency, Is.True,
                    expected.Id);
                Assert.That(importer.sRGBTexture, Is.True, expected.Id);
                Assert.That(importer.mipmapEnabled, Is.False, expected.Id);
                Assert.That(importer.wrapMode,
                    Is.EqualTo(TextureWrapMode.Clamp), expected.Id);
                Assert.That(importer.filterMode,
                    Is.EqualTo(FilterMode.Bilinear), expected.Id);
                Assert.That(importer.crunchedCompression, Is.False,
                    expected.Id);
                Assert.That(importer.textureCompression,
                    Is.EqualTo(TextureImporterCompression.CompressedHQ),
                    expected.Id);
                Assert.That(importer.isReadable, Is.False, expected.Id);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f),
                    expected.Id);
                Assert.That(importer.maxTextureSize,
                    Is.EqualTo(expected.DeliverySize.Max() > 256 ? 512 : 256),
                    expected.Id);
                Assert.That(importer.spritePivot,
                    Is.EqualTo(Vector2.one * .5f), expected.Id);
                Assert.That(importer.spriteBorder,
                    Is.EqualTo(Vector4.zero), expected.Id);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteMeshType,
                    Is.EqualTo(SpriteMeshType.FullRect), expected.Id);
            }
        }

        [Test]
        public void IDEA0023_UnitVisualClassIsAppendedWithoutRenumberingOldValues()
        {
            Type visualClass = Type.GetType(VisualClassType);
            Assert.That(visualClass, Is.Not.Null);
            string[] names = Enum.GetNames(visualClass);
            Assert.That(names, Is.EqualTo(new[]
            {
                "Item",
                "Technology",
                "Building",
                "Ui",
                "Character",
                "WorldMarker",
                "Unit",
            }));
            object unit = Enum.Parse(visualClass, "Unit");
            Assert.That(Convert.ToInt32(unit), Is.EqualTo(6));
        }

        [Test]
        public void IDEA0023_UnifiedCatalogEndsAtExactOneHundredFortyEntries()
        {
            Type builder = Type.GetType(VisualBuilderType);
            Assert.That(builder, Is.Not.Null);
            FieldInfo expectedCount = builder.GetField(
                "ExpectedVisualCount",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(expectedCount, Is.Not.Null);
            Assert.That(expectedCount.GetValue(null), Is.EqualTo(140));
            MethodInfo create = builder.GetMethod(
                "CreateExpectedVisualEntries",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(create, Is.Not.Null);
            var entries = (IEnumerable)create.Invoke(null, null);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var total = 0;
            foreach (object entry in entries)
            {
                Type type = entry.GetType();
                string visualClass = type.GetProperty("VisualClass")
                    .GetValue(entry).ToString();
                string contentId = (string)type.GetProperty("ContentId")
                    .GetValue(entry);
                string variant = (string)type.GetProperty("Variant")
                    .GetValue(entry);
                counts.TryGetValue(visualClass, out int before);
                counts[visualClass] = before + 1;
                Assert.That(keys.Add(
                    visualClass + "|" + contentId + "|" + variant),
                    Is.True);
                total++;
            }

            Assert.That(total, Is.EqualTo(140));
            Assert.That(counts["Item"], Is.EqualTo(31));
            Assert.That(counts["Technology"], Is.EqualTo(44));
            Assert.That(counts["Building"], Is.EqualTo(35));
            Assert.That(counts["Unit"], Is.EqualTo(4));
            Assert.That(counts["Character"], Is.EqualTo(3));
            Assert.That(counts["WorldMarker"], Is.EqualTo(5));
            Assert.That(counts["Ui"], Is.EqualTo(18));
        }

        private static string Sha256(string projectPath)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(File.ReadAllBytes(
                Absolute(projectPath)));
            return string.Concat(hash.Select(value => value.ToString("x2")));
        }

        private static VisualManifest LoadManifest()
        {
            string absolute = Absolute(ManifestPath);
            Assert.That(File.Exists(absolute), Is.True, ManifestPath);
            VisualManifest manifest = JsonUtility.FromJson<VisualManifest>(
                File.ReadAllText(absolute));
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.entries, Is.Not.Null);
            return manifest;
        }

        private static void AssertPng(
            string projectPath,
            IReadOnlyList<int> size,
            string context)
        {
            string absolute = Absolute(projectPath);
            Assert.That(File.Exists(absolute), Is.True, projectPath);
            byte[] bytes = File.ReadAllBytes(absolute);
            Assert.That(bytes.Length, Is.GreaterThan(25), context);
            Assert.That(bytes[25], Is.EqualTo(6),
                context + " must be PNG RGBA color type 6.");
            Texture2D texture = LoadPng(projectPath);
            try
            {
                Assert.That(texture.width, Is.EqualTo(size[0]), context);
                Assert.That(texture.height, Is.EqualTo(size[1]), context);
                Assert.That(texture.GetPixel(0, 0).a, Is.Zero, context);
                Assert.That(texture.GetPixel(texture.width - 1, 0).a,
                    Is.Zero, context);
                Assert.That(texture.GetPixel(0, texture.height - 1).a,
                    Is.Zero, context);
                Assert.That(texture.GetPixel(
                    texture.width - 1,
                    texture.height - 1).a, Is.Zero, context);
                Assert.That(texture.GetPixels32().Any(value => value.a >= 128),
                    Is.True, context);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void AssertSafeArea(VisualEntry entry)
        {
            Texture2D texture = LoadPng(entry.unitySpritePath);
            try
            {
                RectInt opaque = OpaqueBounds(texture);
                Assert.That(opaque.width, Is.GreaterThan(0), entry.contentId);
                Assert.That(opaque.height, Is.GreaterThan(0), entry.contentId);
                float[] safe = entry.safeAreaNormalized;
                Assert.That(safe, Has.Length.EqualTo(4), entry.contentId);
                int minX = Mathf.FloorToInt(safe[0] * texture.width);
                int minY = Mathf.FloorToInt(safe[1] * texture.height);
                int maxX = Mathf.CeilToInt(
                    (safe[0] + safe[2]) * texture.width) - 1;
                int maxY = Mathf.CeilToInt(
                    (safe[1] + safe[3]) * texture.height) - 1;
                Assert.That(opaque.xMin, Is.GreaterThanOrEqualTo(minX),
                    entry.contentId);
                Assert.That(opaque.yMin, Is.GreaterThanOrEqualTo(minY),
                    entry.contentId);
                Assert.That(opaque.xMax - 1, Is.LessThanOrEqualTo(maxX),
                    entry.contentId);
                Assert.That(opaque.yMax - 1, Is.LessThanOrEqualTo(maxY),
                    entry.contentId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Texture2D LoadPng(string projectPath)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(texture.LoadImage(
                File.ReadAllBytes(Absolute(projectPath)), false), Is.True,
                projectPath);
            return texture;
        }

        private static RectInt OpaqueBounds(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            int minX = texture.width;
            int minY = texture.height;
            int maxX = -1;
            int maxY = -1;
            for (var y = 0; y < texture.height; y++)
            for (var x = 0; x < texture.width; x++)
            {
                if (pixels[y * texture.width + x].a < 8) continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
            return maxX < minX
                ? new RectInt()
                : new RectInt(
                    minX,
                    minY,
                    maxX - minX + 1,
                    maxY - minY + 1);
        }

        private static string Absolute(string projectPath)
        {
            return Path.Combine(ProjectRoot(), projectPath);
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName
                .Replace('\\', '/');
        }

        private static ExpectedEntry Unit(
            string id,
            string displayName,
            string slug,
            string lore)
        {
            return Entry(
                id, displayName, "unit", "Units", slug,
                2048, 2048, 512, 512,
                new[] { .10f, .10f, .80f, .80f }, lore);
        }

        private static ExpectedEntry Character(
            string id,
            string displayName,
            string slug,
            string lore)
        {
            return Entry(
                id, displayName, "character", "Characters", slug,
                2048, 2048, 512, 512,
                new[] { .10f, .10f, .80f, .80f }, lore);
        }

        private static ExpectedEntry Marker(
            string id,
            string displayName,
            string slug,
            int masterWidth,
            int masterHeight,
            int deliveryWidth,
            int deliveryHeight,
            float[] safe,
            string lore)
        {
            return Entry(
                id, displayName, "world-marker", "WorldMarkers", slug,
                masterWidth, masterHeight, deliveryWidth, deliveryHeight,
                safe, lore);
        }

        private static ExpectedEntry Ui(
            string id,
            string displayName,
            string slug,
            string lore)
        {
            return Entry(
                id, displayName, "ui", "UI", slug,
                1024, 1024, 256, 256,
                new[] { .125f, .125f, .75f, .75f }, lore);
        }

        private static ExpectedEntry Entry(
            string id,
            string displayName,
            string visualClass,
            string folder,
            string slug,
            int masterWidth,
            int masterHeight,
            int deliveryWidth,
            int deliveryHeight,
            float[] safe,
            string lore)
        {
            return new ExpectedEntry(
                id,
                displayName,
                visualClass,
                "Docs/Art/IDEA-0023/Source/" + folder + "/" + slug +
                "-master-v1.png",
                "Assets/_Game/Art/Production2D/" + folder + "/" + slug +
                ".png",
                new[] { masterWidth, masterHeight },
                new[] { deliveryWidth, deliveryHeight },
                safe,
                lore);
        }

        private sealed class ExpectedEntry
        {
            public ExpectedEntry(
                string id,
                string displayName,
                string visualClass,
                string masterPath,
                string deliveryPath,
                int[] masterSize,
                int[] deliverySize,
                float[] safeArea,
                string loreBrief)
            {
                Id = id;
                DisplayName = displayName;
                VisualClass = visualClass;
                MasterPath = masterPath;
                DeliveryPath = deliveryPath;
                MasterSize = masterSize;
                DeliverySize = deliverySize;
                SafeArea = safeArea;
                LoreBrief = loreBrief;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string VisualClass { get; }
            public string MasterPath { get; }
            public string DeliveryPath { get; }
            public int[] MasterSize { get; }
            public int[] DeliverySize { get; }
            public float[] SafeArea { get; }
            public string LoreBrief { get; }
        }

        [Serializable]
        private sealed class VisualManifest
        {
            public string requirementId;
            public string unityVersion;
            public VisualEntry[] entries;
            public VisualIntegrity[] integrity;
        }

        [Serializable]
        private sealed class VisualIntegrity
        {
            public string contentId;
            public string unityGuid;
            public string sourceSha256;
            public string deliverySha256;
            public float[] pivotNormalized;
            public int[] borderPx;
        }

        [Serializable]
        private sealed class VisualEntry
        {
            public string contentId;
            public string displayNameZh;
            public string visualClass;
            public string useSummaryZh;
            public string loreBriefZh;
            public string visualKeywordsZh;
            public string forbiddenElementsZh;
            public string promptSummaryZh;
            public string sourceAssetPath;
            public string unitySpritePath;
            public int[] masterSizePx;
            public int[] deliverySizePx;
            public float[] safeAreaNormalized;
            public string reviewState;
        }
    }
}
