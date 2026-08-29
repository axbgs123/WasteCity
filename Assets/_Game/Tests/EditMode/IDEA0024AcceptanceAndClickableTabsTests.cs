using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;

namespace WasteCity.Tests
{
    public sealed class IDEA0024AcceptanceAndClickableTabsTests
    {
        private static readonly string[] AcceptanceControlIds =
        {
            "Start.AcceptanceConsole",
            "Acceptance.Continue",
            "Acceptance.NewGame",
            "Acceptance.Back",
        };

        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            root = null;
        }

        [Test]
        public void IDEA0024_ReleaseControlsStayExactWhileDevelopmentAddsAcceptance()
        {
            IReadOnlyList<string> release =
                GrayboxSystemMenuView3D.ResolveVisibleControlIds(false);
            IReadOnlyList<string> development =
                GrayboxSystemMenuView3D.ResolveVisibleControlIds(true);

            foreach (string id in AcceptanceControlIds)
            {
                Assert.That(release, Does.Not.Contain(id),
                    "Release must not expose an acceptance entry or command.");
                Assert.That(development, Does.Contain(id),
                    "Editor/Development must expose the complete launcher surface.");
            }
            Assert.That(
                development.Except(AcceptanceControlIds).ToArray(),
                Is.EqualTo(release),
                "IDEA-0024 may only append its four Development controls.");
        }

        [Test]
        public void IDEA0024_AcceptancePageAndEntryCommandsAreDevelopmentOnly()
        {
            Type viewType = typeof(GrayboxSystemMenuView3D);
            PropertyInfo open = viewType.GetProperty(
                "IsAcceptancePageOpen",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo render = viewType.GetMethod(
                "RenderAcceptancePage",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(bool), typeof(string) },
                null);
            Assert.That(open, Is.Not.Null,
                "The acceptance page needs an observable state separate from Start.");
            Assert.That(open.PropertyType, Is.EqualTo(typeof(bool)));
            Assert.That(render, Is.Not.Null,
                "The Development page must render independently of normal Start.");

            Type entryType = typeof(GrayboxFormalSaveEntryController3D);
            AssertPublicVoid(entryType, "RequestAcceptanceContinue");
            AssertPublicVoid(entryType, "RequestAcceptanceNewGame");
            AssertPublicVoid(entryType, "RequestAcceptanceBack");

            string viewSource = ReadSource(
                "Assets/_Game/Scripts/Graybox3D/Usability/" +
                "GrayboxSystemMenuView3D.cs");
            string entrySource = ReadSource(
                "Assets/_Game/Scripts/Graybox3D/Usability/" +
                "GrayboxFormalSaveEntryController3D.cs");
            Assert.That(viewSource, Does.Contain("Page.Acceptance"));
            Assert.That(viewSource, Does.Contain("Start.AcceptanceConsole"));

            string releaseView = ProjectReleaseSource(viewSource);
            string releaseEntry = ProjectReleaseSource(entrySource);
            foreach (string id in AcceptanceControlIds)
                Assert.That(releaseView, Does.Not.Contain(id), id);
            Assert.That(releaseEntry, Does.Not.Contain("RequestAcceptance"));
        }

        [Test]
        public void IDEA0024_AcceptanceCommandsReuseFormalEntryBeforeModifierOpens()
        {
            string source = ReadSource(
                "Assets/_Game/Scripts/Graybox3D/Usability/" +
                "GrayboxFormalSaveEntryController3D.cs");
            string acceptanceContinue = ExtractMethod(
                source,
                "RequestAcceptanceContinue(");
            string acceptanceNewGame = ExtractMethod(
                source,
                "RequestAcceptanceNewGame(");
            string acceptanceBack = ExtractMethod(
                source,
                "RequestAcceptanceBack(");
            string enterGameplay = ExtractMethod(
                source,
                "private void EnterGameplay(");

            Assert.That(acceptanceContinue, Does.Contain("RequestContinue()"));
            Assert.That(acceptanceContinue,
                Does.Not.Contain("TryContinue()"),
                "Acceptance Continue must not bypass the formal entry command.");
            Assert.That(acceptanceNewGame, Does.Contain("RequestNewGame()"));
            Assert.That(acceptanceNewGame,
                Does.Not.Contain("TryStartNewProgress()"),
                "Overwrite confirmation remains owned by RequestNewGame.");
            Assert.That(acceptanceBack,
                Does.Not.Contain("TryTogglePanel"));
            Assert.That(acceptanceContinue,
                Does.Not.Contain("TryTogglePanel"));
            Assert.That(acceptanceNewGame,
                Does.Not.Contain("TryTogglePanel"));
            Assert.That(enterGameplay, Does.Contain("TryTogglePanel"),
                "The modifier may open only after formal EnterGameplay succeeds.");
        }

