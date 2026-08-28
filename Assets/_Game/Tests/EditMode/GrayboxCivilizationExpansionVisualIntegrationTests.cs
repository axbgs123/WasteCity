using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using WasteCity.Combat;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.World.CivilizationExpansion;

namespace WasteCity.Tests
{
    public sealed class GrayboxCivilizationExpansionVisualIntegrationTests
    {
        private const string PresenterTypeName =
            "WasteCity.Graybox3D.Building." +
            "GrayboxCivilizationExpansionVisualPresenter3D";

        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        [Test]
        public void IDEA0023_SharedMnpViewUsesFormalFramesButtonsDividerAndTabs()
        {
            GameObject canvasObject = Track(new GameObject(
                "IDEA0023.Canvas",
                typeof(RectTransform),
                typeof(Canvas)));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            GrayboxCivilizationExpansionView3D view =
                canvasObject.AddComponent<
                    GrayboxCivilizationExpansionView3D>();
            view.Configure(canvas);
            view.Open(GrayboxCivilizationExpansionPage3D.Army);

            Dictionary<string, Image> images = ImagesByName(canvasObject);
            AssertFormalSprite(
                images,
                "CivilizationExpansion.Panel",
                "core.ui.frame.primary-panel");
            AssertFormalSprite(
                images,
                "Summary",
                "core.ui.frame.secondary-card");
            AssertFormalSprite(
                images,
                "Details",
                "core.ui.frame.secondary-card");
            AssertFormalSprite(
                images,
                "Primary",
                "core.ui.control.primary-button");
            AssertFormalSprite(
                images,
                "Secondary",
                "core.ui.control.primary-button");
            AssertFormalSprite(
                images,
                "Tertiary",
                "core.ui.control.primary-button");
            AssertFormalSprite(
                images,
                "CivilizationExpansion.TerminalDivider",
                "core.ui.divider.terminal-horizontal");
            AssertFormalSprite(
                images,
                "CivilizationExpansion.Tab.Army.Icon",
                "core.ui.tab.army");
            AssertFormalSprite(
                images,
                "CivilizationExpansion.Tab.World.Icon",
                "core.ui.tab.world");
            AssertFormalSprite(
                images,
                "CivilizationExpansion.Tab.Politics.Icon",
                "core.ui.tab.politics");

            Transform sharedPanel = FindTransform(
                canvasObject.transform,
                "CivilizationExpansion.Panel");
            Assert.That(sharedPanel, Is.Not.Null);
            int sharedPanelId = sharedPanel.gameObject.GetInstanceID();
            view.Open(GrayboxCivilizationExpansionPage3D.World);
            view.Open(GrayboxCivilizationExpansionPage3D.Politics);
            Assert.That(FindTransform(
                    canvasObject.transform,
                    "CivilizationExpansion.Panel").gameObject.GetInstanceID(),
                Is.EqualTo(sharedPanelId),
                "M/N/P must skin one shared View instead of rebuilding three truths.");

            view.Open(GrayboxCivilizationExpansionPage3D.Army);
            Color armySelected = images[
                "CivilizationExpansion.Tab.Army.Icon"].color;
            Color worldIdle = images[
                "CivilizationExpansion.Tab.World.Icon"].color;
            Assert.That(armySelected, Is.Not.EqualTo(worldIdle));
            view.Open(GrayboxCivilizationExpansionPage3D.World);
            Assert.That(images["CivilizationExpansion.Tab.World.Icon"].color,
                Is.EqualTo(armySelected));
            Assert.That(images["CivilizationExpansion.Tab.Army.Icon"].color,
                Is.EqualTo(worldIdle));
        }

