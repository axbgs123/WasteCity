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
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.Progression;
using Object = UnityEngine.Object;

namespace WasteCity.Tests
{
    /// <summary>
    /// Shared formal-entry fixture for pre-save PlayMode suites. It isolates
    /// the formal slot before scene Awake, then starts gameplay through the
    /// same UGUI/Input System path used by a player.
    /// </summary>
    internal static class GrayboxFormalPlayModeEntryFixture
    {
        private static readonly MethodInfo ConfigureStoreRoot =
            RequireConfigureStoreRoot();

        private static string isolatedStoreRoot;
        private static string realStoreRoot;
        private static Dictionary<string, FormalSaveFileSnapshot>
            realSaveFilesBefore;

        public static void BeginIsolatedStore()
        {
            CleanupIsolatedStore();
            AssertRealSaveFilesUnchanged();
            realStoreRoot = Application.persistentDataPath;
            realSaveFilesBefore = CaptureFormalSaveFiles(realStoreRoot);
            isolatedStoreRoot = Path.Combine(
                Path.GetTempPath(),
                "wastecity-playmode-entry-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(isolatedStoreRoot);
            ConfigureStoreRoot.Invoke(
                null,
                new object[] { isolatedStoreRoot });
        }

        public static IEnumerator StartNewProgressThroughRealUi(
            Mouse mouse,
            bool completeFateSelection = true)
        {
            Assert.That(mouse, Is.Not.Null);
            GrayboxFormalSaveEntryController3D entry =
                Object.FindObjectOfType<
                    GrayboxFormalSaveEntryController3D>(true);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.IsStartPageOpen, Is.True);

            yield return ClickButton("Start.NewGame", mouse);
            Assert.That(entry.IsNewGameConfirmationOpen, Is.False,
                "An empty isolated formal slot must never request overwrite " +
                "confirmation.");
            GrayboxFormalSaveRuntimeHost3D diagnosticHost =
                Object.FindObjectOfType<GrayboxFormalSaveRuntimeHost3D>();
            Assert.That(
                entry.IsRuntimeReady,
                Is.True,
                entry.FeedbackMessage + " | hostInitialized=" +
                (diagnosticHost?.IsInitialized.ToString() ?? "missing") +
                " | store=" +
                (diagnosticHost?.LastStoreResult?.Code.ToString() ?? "null") +
                " | coordinator=" +
                (diagnosticHost?.LastCoordinatorResult?.Code.ToString() ??
                 "null") +
                " | startError=" +
                (diagnosticHost?.LastStartNewProgressError ?? "null") +
                " | initializationError=" +
                (diagnosticHost?.LastInitializationError ?? "null") +
                " | progressionError=" +
                (diagnosticHost?.LastProgressionRestoreError ?? "null"));
            Assert.That(entry.IsStartPageOpen, Is.False);
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            Assert.That(
                File.Exists(Path.Combine(
                    isolatedStoreRoot,
                    "formal-world.json")),
                Is.True,
                "Starting a fresh formal progress must checkpoint only into " +
                "the isolated PlayMode slot. coordinator=" +
                (diagnosticHost?.LastCoordinatorResult?.Code.ToString() ??
                 "null") + " message=" +
                (diagnosticHost?.LastCoordinatorResult?.Message ?? "null") +
                " store=" +
                (diagnosticHost?.LastStoreResult?.Code.ToString() ?? "null") +
                " storeMessage=" +
                (diagnosticHost?.LastStoreResult?.Message ?? "null") +
                " diagnostic=" +
                (diagnosticHost?.LastStoreResult?.Diagnostic ?? "null"));
            if (completeFateSelection)
            {
                GrayboxFormalSaveRuntimeHost3D host =
                    Object.FindObjectOfType<
                        GrayboxFormalSaveRuntimeHost3D>();
                Assert.That(host, Is.Not.Null);
                string offeredFateId =
                    host.FateRuntime.Capture().OfferedIds[0];
                yield return ClickButton(
                    "FateSelection.Card." +
                    offeredFateId,
                    mouse);
                yield return ClickButton("FateSelection.Confirm", mouse);
                Assert.That(host.FateRuntime.Capture().SelectedId,
                    Is.EqualTo(offeredFateId));
            }
        }

