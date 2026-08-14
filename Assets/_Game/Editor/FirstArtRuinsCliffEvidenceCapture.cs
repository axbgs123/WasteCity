using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WasteCity.ArtIntegration3D;
using WasteCity.Graybox3D;
using Object = UnityEngine.Object;

namespace WasteCity.Editor
{
    public static class FirstArtRuinsCliffEvidenceCapture
    {
        public const int CaptureWidth = 1280;
        public const int CaptureHeight = 720;

        private const string OutputEnvironmentVariable =
            "WASTECITY_RUINS_CLIFF_EVIDENCE_DIR";
        private const string ScenePath =
            "Assets/_Game/Scenes/GrayboxPrototype3D.unity";
        private const string ActiveSessionKey =
            "WasteCity.FirstArtRuinsCliffEvidenceCapture.Active";
        private const string OutputSessionKey =
            "WasteCity.FirstArtRuinsCliffEvidenceCapture.Output";
        private const string ExitCodeSessionKey =
            "WasteCity.FirstArtRuinsCliffEvidenceCapture.ExitCode";
        private const double RuntimeTimeoutSeconds = 120d;
        private const string Passed = "passed";

        private static readonly string[] requiredCaptureFileNames =
        {
            "01-default-camera.png",
            "02-top-view.png",
            "03-ruins-closeup.png",
            "04-cliff-straight-a.png",
            "05-cliff-straight-b.png",
            "06-cliff-inner-corner.png",
            "07-cliff-outer-corner.png",
            "08-cliff-end-cap.png",
            "09-cliff-top-cap.png",
            "10-both-presented.png",
            "11-ruins-fallback.png",
            "12-cliff-fallback.png",
        };

        private static bool automationActive;
        private static string outputRoot;
        private static double runtimeStartedAt;

        public static IReadOnlyList<string> RequiredCaptureFileNames =>
            requiredCaptureFileNames;

        [Serializable]
        private sealed class EvidenceManifest
        {
            public string scene;
            public int seed;
            public string terrainProfileGuid;
            public string geometryProfileGuid;
            public string[] materialGuids;
            public string[] prefabGuids;
            public CaptureRecord[] captures;
            public string captureResult;
        }

        [Serializable]
        private sealed class CaptureRecord
        {
            public string filename;
            public string sha256;
            public string result;
            public string ruinsStatus;
            public string cliffStatus;
            public string worldToCameraMatrix;
            public string projectionMatrix;
        }

        private sealed class CaptureContext
        {
            public GrayboxSceneBootstrap Bootstrap;
            public GrayboxWorldView3D WorldView;
            public FirstArtTerrainRenderer3D Presenter;
            public Camera Camera;
            public GrayboxCameraController3D CameraController;
            public FirstArtTerrainProfile3D TerrainProfile;
            public FirstArtRuinsCliffProfile3D GeometryProfile;
        }

        private readonly struct CameraState
        {
            public CameraState(Camera camera)
            {
                Position = camera.transform.position;
                Rotation = camera.transform.rotation;
                Orthographic = camera.orthographic;
                OrthographicSize = camera.orthographicSize;
                Near = camera.nearClipPlane;
                Far = camera.farClipPlane;
                ClearFlags = camera.clearFlags;
                Background = camera.backgroundColor;
            }

            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public bool Orthographic { get; }
            public float OrthographicSize { get; }
            public float Near { get; }
            public float Far { get; }
            public CameraClearFlags ClearFlags { get; }
            public Color Background { get; }

            public void Restore(Camera camera)
            {
                camera.transform.SetPositionAndRotation(Position, Rotation);
                camera.orthographic = Orthographic;
                camera.orthographicSize = OrthographicSize;
                camera.nearClipPlane = Near;
                camera.farClipPlane = Far;
                camera.clearFlags = ClearFlags;
                camera.backgroundColor = Background;
            }
        }

        public static void StartAutomatedCapture()
        {
            if (automationActive || EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "Ruins/Cliff evidence capture is already active or Play Mode has started.");
            }

