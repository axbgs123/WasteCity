using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.Persistence;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalSaveRuntimeInputTests
    {
        private const string SceneName = "GrayboxPrototype3D";

        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.UpdateMode previousUpdateMode;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode
            previousEditorInputBehavior;
        private float previousTimeScale;
        private string saveDirectory;
        private string realSaveDirectory;
        private Dictionary<string, FormalSaveFileSnapshot>
            originalRealSaveFiles;
        private MethodInfo configureStoreRootForTesting;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            previousUpdateMode = InputSystem.settings.updateMode;
            previousBackgroundBehavior =
                InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior =
                InputSystem.settings.editorInputBehaviorInPlayMode;
            realSaveDirectory = Application.persistentDataPath;
            originalRealSaveFiles = CaptureFormalSaveFileSnapshots(
                realSaveDirectory);
            configureStoreRootForTesting =
                RequireConfigureStoreRootForTesting();
            saveDirectory = Path.Combine(
                Path.GetTempPath(),
                "wastecity-formal-save-runtime-input-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveDirectory);
            configureStoreRootForTesting.Invoke(
                null,
                new object[] { saveDirectory });

            Time.timeScale = 1f;
            InputSystem.settings.updateMode =
                InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode
                    .AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            yield return LoadScene();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return UnloadScene();
            if (configureStoreRootForTesting != null)
            {
                configureStoreRootForTesting.Invoke(
                    null,
                    new object[] { null });
            }
            if (!string.IsNullOrWhiteSpace(saveDirectory) &&
                Directory.Exists(saveDirectory))
            {
                Directory.Delete(saveDirectory, true);
            }
            if (keyboard != null && keyboard.added)
                InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added)
                InputSystem.RemoveDevice(mouse);
            InputSystem.settings.updateMode = previousUpdateMode;
            InputSystem.settings.backgroundBehavior =
                previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                previousEditorInputBehavior;
            Time.timeScale = previousTimeScale;

            Dictionary<string, FormalSaveFileSnapshot> actualRealSaveFiles =
                CaptureFormalSaveFileSnapshots(realSaveDirectory);
            AssertFormalSaveFilesUnchanged(
                originalRealSaveFiles,
                actualRealSaveFiles);
            yield return null;
        }

        [UnityTest]
        public IEnumerator IDEA0015_StartPageDisablesContinueWithoutSchema31()
        {
            EntryProbe entry = RequireEntry();
            Button continueButton = FindButton("Start.Continue");

            Assert.That(entry.IsStartPageOpen, Is.True);
            Assert.That(entry.IsRuntimeReady, Is.False);
            Assert.That(entry.CanContinue, Is.False);
            Assert.That(continueButton, Is.Not.Null);
            Assert.That(continueButton.interactable, Is.False);
            Assert.That(entry.FeedbackMessage,
                Is.EqualTo("没有可继续的有效存档"));
            Assert.That(FindText("FormalSave.Feedback").text,
                Is.EqualTo(entry.FeedbackMessage));
            yield return null;
        }

        [UnityTest]
        public IEnumerator IDEA0015_LegacySchemaOneAndThirtyUseExplicitCompatibilityFlow()
        {
            foreach (string fixtureName in new[]
                     {
                         "schema-01-legacy-2d.json",
                         "schema-30-legacy-2d.json",
                     })
            {
                WriteFixtureToPrimary(fixtureName);
                yield return ReloadScene();
                EntryProbe entry = RequireEntry();

                Assert.That(entry.CanContinue, Is.False, fixtureName);
                Assert.That(entry.IsRuntimeReady, Is.False, fixtureName);
                Assert.That(
                    entry.FeedbackMessage,
                    Is.EqualTo(
                        "检测到旧版 2D 存档，不能直接用于当前 3D 游戏"),
                    fixtureName);
                Assert.That(
                    FindButton("Start.Continue").interactable,
                    Is.False,
                    fixtureName);

                yield return ClickButton("Start.NewGame");
                Assert.That(
                    entry.IsNewGameConfirmationOpen,
                    Is.True,
                    fixtureName);
                Assert.That(entry.IsRuntimeReady, Is.False, fixtureName);
                Assert.That(
                    FindButton("Start.NewGameConfirm"),
                    Is.Not.Null,
                    fixtureName);
                yield return ClickButton("Start.NewGameCancel");
                Assert.That(
                    entry.IsNewGameConfirmationOpen,
                    Is.False,
                    fixtureName);
                Assert.That(entry.IsRuntimeReady, Is.False, fixtureName);

                yield return ClickButton("Start.NewGame");
                yield return ClickButton("Start.NewGameConfirm");
                Assert.That(
                    entry.IsStartPageOpen,
                    Is.False,
                    entry.FeedbackMessage);
                Assert.That(entry.IsRuntimeReady, Is.True, fixtureName);
            }
        }

        [UnityTest]
        public IEnumerator IDEA0015_FailedContinueRefreshesTitleFeedback()
        {
            WriteFixtureToPrimary("schema-31-formal-3d.json");
            yield return ReloadScene();
            EntryProbe entry = RequireEntry();

            Assert.That(entry.CanContinue, Is.True);
            Assert.That(entry.IsStartPageOpen, Is.True);
            Assert.That(entry.IsRuntimeReady, Is.False);
            Assert.That(FindButton("Start.Continue").interactable, Is.True);
            Assert.That(entry.FeedbackMessage,
                Is.EqualTo("已继续最近进度"));
            GrayboxFormalSaveRuntimeHost3D host = UnityEngine.Object
                .FindObjectOfType<GrayboxFormalSaveRuntimeHost3D>();
            Assert.That(host, Is.Not.Null);
            Assert.That(host.ExplorationController, Is.Not.Null);
            AssertExplorationControllerIsUninitialized(
                host.ExplorationController);

            yield return ClickButton("Start.Continue");

            Assert.That(entry.IsStartPageOpen, Is.True);
            Assert.That(entry.IsRuntimeReady, Is.False);
            Assert.That(entry.FeedbackMessage,
                Is.Not.EqualTo("已继续最近进度"));
            Assert.That(entry.FeedbackMessage, Is.Not.Empty);
            Assert.That(FindText("FormalSave.Feedback").text,
                Is.EqualTo(entry.FeedbackMessage));
            AssertExplorationControllerIsUninitialized(
                host.ExplorationController);
        }

        [UnityTest]
        public IEnumerator IDEA0015_Formal3DOverwriteRequiresConfirmation()
        {
            WriteFixtureToPrimary("schema-31-formal-3d.json");
            yield return ReloadScene();
            EntryProbe entry = RequireEntry();

            Assert.That(entry.CanContinue, Is.True);
            Assert.That(entry.IsStartPageOpen, Is.True);
            Assert.That(entry.IsRuntimeReady, Is.False);
            yield return ClickButton("Start.NewGame");

            Assert.That(entry.IsNewGameConfirmationOpen, Is.True);
            Assert.That(entry.IsStartPageOpen, Is.True);
            Assert.That(entry.IsRuntimeReady, Is.False);
            Assert.That(
                FindButton("Start.NewGameConfirm").gameObject
                    .activeInHierarchy,
                Is.True);
            Assert.That(
                FindButton("Start.NewGameCancel").gameObject
                    .activeInHierarchy,
                Is.True);
        }

        [UnityTest]
        public IEnumerator IDEA0015_RealSaveAndQuitWritesBeforeExitThenContinues()
        {
            EntryProbe entry = RequireEntry();
            yield return StartNewGame(entry);
            var exit = new FakeExit();
            ConfigureFakeExit(exit);

            yield return TapKey(Key.Escape);
            Assert.That(RequireMenu().IsOpen, Is.True);
            yield return ClickButton("Main.Quit");
            Assert.That(FindText("Exit.Warning").text,
                Does.Contain("保存并退出"));
            Assert.That(FindText("Exit.Warning").text,
                Does.Not.Contain("不会保存"));
            yield return ClickButton("Exit.SaveAndQuit");

            Assert.That(exit.Count, Is.EqualTo(1));
            Assert.That(File.Exists(PrimaryPath), Is.True);
            Assert.That(entry.FeedbackMessage, Is.EqualTo("游戏已保存"));

            yield return ReloadScene();
            entry = RequireEntry();
            Assert.That(entry.CanContinue, Is.True);
            Assert.That(FindButton("Start.Continue").interactable, Is.True);
            yield return ClickButton("Start.Continue");
            Assert.That(
                entry.IsStartPageOpen,
                Is.False,
                entry.FeedbackMessage);
            Assert.That(entry.IsRuntimeReady, Is.True);
            Assert.That(entry.FeedbackMessage,
                Is.EqualTo("已继续最近进度"));
        }

        [UnityTest]
        public IEnumerator IDEA0015_SaveFailureStaysRunningAndShowsFixedReason()
        {
            EntryProbe entry = RequireEntry();
            yield return StartNewGame(entry);
            var exit = new FakeExit();
            ConfigureFakeExit(exit);

            DeletePath(PrimaryPath);
            Directory.CreateDirectory(PrimaryPath);
            yield return TapKey(Key.Escape);
            yield return ClickButton("Main.Quit");
            yield return ClickButton("Exit.SaveAndQuit");

            Assert.That(exit.Count, Is.Zero);
            Assert.That(RequireMenu().IsOpen, Is.True);
            Assert.That(entry.IsRuntimeReady, Is.True);
            Assert.That(entry.FeedbackMessage,
                Is.EqualTo("保存失败，原存档未被覆盖"));
            Assert.That(FindText("FormalSave.Feedback").text,
                Is.EqualTo(entry.FeedbackMessage));
        }

        [UnityTest]
        public IEnumerator IDEA0015_FailedFirstCheckpointUsesPersistentHudUntilSaved()
        {
            EntryProbe entry = RequireEntry();
            Directory.CreateDirectory(PrimaryPath);

            yield return ClickButton("Start.NewGame");

            Assert.That(entry.IsStartPageOpen, Is.False);
            Assert.That(entry.IsRuntimeReady, Is.True);
            Text warning = FindText("FormalSave.CheckpointWarning");
            Assert.That(warning.gameObject.activeInHierarchy, Is.True);
            Assert.That(warning.text,
                Is.EqualTo("自动存档失败，当前进度尚未保存"));
            Assert.That(File.Exists(PrimaryPath), Is.False);

            DeletePath(PrimaryPath);
            var exit = new FakeExit();
            ConfigureFakeExit(exit);
            yield return TapKey(Key.Escape);
            yield return ClickButton("Main.Quit");
            yield return ClickButton("Exit.SaveAndQuit");

            Assert.That(exit.Count, Is.EqualTo(1));
            Assert.That(File.Exists(PrimaryPath), Is.True);
            Assert.That(warning.text, Is.Empty);
            Assert.That(warning.gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator IDEA0015_QuitWithoutSavingRequiresExplicitRealClick()
        {
            EntryProbe entry = RequireEntry();
            yield return StartNewGame(entry);
            var exit = new FakeExit();
            ConfigureFakeExit(exit);

            DeletePath(PrimaryPath);
            Directory.CreateDirectory(PrimaryPath);
            yield return TapKey(Key.Escape);
            yield return ClickButton("Main.Quit");
            Button quitWithoutSaving =
                FindButton("Exit.QuitWithoutSaving");
            Assert.That(quitWithoutSaving, Is.Not.Null);
            Assert.That(
                quitWithoutSaving.gameObject.activeInHierarchy,
                Is.False);
            Assert.That(quitWithoutSaving.interactable, Is.False);

            yield return ClickButton("Exit.SaveAndQuit");
            Assert.That(exit.Count, Is.Zero);
            Assert.That(entry.FeedbackMessage,
                Is.EqualTo("保存失败，原存档未被覆盖"));
            Assert.That(
                quitWithoutSaving.gameObject.activeInHierarchy,
                Is.True);
            Assert.That(quitWithoutSaving.interactable, Is.True);
            Assert.That(FindButton("Exit.Cancel"), Is.Not.Null);

            yield return ClickButton("Exit.Cancel");
            Assert.That(exit.Count, Is.Zero);
            Assert.That(RequireMenu().Page,
                Is.EqualTo(GrayboxSystemMenuPage3D.Main));

            DeletePath(PrimaryPath);
            byte[] sentinel =
            {
                0x57, 0x43, 0x2d, 0x4e, 0x4f,
                0x53, 0x41, 0x56, 0x45,
            };
            File.WriteAllBytes(PrimaryPath, sentinel);
            yield return ClickButton("Main.Quit");
            Assert.That(
                quitWithoutSaving.gameObject.activeInHierarchy,
                Is.True);
            Assert.That(quitWithoutSaving.interactable, Is.True);
            yield return ClickButton("Exit.QuitWithoutSaving");

            Assert.That(exit.Count, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(PrimaryPath), Is.EqualTo(sentinel));
            Assert.That(File.Exists(PrimaryPath + ".tmp"), Is.False);
        }

        [UnityTest]
        public IEnumerator IDEA0015_BackupRecoveryIsVisibleBeforeRealContinue()
        {
            EntryProbe entry = RequireEntry();
            yield return StartNewGame(entry);
            var firstExit = new FakeExit();
            ConfigureFakeExit(firstExit);
            yield return TapKey(Key.Escape);
            yield return ClickButton("Main.Quit");
            yield return ClickButton("Exit.SaveAndQuit");
            Assert.That(firstExit.Count, Is.EqualTo(1));

            yield return ReloadScene();
            entry = RequireEntry();
            yield return ClickButton("Start.Continue");
            var secondExit = new FakeExit();
            ConfigureFakeExit(secondExit);
            yield return TapKey(Key.Escape);
            yield return ClickButton("Main.Quit");
            yield return ClickButton("Exit.SaveAndQuit");
            Assert.That(secondExit.Count, Is.EqualTo(1));
            Assert.That(File.Exists(BackupPath), Is.True);

            File.WriteAllText(PrimaryPath, "{corrupt");
            yield return ReloadScene();
            entry = RequireEntry();
            Assert.That(entry.CanContinue, Is.True);
            Assert.That(entry.FeedbackMessage,
                Is.EqualTo("主存档损坏，已恢复备份"));
            yield return ClickButton("Start.Continue");
            Assert.That(entry.IsRuntimeReady, Is.True);
            Assert.That(entry.FeedbackMessage,
                Is.EqualTo("主存档损坏，已恢复备份"));
        }

        [UnityTest]
        public IEnumerator IDEA0015_OpenMenuBlocksBuildResearchMovementAndEvacuation()
        {
            EntryProbe entry = RequireEntry();
            yield return StartNewGame(entry);
            GrayboxUsabilityInputCoordinator3D input =
                UnityEngine.Object.FindObjectOfType<
                    GrayboxUsabilityInputCoordinator3D>();
            GrayboxMobileCityController3D city =
                UnityEngine.Object.FindObjectOfType<
                    GrayboxMobileCityController3D>();
            GrayboxBuildingInteractionModel3D building =
                UnityEngine.Object.FindObjectOfType<
                    GrayboxBuildingInteractionModel3D>();
            GrayboxOperationsController3D operations =
                UnityEngine.Object.FindObjectOfType<
                    GrayboxOperationsController3D>();
            GrayboxEvacuationController3D evacuation =
                UnityEngine.Object.FindObjectOfType<
                    GrayboxEvacuationController3D>();
            Assert.That(input, Is.Not.Null);
            Assert.That(city, Is.Not.Null);
            Assert.That(building, Is.Not.Null);
            Assert.That(operations, Is.Not.Null);
            Assert.That(evacuation, Is.Not.Null);

            yield return TapKey(Key.Escape);
            GrayboxSystemMenuController3D menu = RequireMenu();
            Assert.That(menu.IsOpen, Is.True);
            Vector3 cityPosition = city.transform.position;
            uint buildingCalls = input.BuildingInputInvocationCount;

            yield return TapKey(Key.B);
            yield return TapKey(Key.T);
            yield return TapKey(Key.E);
            yield return TapKey(Key.F);
            yield return HoldPausedKey(Key.W, 2);

            Assert.That(menu.IsOpen, Is.True);
            Assert.That(city.transform.position, Is.EqualTo(cityPosition));
            Assert.That(input.BuildingInputInvocationCount,
                Is.EqualTo(buildingCalls));
            Assert.That(building.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));
            Assert.That(operations.IsAnyPanelOpen, Is.False);
            Assert.That(evacuation.IsManifestOpen, Is.False);
        }

        private string PrimaryPath => Path.Combine(
            saveDirectory,
            FormalSaveStore.FileName);

        private string BackupPath => PrimaryPath + ".bak";

        private IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private IEnumerator ReloadScene()
        {
            yield return UnloadScene();
            yield return LoadScene();
        }

        private static IEnumerator UnloadScene()
        {
            Scene graybox = SceneManager.GetSceneByName(SceneName);
            if (graybox.IsValid() && graybox.isLoaded)
            {
                Scene empty = SceneManager.CreateScene(
                    "GrayboxFormalSaveRuntimeInputEmpty");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(graybox);
            }
            Time.timeScale = 1f;
            yield return null;
        }

        private IEnumerator StartNewGame(
            EntryProbe entry)
        {
            Assert.That(entry.IsStartPageOpen, Is.True);
            yield return ClickButton("Start.NewGame");
            if (entry.IsNewGameConfirmationOpen)
                yield return ClickButton("Start.NewGameConfirm");
            Assert.That(entry.IsRuntimeReady, Is.True);
            Assert.That(entry.IsStartPageOpen, Is.False);
        }

        private void ConfigureFakeExit(FakeExit exit)
        {
            GrayboxSystemMenuController3D menu = RequireMenu();
            menu.Configure(
                new WasteCity.Core.GameSpeedModel(),
                new GrayboxDisplaySettingsModel3D(
                    new FakeDisplayPlatform(),
                    new FakeDisplayStore()),
                exit,
                UnityEngine.Object.FindObjectOfType<
                    GrayboxSystemMenuView3D>());
        }

        private IEnumerator TapKey(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        private IEnumerator HoldPausedKey(Key key, int frames)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            for (var index = 0; index < frames; index++)
                yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        private IEnumerator ClickButton(string name)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            Button button = FindButton(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, name);
            Assert.That(button.interactable, Is.True, name);
            yield return ClickUiElement(button.gameObject);
        }

        private IEnumerator ClickUiElement(GameObject target)
        {
            InputSystemUIInputModule module =
                UnityEngine.Object.FindObjectOfType<
                    InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null);
            Assert.That(module.enabled, Is.True);
            Assert.That(module.leftClick.action.enabled, Is.True);
            RectTransform rect = target.GetComponent<RectTransform>();
            Assert.That(rect, Is.Not.Null, target.name);
            Vector2 position = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center));
            QueueMouse(position);
            yield return null;
            QueueMouse(position, MouseButton.Left);
            yield return null;
            QueueMouse(position);
            yield return null;
        }

        private void QueueMouse(Vector2 position, MouseButton? button = null)
        {
            var state = new MouseState { position = position };
            if (button.HasValue)
                state = state.WithButton(button.Value);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
        }

        private void WriteFixtureToPrimary(string fixtureName)
        {
            Directory.CreateDirectory(saveDirectory);
            string fixturePath = Path.Combine(
                Application.dataPath,
                "_Game/Tests/Fixtures/Persistence",
                fixtureName);
            File.Copy(fixturePath, PrimaryPath, true);
        }

        private static EntryProbe RequireEntry()
        {
            MonoBehaviour entry = UnityEngine.Object
                .FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(value =>
                    value.GetType().FullName ==
                    "WasteCity.Graybox3D.Usability." +
                    "GrayboxFormalSaveEntryController3D");
            Assert.That(entry, Is.Not.Null,
                "GrayboxPrototype3D must own the formal save entry.");
            return new EntryProbe(entry);
        }

        private static GrayboxSystemMenuController3D RequireMenu()
        {
            GrayboxSystemMenuController3D menu =
                UnityEngine.Object.FindObjectOfType<
                    GrayboxSystemMenuController3D>();
            Assert.That(menu, Is.Not.Null);
            return menu;
        }

        private static void AssertExplorationControllerIsUninitialized(
            WasteCity.Graybox3D.Exploration.GrayboxExplorationController3D
                controller)
        {
            Assert.That(controller.IsInitialized, Is.False);
            Assert.That(controller.Exploration, Is.Null);
            Assert.That(controller.LeaderControl, Is.Null);
            Assert.That(controller.ManualGather, Is.Null);
            Assert.That(controller.CenJinDistress, Is.Null);
            Assert.That(controller.OutpostAlerts, Is.Null);
            Assert.That(controller.AreBoundariesConfigured, Is.False);
        }

        private static Button FindButton(string name)
        {
            return UnityEngine.Object.FindObjectsOfType<Button>(true)
                .FirstOrDefault(value => value.name == name);
        }

        private static Text FindText(string name)
        {
            Text text = UnityEngine.Object.FindObjectsOfType<Text>(true)
                .FirstOrDefault(value => value.name == name);
            Assert.That(text, Is.Not.Null, name);
            return text;
        }

        private static MethodInfo RequireConfigureStoreRootForTesting()
        {
            MethodInfo method = typeof(GrayboxFormalSaveRuntimeHost3D)
                .GetMethod(
                    "ConfigureStoreRootForTesting",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);
            Assert.That(method, Is.Not.Null,
                "The formal save host needs one private static test-only " +
                "store-root override before the formal scene is loaded.");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(
                method.GetParameters().Select(value => value.ParameterType),
                Is.EqualTo(new[] { typeof(string) }));
            return method;
        }

        private static Dictionary<string, FormalSaveFileSnapshot>
            CaptureFormalSaveFileSnapshots(
                string directory)
        {
            var snapshot = new Dictionary<string, FormalSaveFileSnapshot>(
                StringComparer.Ordinal);
            if (!Directory.Exists(directory)) return snapshot;
            foreach (string path in Directory.GetFiles(
                         directory,
                         "formal-world*",
                         SearchOption.TopDirectoryOnly))
            {
                snapshot[Path.GetFileName(path)] =
                    new FormalSaveFileSnapshot(
                        File.ReadAllBytes(path),
                        File.GetCreationTimeUtc(path),
                        File.GetLastWriteTimeUtc(path),
                        File.GetAttributes(path));
            }
            return snapshot;
        }

        private static void AssertFormalSaveFilesUnchanged(
            IReadOnlyDictionary<string, FormalSaveFileSnapshot> expected,
            IReadOnlyDictionary<string, FormalSaveFileSnapshot> actual)
        {
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys),
                "PlayMode save tests must not create or remove real saves.");
            foreach (KeyValuePair<string, FormalSaveFileSnapshot> pair in
                     expected)
            {
                Assert.That(actual.TryGetValue(
                    pair.Key,
                    out FormalSaveFileSnapshot observed), Is.True);
                CollectionAssert.AreEqual(
                    pair.Value.Bytes,
                    observed.Bytes,
                    pair.Key + " bytes");
                Assert.That(
                    observed.CreationTimeUtc,
                    Is.EqualTo(pair.Value.CreationTimeUtc),
                    pair.Key + " creation time");
                Assert.That(
                    observed.LastWriteTimeUtc,
                    Is.EqualTo(pair.Value.LastWriteTimeUtc),
                    pair.Key + " last-write time");
                Assert.That(
                    observed.Attributes,
                    Is.EqualTo(pair.Value.Attributes),
                    pair.Key + " attributes");
            }
        }

        private static void DeletePath(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
            else if (Directory.Exists(path))
                Directory.Delete(path, true);
        }

        private sealed class FakeExit : IGrayboxApplicationExit
        {
            public int Count { get; private set; }
            public void Exit() => Count++;
        }

        private sealed class FormalSaveFileSnapshot
        {
            public FormalSaveFileSnapshot(
                byte[] bytes,
                DateTime creationTimeUtc,
                DateTime lastWriteTimeUtc,
                FileAttributes attributes)
            {
                Bytes = bytes;
                CreationTimeUtc = creationTimeUtc;
                LastWriteTimeUtc = lastWriteTimeUtc;
                Attributes = attributes;
            }

            public byte[] Bytes { get; }
            public DateTime CreationTimeUtc { get; }
            public DateTime LastWriteTimeUtc { get; }
            public FileAttributes Attributes { get; }
        }

        private sealed class EntryProbe
        {
            private readonly MonoBehaviour target;

            public EntryProbe(MonoBehaviour target)
            {
                this.target = target;
            }

            public bool IsStartPageOpen => Read<bool>("IsStartPageOpen");
            public bool CanContinue => Read<bool>("CanContinue");
            public bool IsNewGameConfirmationOpen =>
                Read<bool>("IsNewGameConfirmationOpen");
            public string FeedbackMessage =>
                Read<string>("FeedbackMessage");
            public bool IsRuntimeReady => Read<bool>("IsRuntimeReady");

            private T Read<T>(string propertyName)
            {
                System.Reflection.PropertyInfo property = target.GetType()
                    .GetProperty(propertyName);
                Assert.That(property, Is.Not.Null,
                    target.GetType().FullName + "." + propertyName);
                return (T)property.GetValue(target);
            }
        }

        private sealed class FakeDisplayPlatform :
            IGrayboxDisplaySettingsPlatform
        {
            private static readonly IReadOnlyList<
                GrayboxDisplayResolution3D> Resolutions =
                new[] { new GrayboxDisplayResolution3D(1280, 720) };

            public IReadOnlyList<GrayboxDisplayResolution3D>
                AvailableResolutions => Resolutions;
            public GrayboxDisplaySettings3D Current =>
                new GrayboxDisplaySettings3D(
                    1280,
                    720,
                    GrayboxWindowMode3D.Windowed);
            public bool TryApply(GrayboxDisplaySettings3D settings) => true;
        }

        private sealed class FakeDisplayStore :
            IGrayboxDisplaySettingsStore
        {
            public bool TryLoad(
                out int version,
                out GrayboxDisplaySettings3D settings)
            {
                version = 0;
                settings = default;
                return false;
            }

            public void Save(
                int version,
                GrayboxDisplaySettings3D settings)
            {
            }
        }
    }
}