        [Test]
        public void IDEA0024_ClickableTabsPublishOnePageChangeAndReuseSharedPanel()
        {
            GrayboxCivilizationExpansionView3D view = CreateExpansionView();
            view.Open(GrayboxCivilizationExpansionPage3D.Army);
            Transform panel = FindTransform(
                root.transform,
                "CivilizationExpansion.Panel");
            Assert.That(panel, Is.Not.Null);
            int panelId = panel.gameObject.GetInstanceID();
            int objectCount = root.GetComponentsInChildren<Transform>(true).Length;

            Button army = FindButton("CivilizationExpansion.Tab.Army");
            Button world = FindButton("CivilizationExpansion.Tab.World");
            Button politics = FindButton("CivilizationExpansion.Tab.Politics");
            foreach (Button button in new[] { army, world, politics })
            {
                Assert.That(button.targetGraphic, Is.Not.Null);
                Assert.That(button.targetGraphic.raycastTarget, Is.True,
                    button.gameObject.name);
            }
            Image blocker = FindTransform(
                    root.transform,
                    "CivilizationExpansion.Root")
                ?.GetComponent<Image>();
            Assert.That(blocker, Is.Not.Null);
            Assert.That(blocker.raycastTarget, Is.True,
                "The shared modal must continue blocking world clicks.");

            var pages = new List<GrayboxCivilizationExpansionPage3D>();
            view.PageChanged += pages.Add;
            world.onClick.Invoke();
            politics.onClick.Invoke();
            army.onClick.Invoke();

            Assert.That(pages, Is.EqualTo(new[]
            {
                GrayboxCivilizationExpansionPage3D.World,
                GrayboxCivilizationExpansionPage3D.Politics,
                GrayboxCivilizationExpansionPage3D.Army,
            }));
            Assert.That(view.Page,
                Is.EqualTo(GrayboxCivilizationExpansionPage3D.Army));
            Assert.That(FindTransform(
                    root.transform,
                    "CivilizationExpansion.Panel").gameObject.GetInstanceID(),
                Is.EqualTo(panelId));
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length,
                Is.EqualTo(objectCount));
        }

        [Test]
        public void IDEA0024_RepeatedApplyAndTabClicksDoNotGrowObjectsOrListeners()
        {
            GrayboxCivilizationExpansionView3D view = CreateExpansionView();
            view.Open(GrayboxCivilizationExpansionPage3D.Army);
            Button army = FindButton("CivilizationExpansion.Tab.Army");
            Button world = FindButton("CivilizationExpansion.Tab.World");
            int initialObjects = root.GetComponentsInChildren<Transform>(true).Length;
            int changed = 0;
            view.PageChanged += _ => changed++;
            var presentation = new GrayboxCivilizationExpansionPresentation3D(
                "标题",
                "摘要",
                "详情",
                "主要",
                true,
                "次要",
                true,
                "第三",
                true);

            for (var index = 0; index < 32; index++)
            {
                view.Apply(presentation);
                (index % 2 == 0 ? world : army).onClick.Invoke();
            }

            Assert.That(changed, Is.EqualTo(32),
                "One click must publish exactly one PageChanged event.");
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length,
                Is.EqualTo(initialObjects));
            Assert.That(FindButton("CivilizationExpansion.Tab.World"),
                Is.SameAs(world));
        }

        private GrayboxCivilizationExpansionView3D CreateExpansionView()
        {
            root = new GameObject(
                "IDEA0024.AcceptanceTabs",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = root.GetComponent<Canvas>();
            var view = root.AddComponent<
                GrayboxCivilizationExpansionView3D>();
            view.Configure(canvas);
            return view;
        }

        private Button FindButton(string name)
        {
            Transform value = FindTransform(root.transform, name);
            Assert.That(value, Is.Not.Null, name);
            Button button = value.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, name);
            return button;
        }

        private static Transform FindTransform(Transform parent, string name)
        {
            Transform[] all = parent.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < all.Length; index++)
            {
                if (string.Equals(
                        all[index].gameObject.name,
                        name,
                        StringComparison.Ordinal))
                    return all[index];
            }
            return null;
        }

        private static void AssertPublicVoid(Type type, string methodName)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null, type.Name + "." + methodName);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
        }

        private static string ReadSource(string relativePath)
        {
            string path = Path.Combine(ProjectRoot(), relativePath);
            Assert.That(File.Exists(path), Is.True, relativePath);
            return File.ReadAllText(path);
        }

        private static string ExtractMethod(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            int brace = source.IndexOf('{', start);
            Assert.That(brace, Is.GreaterThan(start), signature);
            var depth = 0;
            for (var index = brace; index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                else if (source[index] == '}' && --depth == 0)
                    return source.Substring(start, index - start + 1);
            }
            Assert.Fail("Unclosed method: " + signature);
            return string.Empty;
        }

        private static string ProjectReleaseSource(string source)
        {
            string[] lines = source.Replace("\r\n", "\n").Split('\n');
            var output = new List<string>(lines.Length);
            var parents = new Stack<bool>();
            var conditions = new Stack<bool>();
            bool active = true;
            for (var index = 0; index < lines.Length; index++)
            {
                string trimmed = lines[index].Trim();
                if (trimmed.StartsWith("#if ", StringComparison.Ordinal))
                {
                    bool condition = EvaluateReleaseCondition(
                        trimmed.Substring(4).Trim());
                    parents.Push(active);
                    conditions.Push(condition);
                    active = active && condition;
                    continue;
                }
                if (string.Equals(trimmed, "#else", StringComparison.Ordinal))
                {
                    bool condition = conditions.Pop();
                    bool parent = parents.Peek();
                    conditions.Push(!condition);
                    active = parent && !condition;
                    continue;
                }
                if (string.Equals(trimmed, "#endif", StringComparison.Ordinal))
                {
                    conditions.Pop();
                    active = parents.Pop();
                    continue;
                }
                if (active) output.Add(lines[index]);
            }
            return string.Join("\n", output);
        }

        private static bool EvaluateReleaseCondition(string condition)
        {
            if (condition.Contains("UNITY_EDITOR") ||
                condition.Contains("DEVELOPMENT_BUILD"))
            {
                return condition.Contains("!");
            }
            return true;
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }
    }
}