        public static void CleanupIsolatedStore()
        {
            ConfigureStoreRoot.Invoke(null, new object[] { null });
            string isolatedRoot = isolatedStoreRoot;
            isolatedStoreRoot = null;
            if (!string.IsNullOrWhiteSpace(isolatedRoot) &&
                Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, true);
            }
        }

        public static void AssertRealSaveFilesUnchanged()
        {
            if (realSaveFilesBefore == null) return;
            try
            {
                AssertFormalSaveFilesUnchanged(
                    realSaveFilesBefore,
                    CaptureFormalSaveFiles(realStoreRoot));
            }
            finally
            {
                realStoreRoot = null;
                realSaveFilesBefore = null;
            }
        }

        private static IEnumerator ClickButton(string name, Mouse mouse)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            Button button = Object.FindObjectsOfType<Button>(true)
                .FirstOrDefault(candidate => candidate.name == name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, name);
            Assert.That(button.interactable, Is.True, name);

            InputSystemUIInputModule module = Object.FindObjectOfType<
                InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null);
            Assert.That(module.enabled, Is.True);
            Assert.That(module.point?.action?.enabled, Is.True);
            Assert.That(module.leftClick?.action?.enabled, Is.True);
            RectTransform rect = button.GetComponent<RectTransform>();
            Assert.That(rect, Is.Not.Null, name);
            Vector2 position = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center));

            var pointer = new PointerEventData(EventSystem.current)
            {
                position = position,
            };
            var raycasts = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, raycasts);
            string raycastNames = string.Join(", ", raycasts.Select(
                value => value.gameObject == null
                    ? "null"
                    : value.gameObject.name));
            Assert.That(raycasts, Is.Not.Empty, name + " raycast");
            Assert.That(
                raycasts[0].gameObject == button.gameObject ||
                raycasts[0].gameObject.transform.IsChildOf(button.transform),
                Is.True,
                name + " is covered by: " + raycastNames);

            QueueMouse(mouse, position);
            yield return null;
            QueueMouse(mouse, position, MouseButton.Left);
            yield return null;
            QueueMouse(mouse, position);
            yield return null;
        }

        private static void QueueMouse(
            Mouse mouse,
            Vector2 position,
            MouseButton? button = null)
        {
            var state = new MouseState { position = position };
            if (button.HasValue)
                state = state.WithButton(button.Value);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
        }

        private static MethodInfo RequireConfigureStoreRoot()
        {
            MethodInfo method = typeof(GrayboxFormalSaveRuntimeHost3D)
                .GetMethod(
                    "ConfigureStoreRootForTesting",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static Dictionary<string, FormalSaveFileSnapshot>
            CaptureFormalSaveFiles(string directory)
        {
            var snapshot = new Dictionary<string, FormalSaveFileSnapshot>(
                StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(directory) ||
                !Directory.Exists(directory))
            {
                return snapshot;
            }
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
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys));
            foreach (KeyValuePair<string, FormalSaveFileSnapshot> pair in
                     expected)
            {
                Assert.That(actual.TryGetValue(
                    pair.Key,
                    out FormalSaveFileSnapshot observed), Is.True);
                CollectionAssert.AreEqual(pair.Value.Bytes, observed.Bytes);
                Assert.That(observed.CreationTimeUtc,
                    Is.EqualTo(pair.Value.CreationTimeUtc));
                Assert.That(observed.LastWriteTimeUtc,
                    Is.EqualTo(pair.Value.LastWriteTimeUtc));
                Assert.That(observed.Attributes,
                    Is.EqualTo(pair.Value.Attributes));
            }
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
    }
}