        [Test]
        public void IDEA0023_VisualPresenterMapsStableWorldEntitiesAndPrimaryUnit()
        {
            Assert.That(
                Enum.GetNames(typeof(Production2DVisualClass)).Last(),
                Is.EqualTo("Unit"),
                "Unit must be appended without renumbering the six existing classes.");

            var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal)
            {
                ["WorldMarker|core.world-marker.secondary-city"] =
                    CreateSprite("secondary-city"),
                ["WorldMarker|core.world-marker.outpost"] =
                    CreateSprite("outpost"),
                ["WorldMarker|core.world-marker.convoy"] =
                    CreateSprite("convoy"),
                ["Unit|" + ArmyUnitCatalog.CombatPuppetId] =
                    CreateSprite("combat-puppet"),
            };
            object presenter = CreatePresenter((visualClass, contentId) =>
            {
                sprites.TryGetValue(
                    visualClass + "|" + contentId,
                    out Sprite sprite);
                return sprite;
            });

            AssertMarker(
                presenter,
                WorldLayerCatalog.SecondaryCity.Id,
                null,
                "WorldMarker",
                "core.world-marker.secondary-city",
                sprites["WorldMarker|core.world-marker.secondary-city"]);
            AssertMarker(
                presenter,
                WorldLayerCatalog.Outpost.Id,
                null,
                "WorldMarker",
                "core.world-marker.outpost",
                sprites["WorldMarker|core.world-marker.outpost"]);
            AssertMarker(
                presenter,
                "core.convoy.000001",
                null,
                "WorldMarker",
                "core.world-marker.convoy",
                sprites["WorldMarker|core.world-marker.convoy"]);
            AssertMarker(
                presenter,
                SingleCityArmyModel.DefaultSquadId,
                ArmyUnitCatalog.CombatPuppetId,
                "Unit",
                ArmyUnitCatalog.CombatPuppetId,
                sprites["Unit|" + ArmyUnitCatalog.CombatPuppetId]);
        }

        [Test]
        public void IDEA0023_SharedViewShowsPageContentSpritesWithoutBakingState()
        {
            GameObject canvasObject = Track(new GameObject(
                "IDEA0023.ContentCanvas",
                typeof(RectTransform),
                typeof(Canvas)));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            GrayboxCivilizationExpansionView3D view =
                canvasObject.AddComponent<
                    GrayboxCivilizationExpansionView3D>();
            view.Configure(canvas);

            view.Open(GrayboxCivilizationExpansionPage3D.Army);
            Dictionary<string, Image> images = ImagesByName(canvasObject);
            AssertContentSprite(images, 0, Production2DVisualClass.Unit,
                ArmyUnitCatalog.CombatPuppetId);
            AssertContentSprite(images, 1, Production2DVisualClass.Unit,
                ArmyUnitCatalog.BredBehemothId);
            AssertContentSprite(images, 2, Production2DVisualClass.Unit,
                ArmyUnitCatalog.PsionicMechId);
            AssertContentSprite(images, 3, Production2DVisualClass.Unit,
                ArmyUnitCatalog.BioMechanicalBehemothId);

            view.Open(GrayboxCivilizationExpansionPage3D.World);
            AssertContentSprite(images, 0,
                Production2DVisualClass.WorldMarker,
                "core.world-marker.secondary-city");
            AssertContentSprite(images, 1,
                Production2DVisualClass.WorldMarker,
                "core.world-marker.outpost");
            AssertContentSprite(images, 2,
                Production2DVisualClass.WorldMarker,
                "core.world-marker.convoy");
            Assert.That(images["CivilizationExpansion.PageVisual.3"]
                .gameObject.activeSelf, Is.False);

            view.Open(GrayboxCivilizationExpansionPage3D.Politics);
            AssertContentSprite(images, 0,
                Production2DVisualClass.Character,
                "core.character.cen-jin");
            AssertContentSprite(images, 1,
                Production2DVisualClass.Character,
                "core.character.lin-xi");
            AssertContentSprite(images, 2,
                Production2DVisualClass.Character,
                "core.character.han-gu");
            Assert.That(images["CivilizationExpansion.PageVisual.3"]
                .gameObject.activeSelf, Is.False);
        }