            string requested = Environment.GetEnvironmentVariable(
                OutputEnvironmentVariable);
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            outputRoot = ValidateOutputDirectoryForTests(requested, projectRoot);
            if (Directory.Exists(outputRoot) &&
                Directory.EnumerateFileSystemEntries(outputRoot).Any())
            {
                throw new InvalidOperationException(
                    "Ruins/Cliff evidence output directory must be empty: " + outputRoot);
            }
            Directory.CreateDirectory(outputRoot);

            automationActive = true;
            SessionState.SetBool(ActiveSessionKey, true);
            SessionState.SetString(OutputSessionKey, outputRoot);
            SessionState.SetInt(ExitCodeSessionKey, 1);
            runtimeStartedAt = EditorApplication.timeSinceStartup;
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                EditorApplication.isPlaying = true;
            }
            catch
            {
                DeleteOwnedOutputOnFailure();
                StopCallbacks();
                throw;
            }
        }

        [InitializeOnLoadMethod]
        private static void ResumeAfterDomainReload()
        {
            if (!SessionState.GetBool(ActiveSessionKey, false))
                return;
            automationActive = true;
            outputRoot = SessionState.GetString(OutputSessionKey, string.Empty);
            runtimeStartedAt = EditorApplication.timeSinceStartup;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= TickAutomatedCapture;
            if (EditorApplication.isPlaying)
                EditorApplication.update += TickAutomatedCapture;
        }

        internal static string ValidateOutputDirectoryForTests(
            string requested,
            string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(requested) ||
                !Path.IsPathRooted(requested))
            {
                throw new InvalidOperationException(
                    OutputEnvironmentVariable +
                    " must be a non-empty absolute directory path.");
            }
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new InvalidOperationException("Unity project root is unavailable.");

            string full = Path.GetFullPath(requested);
            string project = Path.GetFullPath(projectRoot);
            string projectPrefix = project.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            const StringComparison pathComparison =
                StringComparison.OrdinalIgnoreCase;
            if (string.Equals(full, project, pathComparison) ||
                full.StartsWith(projectPrefix, pathComparison))
            {
                throw new InvalidOperationException(
                    "Ruins/Cliff evidence must be written outside the Unity project.");
            }
            return full;
        }

        internal static void ValidatePixelsForTests(
            IReadOnlyList<Color32> pixels,
            int width,
            int height,
            string filename)
        {
            if (width < 1 || height < 1 || pixels == null ||
                pixels.Count != checked(width * height))
            {
                throw new InvalidOperationException(
                    "Capture pixel dimensions are invalid for " + filename + ".");
            }

            int lit = 0;
            int magenta = 0;
            for (int index = 0; index < pixels.Count; index++)
            {
                Color32 pixel = pixels[index];
                int luminance = (pixel.r * 54 + pixel.g * 183 + pixel.b * 19) >> 8;
                if (luminance >= 16)
                    lit++;
                if (pixel.r >= 220 && pixel.g <= 60 && pixel.b >= 220)
                    magenta++;
            }
            if (lit < Math.Max(1, pixels.Count / 50))
            {
                throw new InvalidOperationException(
                    "Capture is effectively black: " + filename + ".");
            }
            if (magenta > Math.Max(0, pixels.Count / 1000))
            {
                throw new InvalidOperationException(
                    "Capture contains missing-shader magenta: " + filename + ".");
            }
        }

        internal static void ValidateEvidenceDirectoryForTests(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))
                throw new InvalidOperationException("Evidence directory must be absolute.");
            string manifestPath = Path.Combine(root, "manifest.json");
            if (!File.Exists(manifestPath))
                throw new InvalidOperationException("Evidence manifest is missing.");

            EvidenceManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<EvidenceManifest>(
                    File.ReadAllText(manifestPath));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Evidence manifest is invalid JSON.", exception);
            }
            if (manifest == null || manifest.scene != ScenePath ||
                manifest.seed != GrayboxSceneBootstrap.WorldSeedValue ||
                manifest.captureResult != Passed)
            {
                throw new InvalidOperationException(
                    "Evidence manifest scene, seed, or result is incomplete.");
            }
            ValidateGuid(manifest.terrainProfileGuid, "terrain profile");
            ValidateGuid(manifest.geometryProfileGuid, "geometry profile");
            ValidateGuidSet(manifest.materialGuids, 13, "material");
            ValidateGuidSet(manifest.prefabGuids, 14, "prefab");
            if (manifest.captures == null ||
                manifest.captures.Length != requiredCaptureFileNames.Length)
            {
                throw new InvalidOperationException(
                    "Evidence manifest must contain every required capture.");
            }

            for (int index = 0; index < requiredCaptureFileNames.Length; index++)
            {
                CaptureRecord record = manifest.captures[index];
                string expectedName = requiredCaptureFileNames[index];
                string expectedRuins = index == 10 ? "Fallback" : "Presented";
                string expectedCliff = index == 11 ? "Fallback" : "Presented";
                if (record == null || record.filename != expectedName ||
                    record.result != Passed ||
                    record.ruinsStatus != expectedRuins ||
                    record.cliffStatus != expectedCliff ||
                    string.IsNullOrWhiteSpace(record.worldToCameraMatrix) ||
                    string.IsNullOrWhiteSpace(record.projectionMatrix) ||
                    !IsLowerHex(record.sha256, 64))
                {
                    throw new InvalidOperationException(
                        "Evidence capture record is incomplete: " + expectedName + ".");
                }
                string path = Path.Combine(root, expectedName);
                if (!File.Exists(path))
                {
                    throw new InvalidOperationException(
                        "Required evidence image is missing: " + expectedName + ".");
                }
                byte[] bytes = File.ReadAllBytes(path);
                if (Sha256(bytes) != record.sha256)
                {
                    throw new InvalidOperationException(
                        "Evidence image hash mismatch: " + expectedName + ".");
                }
                DecodeAndValidatePng(bytes, expectedName);
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!automationActive)
                return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                runtimeStartedAt = EditorApplication.timeSinceStartup;
                EditorApplication.update -= TickAutomatedCapture;
                EditorApplication.update += TickAutomatedCapture;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                int exitCode = SessionState.GetInt(ExitCodeSessionKey, 1);
                StopCallbacks();
                EditorApplication.Exit(exitCode);
            }
        }

        private static void TickAutomatedCapture()
        {
            if (!EditorApplication.isPlaying)
                return;
            try
            {
                if (!TryResolveContext(out CaptureContext context))
                {
                    if (EditorApplication.timeSinceStartup - runtimeStartedAt >=
                        RuntimeTimeoutSeconds)
                    {
                        throw new TimeoutException(
                            "Ruins/Cliff runtime did not become capture-ready within 120 seconds.");
                    }
                    return;
                }

                EditorApplication.update -= TickAutomatedCapture;
                CaptureAll(context);
                ValidateEvidenceDirectoryForTests(outputRoot);
                SessionState.SetInt(ExitCodeSessionKey, 0);
                Debug.Log("Ruins/Cliff evidence capture completed: " + outputRoot);
                EditorApplication.isPlaying = false;
            }
            catch (Exception exception)
            {
                EditorApplication.update -= TickAutomatedCapture;
                Debug.LogException(exception);
                DeleteOwnedOutputOnFailure();
                SessionState.SetInt(ExitCodeSessionKey, 1);
                EditorApplication.isPlaying = false;
            }
        }

        private static bool TryResolveContext(out CaptureContext context)
        {
            context = null;
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                throw new InvalidOperationException(
                    "Ruins/Cliff evidence capture opened the wrong scene.");
            }
            GrayboxSceneBootstrap bootstrap = Object.FindObjectOfType<GrayboxSceneBootstrap>(true);
            GrayboxWorldView3D world = Object.FindObjectOfType<GrayboxWorldView3D>(true);
            FirstArtTerrainRenderer3D presenter =
                Object.FindObjectOfType<FirstArtTerrainRenderer3D>(true);
            Camera camera = Camera.main;
            if (bootstrap == null || !bootstrap.IsInitialized || world == null ||
                world.Model == null || world.Coordinates == null || presenter == null ||
                !presenter.IsPresented || camera == null)
                return false;
            if (presenter.GeometryProfile == null ||
                presenter.RuinsStatus != FirstArtRuinsCliffPresentationStatus3D.Presented ||
                presenter.CliffStatus != FirstArtRuinsCliffPresentationStatus3D.Presented)
            {
                if (!string.IsNullOrEmpty(presenter.LastPresentationError) ||
                    !string.IsNullOrEmpty(presenter.RuinsError) ||
                    !string.IsNullOrEmpty(presenter.CliffError))
                {
                    throw new InvalidOperationException(
                        "Formal Ruins/Cliff presentation failed before capture: " +
                        presenter.LastPresentationError + " " + presenter.RuinsError + " " +
                        presenter.CliffError);
                }
                return false;
            }

            context = new CaptureContext
            {
                Bootstrap = bootstrap,
                WorldView = world,
                Presenter = presenter,
                Camera = camera,
                CameraController = camera.GetComponentInParent<GrayboxCameraController3D>(),
                TerrainProfile = presenter.Profile,
                GeometryProfile = presenter.GeometryProfile,
            };
            return true;
        }

        private static void CaptureAll(CaptureContext context)
        {
            var records = new List<CaptureRecord>(requiredCaptureFileNames.Length);
            CameraState cameraState = new CameraState(context.Camera);
            bool controllerEnabled = context.CameraController != null &&
                context.CameraController.enabled;
            if (context.CameraController != null)
                context.CameraController.enabled = false;
            try
            {
                records.Add(Capture(context, requiredCaptureFileNames[0]));

                FocusTopView(context);
                records.Add(Capture(context, requiredCaptureFileNames[1]));

                FirstArtRuinsCliffCatalogEntry3D ruinsCloseupEntry =
                    FirstArtRuinsCliffCatalog3D.Entries[0];
                if (ruinsCloseupEntry.Family != FirstArtRuinsCliffFamily3D.Ruins)
                {
                    throw new InvalidOperationException(
                        "The deterministic Ruins closeup entry is not a Ruins prefab.");
                }
                records.Add(CaptureSinglePrefabFixture(
                    context,
                    ruinsCloseupEntry,
                    requiredCaptureFileNames[2]));

                for (int index = 0; index < 6; index++)
                {
                    FirstArtRuinsCliffCatalogEntry3D entry =
                        FirstArtRuinsCliffCatalog3D.Entries[
                            FirstArtRuinsCliffCatalog3D.RuinsEntryCount + index];
                    records.Add(CaptureSinglePrefabFixture(
                        context,
                        entry,
                        requiredCaptureFileNames[3 + index]));
                }

                cameraState.Restore(context.Camera);
                records.Add(Capture(context, requiredCaptureFileNames[9]));
                records.Add(CaptureFallback(
                    context,
                    FirstArtRuinsCliffFamily3D.Ruins,
                    requiredCaptureFileNames[10]));
                records.Add(CaptureFallback(
                    context,
                    FirstArtRuinsCliffFamily3D.Cliff,
                    requiredCaptureFileNames[11]));

                RestoreApprovedPresentation(context);
                WriteManifest(context, records);
            }
            finally
            {
                cameraState.Restore(context.Camera);
                if (context.CameraController != null)
                    context.CameraController.enabled = controllerEnabled;
                RestoreApprovedPresentation(context);
            }
        }

        private static CaptureRecord CaptureSinglePrefabFixture(
            CaptureContext context,
            FirstArtRuinsCliffCatalogEntry3D entry,
            string filename)
        {
            if (!context.GeometryProfile.TryResolvePrefab(entry.StableId, out GameObject prefab))
                throw new InvalidOperationException(
                    "Evidence fixture prefab is missing: " + entry.StableId);
            var fixtureRoot = new GameObject(
                "Evidence" + entry.Family + "Fixture");
            GameObject fixture = null;
            try
            {
                fixtureRoot.transform.position = new Vector3(0f, 1000f, 0f);
                fixture = Object.Instantiate(prefab, fixtureRoot.transform, false);
                fixture.name = entry.StableId;
                Renderer[] renderers = fixture.GetComponentsInChildren<Renderer>(true);
                Bounds bounds = CombinedBounds(renderers, entry.StableId);
                FocusBounds(context.Camera, bounds);
                context.Camera.farClipPlane = 80f;
                context.Camera.clearFlags = CameraClearFlags.SolidColor;
                context.Camera.backgroundColor = new Color(.11f, .09f, .07f, 1f);
                return Capture(context, filename);
            }
            finally
            {
                DestroyImmediateSafe(fixtureRoot);
            }
        }

        private static CaptureRecord CaptureFallback(
            CaptureContext context,
            FirstArtRuinsCliffFamily3D failedFamily,
            string filename)
        {
            FirstArtRuinsCliffProfile3D invalid = null;
            var invalidPrefabs = new List<GameObject>();
            try
            {
                invalid = CreateSingleFamilyFailureProfile(
                    context.GeometryProfile,
                    failedFamily,
                    invalidPrefabs);
                context.Presenter.Configure(context.TerrainProfile, invalid);
                if (!context.Presenter.TryPresent(context.WorldView, false))
                    throw new InvalidOperationException("Surface failed during fallback evidence setup.");

                FirstArtRuinsCliffPresentationStatus3D ruinsExpected =
                    failedFamily == FirstArtRuinsCliffFamily3D.Ruins
                        ? FirstArtRuinsCliffPresentationStatus3D.Fallback
                        : FirstArtRuinsCliffPresentationStatus3D.Presented;
                FirstArtRuinsCliffPresentationStatus3D cliffExpected =
                    failedFamily == FirstArtRuinsCliffFamily3D.Cliff
                        ? FirstArtRuinsCliffPresentationStatus3D.Fallback
                        : FirstArtRuinsCliffPresentationStatus3D.Presented;
                if (context.Presenter.RuinsStatus != ruinsExpected ||
                    context.Presenter.CliffStatus != cliffExpected)
                {
                    throw new InvalidOperationException(
                        "Selective fallback evidence did not reach the requested state.");
                }
                return Capture(context, filename);
            }
            finally
            {
                RestoreApprovedPresentation(context);
                DestroyImmediateSafe(invalid);
                for (int index = invalidPrefabs.Count - 1; index >= 0; index--)
                    DestroyImmediateSafe(invalidPrefabs[index]);
            }
        }

        private static FirstArtRuinsCliffProfile3D CreateSingleFamilyFailureProfile(
            FirstArtRuinsCliffProfile3D source,
            FirstArtRuinsCliffFamily3D failedFamily,
            List<GameObject> ownedInvalidPrefabs)
        {
            var prefabs = new FirstArtRuinsCliffPrefabBinding3D[
                source.PrefabBindings.Count];
            for (int index = 0; index < prefabs.Length; index++)
            {
                FirstArtRuinsCliffPrefabBinding3D binding = source.PrefabBindings[index];
                FirstArtRuinsCliffCatalogEntry3D entry =
                    FirstArtRuinsCliffCatalog3D.Entries[index];
                GameObject value = binding.Prefab;
                if (entry.Family == failedFamily)
                {
                    value = new GameObject(Path.GetFileNameWithoutExtension(entry.PrefabPath));
                    ownedInvalidPrefabs.Add(value);
                }
                prefabs[index] = new FirstArtRuinsCliffPrefabBinding3D(
                    binding.StableId,
                    value);
            }
            FirstArtRuinsCliffMaterialBinding3D[] materials =
                source.MaterialBindings.Select(binding =>
                    new FirstArtRuinsCliffMaterialBinding3D(
                        binding.Role,
                        binding.Material)).ToArray();
            var profile = ScriptableObject.CreateInstance<FirstArtRuinsCliffProfile3D>();
            profile.name = "Evidence-" + failedFamily + "-FailureProfile";
            profile.Configure(source.GeometryShader, prefabs, materials);
            if (!profile.TryValidate(out string error))
            {
                DestroyImmediateSafe(profile);
                throw new InvalidOperationException(
                    "Injected fallback profile did not retain the profile contract: " + error);
            }
            return profile;
        }

        private static void RestoreApprovedPresentation(CaptureContext context)
        {
            if (context == null || context.Presenter == null || context.WorldView == null)
                return;
            if (context.Presenter.GeometryProfile == context.GeometryProfile &&
                context.Presenter.IsPresented &&
                context.Presenter.RuinsStatus ==
                    FirstArtRuinsCliffPresentationStatus3D.Presented &&
                context.Presenter.CliffStatus ==
                    FirstArtRuinsCliffPresentationStatus3D.Presented)
                return;

            context.Presenter.Configure(context.TerrainProfile, context.GeometryProfile);
            if (!context.Presenter.TryPresent(context.WorldView, false) ||
                context.Presenter.RuinsStatus !=
                    FirstArtRuinsCliffPresentationStatus3D.Presented ||
                context.Presenter.CliffStatus !=
                    FirstArtRuinsCliffPresentationStatus3D.Presented)
            {
                throw new InvalidOperationException(
                    "Approved Ruins/Cliff presentation could not be restored after capture.");
            }
        }

        private static void FocusTopView(CaptureContext context)
        {
            Renderer surface = context.Presenter.SurfaceRenderer;
            if (surface == null)
                throw new InvalidOperationException("Formal terrain surface renderer is missing.");
            Bounds bounds = surface.bounds;
            context.Camera.orthographic = true;
            context.Camera.transform.SetPositionAndRotation(
                bounds.center + Vector3.up * 80f,
                Quaternion.Euler(90f, 0f, 0f));
            context.Camera.orthographicSize = Math.Max(bounds.extents.x, bounds.extents.z) * 1.08f;
            context.Camera.nearClipPlane = .1f;
            context.Camera.farClipPlane = 160f;
        }

        private static void FocusBounds(Camera camera, Bounds bounds)
        {
            Vector3 direction = new Vector3(1f, .72f, -1f).normalized;
            float extent = Math.Max(bounds.extents.x,
                Math.Max(bounds.extents.y, bounds.extents.z));
            camera.orthographic = true;
            camera.orthographicSize = Math.Max(.75f, extent * 1.65f);
            camera.transform.position = bounds.center + direction * Math.Max(3f, extent * 4f);
            camera.transform.rotation = Quaternion.LookRotation(
                bounds.center - camera.transform.position,
                Vector3.up);
            camera.nearClipPlane = .05f;
            camera.farClipPlane = Math.Max(40f, extent * 10f);
        }

        private static Bounds CombinedBounds(Renderer[] renderers, string label)
        {
            if (renderers == null || renderers.Length == 0)
                throw new InvalidOperationException("No renderer exists for " + label + ".");
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static CaptureRecord Capture(CaptureContext context, string filename)
        {
            Camera camera = context.Camera;
            byte[] bytes = RenderCameraPng(camera);
            DecodeAndValidatePng(bytes, filename);
            File.WriteAllBytes(Path.Combine(outputRoot, filename), bytes);
            return new CaptureRecord
            {
                filename = filename,
                sha256 = Sha256(bytes),
                result = Passed,
                ruinsStatus = context.Presenter.RuinsStatus.ToString(),
                cliffStatus = context.Presenter.CliffStatus.ToString(),
                worldToCameraMatrix = MatrixToString(camera.worldToCameraMatrix),
                projectionMatrix = MatrixToString(camera.projectionMatrix),
            };
        }

        private static byte[] RenderCameraPng(Camera camera)
        {
            var target = new RenderTexture(
                CaptureWidth,
                CaptureHeight,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            var readback = new Texture2D(
                CaptureWidth,
                CaptureHeight,
                TextureFormat.RGBA32,
                false,
                false);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                readback.ReadPixels(
                    new Rect(0f, 0f, CaptureWidth, CaptureHeight),
                    0,
                    0,
                    false);
                readback.Apply(false, false);
                return readback.EncodeToPNG();
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                DestroyImmediateSafe(target);
                DestroyImmediateSafe(readback);
            }
        }

        private static void DecodeAndValidatePng(byte[] bytes, string filename)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (bytes == null || bytes.Length == 0 ||
                    !ImageConversion.LoadImage(texture, bytes, false))
                {
                    throw new InvalidOperationException(
                        "Evidence image is not a readable PNG: " + filename + ".");
                }
                if (texture.width != CaptureWidth ||
                    texture.height != CaptureHeight)
                {
                    throw new InvalidOperationException(
                        "Evidence image dimensions must be exactly " +
                        CaptureWidth + "x" + CaptureHeight + ": " + filename + ".");
                }
                ValidatePixelsForTests(
                    texture.GetPixels32(),
                    texture.width,
                    texture.height,
                    filename);
            }
            finally
            {
                DestroyImmediateSafe(texture);
            }
        }

        private static void WriteManifest(
            CaptureContext context,
            IReadOnlyList<CaptureRecord> records)
        {
            var manifest = new EvidenceManifest
            {
                scene = ScenePath,
                seed = GrayboxSceneBootstrap.WorldSeedValue,
                terrainProfileGuid = AssetGuid(context.TerrainProfile, "terrain profile"),
                geometryProfileGuid = AssetGuid(context.GeometryProfile, "geometry profile"),
                materialGuids = context.GeometryProfile.MaterialBindings
                    .Select(binding => AssetGuid(binding.Material, binding.Role))
                    .ToArray(),
                prefabGuids = context.GeometryProfile.PrefabBindings
                    .Select(binding => AssetGuid(binding.Prefab, binding.StableId))
                    .ToArray(),
                captures = records.ToArray(),
                captureResult = Passed,
            };
            if (manifest.captures.Length != requiredCaptureFileNames.Length)
                throw new InvalidOperationException("Not every required capture was produced.");

            string temporary = Path.Combine(outputRoot, "manifest.json.tmp");
            string final = Path.Combine(outputRoot, "manifest.json");
            File.WriteAllText(temporary, JsonUtility.ToJson(manifest, true));
            if (File.Exists(final))
                throw new InvalidOperationException("Evidence manifest already exists.");
            File.Move(temporary, final);
        }

        private static string AssetGuid(Object asset, string label)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            string guid = string.IsNullOrEmpty(path)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(path);
            ValidateGuid(guid, label);
            return guid;
        }

        private static void ValidateGuidSet(string[] values, int count, string label)
        {
            if (values == null || values.Length != count)
                throw new InvalidOperationException("Expected " + count + " " + label + " GUIDs.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++)
            {
                ValidateGuid(values[index], label + " " + index);
                if (!seen.Add(values[index]))
                    throw new InvalidOperationException("Duplicate " + label + " GUID.");
            }
        }

        private static void ValidateGuid(string value, string label)
        {
            if (!IsLowerHex(value, 32))
                throw new InvalidOperationException("Invalid " + label + " GUID.");
        }

        private static bool IsLowerHex(string value, int length)
        {
            if (value == null || value.Length != length)
                return false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    return false;
            }
            return true;
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(
                    sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
            }
        }

        private static string MatrixToString(Matrix4x4 matrix)
        {
            var values = new string[16];
            int valueIndex = 0;
            for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++)
            {
                values[valueIndex++] = matrix[row, column].ToString(
                    "R",
                    CultureInfo.InvariantCulture);
            }
            return string.Join(",", values);
        }

        private static void DeleteOwnedOutputOnFailure()
        {
            if (!string.IsNullOrEmpty(outputRoot) && Directory.Exists(outputRoot))
                Directory.Delete(outputRoot, true);
        }

        private static void StopCallbacks()
        {
            automationActive = false;
            outputRoot = null;
            runtimeStartedAt = 0d;
            EditorApplication.update -= TickAutomatedCapture;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            SessionState.SetBool(ActiveSessionKey, false);
            SessionState.EraseString(OutputSessionKey);
            SessionState.EraseInt(ExitCodeSessionKey);
        }

        private static void DestroyImmediateSafe(Object value)
        {
            if (value == null)
                return;
            Object.DestroyImmediate(value);
        }
    }
}