        [Test]
        public void IDEA0023_MarkerDisplayScaleAnchorAndHeightComeFromOneProfile()
        {
            Sprite sprite = CreateSprite("profile-probe");
            object presenter = CreatePresenter((_, __) => sprite);
            object secondary = Describe(
                presenter,
                WorldLayerCatalog.SecondaryCity.Id,
                null);
            object outpost = Describe(
                presenter,
                WorldLayerCatalog.Outpost.Id,
                null);
            object convoy = Describe(
                presenter,
                "core.convoy.000001",
                null);
            object squad = Describe(
                presenter,
                SingleCityArmyModel.DefaultSquadId,
                ArmyUnitCatalog.CombatPuppetId);

            foreach (object value in new[]
                     {
                         secondary,
                         outpost,
                         convoy,
                         squad,
                     })
            {
                Assert.That(Read<float>(value, "WorldScale"),
                    Is.GreaterThan(0f));
                Assert.That(Read<float>(value, "WorldHeight"),
                    Is.GreaterThanOrEqualTo(0f));
                Vector2 anchor = Read<Vector2>(value, "Anchor");
                Assert.That(anchor.x, Is.InRange(0f, 1f));
                Assert.That(anchor.y, Is.InRange(0f, 1f));
                Assert.That(float.IsNaN(anchor.x) || float.IsNaN(anchor.y),
                    Is.False);
            }
            Assert.That(Read<float>(secondary, "WorldScale"),
                Is.GreaterThan(Read<float>(outpost, "WorldScale")));
            Assert.That(Read<float>(outpost, "WorldScale"),
                Is.GreaterThan(Read<float>(convoy, "WorldScale")));
        }

        [Test]
        public void IDEA0023_MissingSpriteUsesProgrammaticFallbackWithoutScaleDrift()
        {
            Sprite sprite = CreateSprite("resolved-outpost");
            object resolvedPresenter = CreatePresenter((_, __) => sprite);
            object missingPresenter = CreatePresenter((_, __) => null);
            object resolved = Describe(
                resolvedPresenter,
                WorldLayerCatalog.Outpost.Id,
                null);
            object missing = Describe(
                missingPresenter,
                WorldLayerCatalog.Outpost.Id,
                null);

            Assert.That(Read<Sprite>(resolved, "Sprite"), Is.SameAs(sprite));
            Assert.That(Read<bool>(resolved, "UsesProgrammaticFallback"),
                Is.False);
            Assert.That(Read<Sprite>(missing, "Sprite"), Is.Null);
            Assert.That(Read<bool>(missing, "UsesProgrammaticFallback"),
                Is.True);
            Assert.That(Read<float>(missing, "WorldScale"),
                Is.EqualTo(Read<float>(resolved, "WorldScale")));
            Assert.That(Read<float>(missing, "WorldHeight"),
                Is.EqualTo(Read<float>(resolved, "WorldHeight")));
            Assert.That(Read<Vector2>(missing, "Anchor"),
                Is.EqualTo(Read<Vector2>(resolved, "Anchor")));
        }

        [Test]
        public void IDEA0023_PresenterCachesStableVisualResolution()
        {
            Sprite sprite = CreateSprite("cached-outpost");
            var calls = 0;
            object presenter = CreatePresenter((_, __) =>
            {
                calls++;
                return sprite;
            });
            Describe(presenter, WorldLayerCatalog.Outpost.Id, null);
            Describe(presenter, WorldLayerCatalog.Outpost.Id, null);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void IDEA0023_VerticalBillboardKeepsWorldUpAndFacesCameraYaw()
        {
            Type type = typeof(GrayboxCivilizationExpansionView3D).Assembly
                .GetType(PresenterTypeName);
            MethodInfo method = type?.GetMethod(
                "OrientVerticalBillboard",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            GameObject marker = Track(new GameObject("Billboard"));
            marker.transform.position = new Vector3(2f, .5f, 3f);
            var cameraPosition = new Vector3(8f, 14f, -4f);
            method.Invoke(null, new object[]
            {
                marker.transform,
                cameraPosition,
            });
            Assert.That(Vector3.Dot(marker.transform.up, Vector3.up),
                Is.GreaterThan(.999f));
            Vector3 expected = cameraPosition - marker.transform.position;
            expected.y = 0f;
            expected.Normalize();
            Assert.That(Vector3.Dot(marker.transform.forward, expected),
                Is.GreaterThan(.999f));
        }

        [Test]
        public void IDEA0023_PresentationDrivesStatusBadgesWithoutBakedText()
        {
            GameObject canvasObject = Track(new GameObject(
                "IDEA0023.StatusCanvas",
                typeof(RectTransform),
                typeof(Canvas)));
            GrayboxCivilizationExpansionView3D view =
                canvasObject.AddComponent<
                    GrayboxCivilizationExpansionView3D>();
            view.Configure(canvasObject.GetComponent<Canvas>());
            view.Open(GrayboxCivilizationExpansionPage3D.Army);
            ConstructorInfo constructor = typeof(
                    GrayboxCivilizationExpansionPresentation3D)
                .GetConstructors()
                .SingleOrDefault(value =>
                    value.GetParameters().Length == 10);
            Assert.That(constructor, Is.Not.Null,
                "Presentation must accept runtime status visual IDs.");
            var presentation = (GrayboxCivilizationExpansionPresentation3D)
                constructor.Invoke(new object[]
                {
                    "军队", "摘要", "详情",
                    "守卫", true,
                    "跟随", true,
                    "撤退", true,
                    new[]
                    {
                        "core.ui.status.guard",
                        "core.ui.status.expedition",
                    },
                });
            view.Apply(presentation);
            Dictionary<string, Image> images = ImagesByName(canvasObject);
            Assert.That(images["CivilizationExpansion.StatusVisual.0"].sprite,
                Is.SameAs(Production2DVisualCatalog3D.Resolve(
                    Production2DVisualClass.Ui,
                    "core.ui.status.guard")));
            Assert.That(images["CivilizationExpansion.StatusVisual.1"].sprite,
                Is.SameAs(Production2DVisualCatalog3D.Resolve(
                    Production2DVisualClass.Ui,
                    "core.ui.status.expedition")));
            Assert.That(images["CivilizationExpansion.StatusVisual.2"]
                .gameObject.activeSelf, Is.False);
        }

        [Test]
        public void IDEA0023_RuntimeStatesMapToExactStatusVisualIds()
        {
            Type type = typeof(GrayboxCivilizationExpansionView3D).Assembly
                .GetType(PresenterTypeName);
            MethodInfo army = type?.GetMethod("ArmyStatusVisuals",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo world = type?.GetMethod("WorldStatusVisuals",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo politics = type?.GetMethod("PoliticsStatusVisuals",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(army, Is.Not.Null);
            Assert.That(world, Is.Not.Null);
            Assert.That(politics, Is.Not.Null);
            Assert.That((string[])army.Invoke(null, new object[]
                { FriendlySquadCommandType.Guard }),
                Is.EqualTo(new[] { "core.ui.status.guard" }));
            Assert.That((string[])army.Invoke(null, new object[]
                { FriendlySquadCommandType.FollowLeader }),
                Is.EqualTo(new[] { "core.ui.status.follow" }));
            Assert.That((string[])army.Invoke(null, new object[]
                { FriendlySquadCommandType.Expedition }),
                Is.EqualTo(new[] { "core.ui.status.expedition" }));
            Assert.That((string[])army.Invoke(null, new object[]
                { FriendlySquadCommandType.Retreat }),
                Is.EqualTo(new[] { "core.ui.status.retreat" }));
            Assert.That((string[])world.Invoke(null, new object[]
                { true, true }), Is.EqualTo(new[]
                {
                    "core.ui.status.transport",
                    "core.ui.status.communication",
                }));
            Assert.That((string[])world.Invoke(null, new object[]
                { false, false }), Is.Empty);
            Assert.That((string[])politics.Invoke(null, new object[]
                { true }), Is.EqualTo(new[]
                {
                    "core.ui.status.loyalty",
                    "core.ui.status.rescue",
                }));
        }

        private static object CreatePresenter(
            Func<Production2DVisualClass, string, Sprite> resolver)
        {
            Type type = typeof(GrayboxCivilizationExpansionView3D).Assembly
                .GetType(PresenterTypeName);
            Assert.That(type, Is.Not.Null,
                PresenterTypeName + " must centralize all IDEA-0023 visual mappings.");
            ConstructorInfo constructor = type.GetConstructor(new[]
            {
                typeof(Func<Production2DVisualClass, string, Sprite>),
            });
            Assert.That(constructor, Is.Not.Null,
                "Presenter requires an injectable resolver for deterministic fallback tests.");
            return constructor.Invoke(new object[] { resolver });
        }

        private static object Describe(
            object presenter,
            string stableRuntimeId,
            string primaryUnitDefinitionId)
        {
            MethodInfo method = presenter.GetType().GetMethod(
                "DescribeWorldMarker",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            object result = method.Invoke(presenter, new object[]
            {
                stableRuntimeId,
                primaryUnitDefinitionId,
            });
            Assert.That(result, Is.Not.Null, stableRuntimeId);
            return result;
        }

        private static void AssertMarker(
            object presenter,
            string stableRuntimeId,
            string primaryUnitDefinitionId,
            string expectedClass,
            string expectedContentId,
            Sprite expectedSprite)
        {
            object value = Describe(
                presenter,
                stableRuntimeId,
                primaryUnitDefinitionId);
            Assert.That(Read<object>(value, "VisualClass").ToString(),
                Is.EqualTo(expectedClass), stableRuntimeId);
            Assert.That(Read<string>(value, "VisualContentId"),
                Is.EqualTo(expectedContentId), stableRuntimeId);
            Assert.That(Read<Sprite>(value, "Sprite"),
                Is.SameAs(expectedSprite), stableRuntimeId);
            Assert.That(Read<bool>(value, "UsesProgrammaticFallback"),
                Is.False, stableRuntimeId);
        }

        private static T Read<T>(object owner, string propertyName)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                owner.GetType().Name + "." + propertyName);
            object value = property.GetValue(owner);
            if (value == null)
                return default;
            Assert.That(value, Is.AssignableTo<T>());
            return (T)value;
        }

        private void AssertFormalSprite(
            IReadOnlyDictionary<string, Image> images,
            string objectName,
            string visualId)
        {
            Assert.That(images.TryGetValue(objectName, out Image image),
                Is.True, objectName);
            Sprite expected = Production2DVisualCatalog3D.Resolve(
                Production2DVisualClass.Ui,
                visualId);
            Assert.That(expected, Is.Not.Null, visualId);
            Assert.That(image.sprite, Is.SameAs(expected), objectName);
            Assert.That(image.type,
                Is.EqualTo(expected.border.sqrMagnitude > 0f
                    ? Image.Type.Sliced
                    : Image.Type.Simple),
                objectName);
        }

        private static void AssertContentSprite(
            IReadOnlyDictionary<string, Image> images,
            int index,
            Production2DVisualClass visualClass,
            string contentId)
        {
            string name = "CivilizationExpansion.PageVisual." + index;
            Assert.That(images.TryGetValue(name, out Image image), Is.True,
                name);
            Assert.That(image.gameObject.activeSelf, Is.True, name);
            Assert.That(image.preserveAspect, Is.True, name);
            Assert.That(image.sprite, Is.SameAs(
                Production2DVisualCatalog3D.Resolve(visualClass, contentId)),
                name);
        }

        private static Dictionary<string, Image> ImagesByName(GameObject root)
        {
            return root.GetComponentsInChildren<Image>(includeInactive: true)
                .ToDictionary(value => value.gameObject.name,
                    StringComparer.Ordinal);
        }

        private static Transform FindTransform(Transform root, string name)
        {
            Transform[] values = root.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < values.Length; index++)
            {
                if (string.Equals(
                        values[index].gameObject.name,
                        name,
                        StringComparison.Ordinal))
                    return values[index];
            }
            return null;
        }

        private Sprite CreateSprite(string name)
        {
            var texture = Track(new Texture2D(8, 8, TextureFormat.RGBA32,
                false));
            texture.name = name + ".Texture";
            var sprite = Track(Sprite.Create(
                texture,
                new Rect(0f, 0f, 8f, 8f),
                new Vector2(.5f, .5f),
                8f));
            sprite.name = name;
            return sprite;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }
    }
}
