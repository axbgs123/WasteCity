using System;
using System.Collections.Generic;
using UnityEngine;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxDefenseSelectionKind3D
    {
        None,
        Tower,
        Enemy,
    }

    public sealed class GrayboxDefenseWorldView3D : MonoBehaviour
    {
        private const int TutorialEnemyPoolCapacity = 8;
        private const float EnemyVisualHeight = .55f;
        private const float TowerVisualHeight = .05f;

        [SerializeField] private Transform enemyRoot;
        [SerializeField] private Transform towerRoot;
        [SerializeField] private Material sharedMaterial;

        private readonly List<EnemyVisual> enemyPool =
            new List<EnemyVisual>(TutorialEnemyPoolCapacity);
        private readonly Dictionary<string, EnemyVisual> enemyById =
            new Dictionary<string, EnemyVisual>(StringComparer.Ordinal);
        private readonly Dictionary<string, TowerVisual> towerById =
            new Dictionary<string, TowerVisual>(StringComparer.Ordinal);
        private readonly Dictionary<string, TowerVisual> nextTowerById =
            new Dictionary<string, TowerVisual>(StringComparer.Ordinal);
        private readonly List<TowerVisual> towerPool =
            new List<TowerVisual>();
        private readonly HashSet<string> retainedTowerIds =
            new HashSet<string>(StringComparer.Ordinal);
        private PlanarCoordinateMapper3D coordinates;
        private Material ownedFallbackMaterial;
        private bool simulationPaused;

        public int EnemyVisualCount { get; private set; }
        public int TowerVisualCount { get; private set; }
        public int PooledEnemyCapacity => enemyPool.Count;
        public int PooledTowerCapacity => towerPool.Count;
        public int VisibleTracerCount { get; private set; }
        public GrayboxDefenseRuntimeSnapshot3D LastSnapshot { get; private set; }
        public int RefreshCount { get; private set; }

        public void Configure(
            Transform configuredEnemyRoot,
            Transform configuredTowerRoot,
            Material configuredSharedMaterial,
            PlanarCoordinateMapper3D configuredCoordinates)
        {
            if (configuredEnemyRoot == null)
                throw new ArgumentNullException(nameof(configuredEnemyRoot));
            if (configuredTowerRoot == null)
                throw new ArgumentNullException(nameof(configuredTowerRoot));
            if (configuredSharedMaterial == null)
                throw new ArgumentNullException(nameof(configuredSharedMaterial));
            coordinates = configuredCoordinates ??
                throw new ArgumentNullException(nameof(configuredCoordinates));

            if (enemyRoot != configuredEnemyRoot ||
                towerRoot != configuredTowerRoot)
            {
                ClearOwnedVisuals();
            }

            enemyRoot = configuredEnemyRoot;
            towerRoot = configuredTowerRoot;
            sharedMaterial = configuredSharedMaterial;
            EnsureEnemyPool(TutorialEnemyPoolCapacity);
            ApplySharedMaterial();
        }

        public void BindCoordinates(
            PlanarCoordinateMapper3D configuredCoordinates)
        {
            coordinates = configuredCoordinates ??
                throw new ArgumentNullException(nameof(configuredCoordinates));
        }

        public void Apply(
            GrayboxDefenseRuntimeSnapshot3D snapshot,
            IReadOnlyList<GrayboxBuildingInstance3D> instances)
        {
            LastSnapshot = snapshot;
            RefreshCount++;
            EnemyVisualCount = 0;
            TowerVisualCount = 0;
            EnsureFallbackConfiguration();
            if (!IsConfigured)
                return;

            ApplyEnemies(snapshot);
            ApplyTowers(snapshot, instances);
            RefreshTracers();
        }

        public void SetSimulationPaused(bool paused)
        {
            if (simulationPaused == paused)
                return;
            simulationPaused = paused;
            RefreshTracers();
        }

        public bool TryPick(
            Ray ray,
            out GrayboxDefenseSelectionKind3D kind,
            out string stableId)
        {
            kind = GrayboxDefenseSelectionKind3D.None;
            stableId = null;
            if (ray.direction.sqrMagnitude <= 0f)
                return false;

            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                Mathf.Infinity,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) =>
                left.distance.CompareTo(right.distance));
            for (int index = 0; index < hits.Length; index++)
            {
                GrayboxDefensePickTarget3D target = hits[index].collider
                    .GetComponentInParent<GrayboxDefensePickTarget3D>();
                if (target == null || target.Owner != this ||
                    !target.gameObject.activeInHierarchy)
                {
                    continue;
                }

                kind = target.Kind;
                stableId = target.StableId;
                return kind != GrayboxDefenseSelectionKind3D.None &&
                       !string.IsNullOrEmpty(stableId);
            }
            return false;
        }

        public bool TryGetTowerObject(string stableId, out GameObject value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(stableId) ||
                !towerById.TryGetValue(stableId, out TowerVisual visual) ||
                !visual.Root.activeSelf)
            {
                return false;
            }
            value = visual.Root;
            return true;
        }

        public bool TryGetEnemyObject(string stableId, out GameObject value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(stableId) ||
                !enemyById.TryGetValue(stableId, out EnemyVisual visual) ||
                !visual.Root.activeSelf)
            {
                return false;
            }
            value = visual.Root;
            return true;
        }

        private bool IsConfigured =>
            enemyRoot != null &&
            towerRoot != null &&
            sharedMaterial != null &&
            coordinates != null;

        private void OnDestroy()
        {
            if (ownedFallbackMaterial != null)
                DestroyOwned(ownedFallbackMaterial);
        }

        private void EnsureFallbackConfiguration()
        {
            if (IsConfigured)
                return;

            Transform fallbackEnemyRoot = enemyRoot;
            if (fallbackEnemyRoot == null)
            {
                fallbackEnemyRoot = new GameObject("Defense.Enemies")
                    .transform;
                fallbackEnemyRoot.SetParent(transform, false);
            }
            Transform fallbackTowerRoot = towerRoot;
            if (fallbackTowerRoot == null)
            {
                fallbackTowerRoot = new GameObject("Defense.Towers")
                    .transform;
                fallbackTowerRoot.SetParent(transform, false);
            }
            Material fallbackMaterial = sharedMaterial;
            if (fallbackMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/InternalErrorShader");
                if (shader == null)
                    return;
                ownedFallbackMaterial = new Material(shader)
                {
                    name = "Defense.Placeholder.SharedFallback",
                };
                fallbackMaterial = ownedFallbackMaterial;
            }
            PlanarCoordinateMapper3D fallbackCoordinates = coordinates ??
                new PlanarCoordinateMapper3D(
                    GrayboxWorldLayout3D.WorldWidth,
                    GrayboxWorldLayout3D.WorldHeight);
            Configure(
                fallbackEnemyRoot,
                fallbackTowerRoot,
                fallbackMaterial,
                fallbackCoordinates);
        }

        private void ApplyEnemies(GrayboxDefenseRuntimeSnapshot3D snapshot)
        {
            enemyById.Clear();
            int requestedCount = snapshot?.Enemies?.Count ?? 0;
            EnsureEnemyPool(Math.Max(TutorialEnemyPoolCapacity, requestedCount));
            for (int index = 0; index < enemyPool.Count; index++)
            {
                EnemyVisual visual = enemyPool[index];
                if (index >= requestedCount)
                {
                    visual.Target.Configure(
                        this,
                        GrayboxDefenseSelectionKind3D.None,
                        null);
                    visual.Root.SetActive(false);
                    continue;
                }

                GrayboxDefenseEnemySnapshot3D enemy = snapshot.Enemies[index];
                visual.Root.name = "Defense.Enemy." + enemy.StableId;
                visual.Root.transform.position = LogicalToWorld(
                    enemy.X,
                    enemy.Z,
                    EnemyVisualHeight);
                visual.Target.Configure(
                    this,
                    GrayboxDefenseSelectionKind3D.Enemy,
                    enemy.StableId);
                visual.Root.SetActive(true);
                enemyById[enemy.StableId] = visual;
                ApplyEnemyState(visual, enemy);
                EnemyVisualCount++;
            }
        }

        private void ApplyTowers(
            GrayboxDefenseRuntimeSnapshot3D snapshot,
            IReadOnlyList<GrayboxBuildingInstance3D> instances)
        {
            retainedTowerIds.Clear();
            nextTowerById.Clear();
            for (int index = 0; index < towerPool.Count; index++)
                towerPool[index].InUse = false;
            int towerCount = snapshot?.Towers?.Count ?? 0;

            // Reserve visuals already associated with surviving tower IDs before
            // assigning pooled slots to new IDs. This keeps snapshot reordering
            // from stealing a visual that still belongs to another active tower.
            for (int index = 0; index < towerCount; index++)
            {
                GrayboxDefenseTowerSnapshot3D tower = snapshot.Towers[index];
                GrayboxBuildingInstance3D instance = FindInstance(
                    instances,
                    tower.StableId);
                if (instance == null ||
                    !retainedTowerIds.Add(tower.StableId))
                {
                    continue;
                }

                if (towerById.TryGetValue(
                        tower.StableId,
                        out TowerVisual retainedVisual) &&
                    !retainedVisual.InUse)
                {
                    retainedVisual.InUse = true;
                    nextTowerById.Add(tower.StableId, retainedVisual);
                }
            }

            for (int index = 0; index < towerCount; index++)
            {
                GrayboxDefenseTowerSnapshot3D tower = snapshot.Towers[index];
                GrayboxBuildingInstance3D instance = FindInstance(
                    instances,
                    tower.StableId);
                if (instance == null ||
                    !retainedTowerIds.Remove(tower.StableId))
                {
                    continue;
                }

                if (!nextTowerById.TryGetValue(
                        tower.StableId,
                        out TowerVisual visual))
                {
                    visual = FirstAvailableTowerVisual();
                    if (visual == null)
                    {
                        visual = CreateTowerVisual(towerPool.Count);
                        towerPool.Add(visual);
                    }
                    visual.InUse = true;
                    nextTowerById.Add(tower.StableId, visual);
                }

                visual.Root.name = "Defense.Tower." + tower.StableId;
                visual.Root.transform.position = LogicalToWorld(
                    instance.Placement.X,
                    instance.Placement.Y,
                    TowerVisualHeight);
                visual.Target.Configure(
                    this,
                    GrayboxDefenseSelectionKind3D.Tower,
                    tower.StableId);
                visual.Root.SetActive(true);
                ApplyTowerState(visual, tower);
                TowerVisualCount++;
            }

            for (int index = 0; index < towerPool.Count; index++)
            {
                TowerVisual visual = towerPool[index];
                if (visual.InUse)
                    continue;
                visual.Tracer.enabled = false;
                visual.Root.SetActive(false);
                visual.Target.Configure(
                    this,
                    GrayboxDefenseSelectionKind3D.None,
                    null);
            }

            towerById.Clear();
            foreach (KeyValuePair<string, TowerVisual> entry in nextTowerById)
                towerById.Add(entry.Key, entry.Value);
        }

        private void EnsureEnemyPool(int capacity)
        {
            while (enemyPool.Count < capacity)
            {
                int index = enemyPool.Count;
                GameObject root = GameObject.CreatePrimitive(
                    PrimitiveType.Capsule);
                root.name = "Defense.Enemy.Pool." + index.ToString("D2");
                root.transform.SetParent(enemyRoot, false);
                root.transform.localScale = new Vector3(.55f, .7f, .55f);
                Renderer renderer = root.GetComponent<Renderer>();
                renderer.sharedMaterial = sharedMaterial;
                GrayboxDefensePickTarget3D target =
                    root.AddComponent<GrayboxDefensePickTarget3D>();
                target.Configure(
                    this,
                    GrayboxDefenseSelectionKind3D.None,
                    null);
                root.SetActive(false);
                enemyPool.Add(new EnemyVisual(root, renderer, target));
            }
        }

        private TowerVisual FirstAvailableTowerVisual()
        {
            for (int index = 0; index < towerPool.Count; index++)
            {
                if (!towerPool[index].InUse)
                    return towerPool[index];
            }
            return null;
        }

        private TowerVisual CreateTowerVisual(int poolIndex)
        {
            var root = new GameObject(
                "Defense.Tower.Pool." + poolIndex.ToString("D2"),
                typeof(BoxCollider));
            root.transform.SetParent(towerRoot, false);
            BoxCollider collider = root.GetComponent<BoxCollider>();
            collider.center = new Vector3(0f, .45f, 0f);
            collider.size = new Vector3(.9f, .9f, .9f);
            GrayboxDefensePickTarget3D target =
                root.AddComponent<GrayboxDefensePickTarget3D>();

            GameObject baseVisual = CreatePrimitiveChild(
                root.transform,
                "Defense.Tower.Base",
                PrimitiveType.Cube,
                new Vector3(0f, .25f, 0f),
                new Vector3(.75f, .5f, .75f));
            GameObject barrelVisual = CreatePrimitiveChild(
                root.transform,
                "Defense.Tower.Barrel",
                PrimitiveType.Cube,
                new Vector3(0f, .7f, .38f),
                new Vector3(.18f, .18f, .95f));
            GameObject tracerObject = new GameObject("Defense.Tower.Tracer");
            tracerObject.transform.SetParent(root.transform, false);
            LineRenderer tracer = tracerObject.AddComponent<LineRenderer>();
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.startWidth = .04f;
            tracer.endWidth = .025f;
            tracer.sharedMaterial = sharedMaterial;
            tracer.enabled = false;
            return new TowerVisual(
                root,
                target,
                tracer,
                new[]
                {
                    baseVisual.GetComponent<Renderer>(),
                    barrelVisual.GetComponent<Renderer>(),
                    tracer,
                });
        }

        private GameObject CreatePrimitiveChild(
            Transform parent,
            string objectName,
            PrimitiveType primitive,
            Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject child = GameObject.CreatePrimitive(primitive);
            child.name = objectName;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;
            Collider collider = child.GetComponent<Collider>();
            if (collider != null)
                DestroyOwned(collider);
            child.GetComponent<Renderer>().sharedMaterial = sharedMaterial;
            return child;
        }

        private void ApplySharedMaterial()
        {
            for (int index = 0; index < enemyPool.Count; index++)
                enemyPool[index].Renderer.sharedMaterial = sharedMaterial;
            for (int towerIndex = 0;
                 towerIndex < towerPool.Count;
                 towerIndex++)
            {
                TowerVisual tower = towerPool[towerIndex];
                for (int index = 0; index < tower.Renderers.Count; index++)
                    tower.Renderers[index].sharedMaterial = sharedMaterial;
            }
        }

        private void RefreshTracers()
        {
            VisibleTracerCount = 0;
            for (int index = 0; index < towerPool.Count; index++)
                towerPool[index].Tracer.enabled = false;
            if (simulationPaused || LastSnapshot?.Towers == null)
                return;

            for (int index = 0; index < LastSnapshot.Towers.Count; index++)
            {
                GrayboxDefenseTowerSnapshot3D tower =
                    LastSnapshot.Towers[index];
                if (tower.Status != GrayboxDefenseTowerStatus3D.Firing ||
                    string.IsNullOrWhiteSpace(tower.TargetId) ||
                    !towerById.TryGetValue(tower.StableId, out TowerVisual visual) ||
                    !enemyById.TryGetValue(tower.TargetId, out EnemyVisual target) ||
                    !visual.Root.activeSelf ||
                    !target.Root.activeSelf)
                {
                    continue;
                }

                visual.Tracer.SetPosition(0, visual.Root.transform.position);
                visual.Tracer.SetPosition(1, target.Root.transform.position);
                visual.Tracer.enabled = true;
                VisibleTracerCount++;
            }
        }

        private static void ApplyEnemyState(
            EnemyVisual visual,
            GrayboxDefenseEnemySnapshot3D enemy)
        {
            float healthRatio = Mathf.Clamp01(
                enemy.CurrentHealth / 60f);
            visual.Properties.SetColor(
                "_Color",
                Color.Lerp(
                    new Color(.25f, .08f, .06f, 1f),
                    new Color(.9f, .28f, .14f, 1f),
                    healthRatio));
            visual.Renderer.SetPropertyBlock(visual.Properties);
        }

        private static void ApplyTowerState(
            TowerVisual visual,
            GrayboxDefenseTowerSnapshot3D tower)
        {
            Color color = tower.PlayerPaused
                ? new Color(.45f, .45f, .45f, 1f)
                : tower.Status == GrayboxDefenseTowerStatus3D.Firing
                    ? new Color(.95f, .7f, .2f, 1f)
                    : new Color(.3f, .65f, .78f, 1f);
            visual.Properties.SetColor("_Color", color);
            for (int index = 0; index < visual.Renderers.Count; index++)
                visual.Renderers[index].SetPropertyBlock(visual.Properties);
        }

        private Vector3 LogicalToWorld(float x, float z, float visualY)
        {
            int cellX = Mathf.FloorToInt(x);
            int cellZ = Mathf.FloorToInt(z);
            if (!coordinates.TryCellToWorld(
                    cellX,
                    cellZ,
                    visualY,
                    out Vector3 world))
            {
                return default;
            }
            world.x += x - cellX;
            world.z += z - cellZ;
            return world;
        }

        private static GrayboxBuildingInstance3D FindInstance(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            string stableId)
        {
            if (instances == null)
                return null;
            for (int index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance != null && string.Equals(
                        instance.StableInstanceId,
                        stableId,
                        StringComparison.Ordinal))
                {
                    return instance;
                }
            }
            return null;
        }

        private void ClearOwnedVisuals()
        {
            for (int index = 0; index < enemyPool.Count; index++)
                DestroyOwned(enemyPool[index].Root);
            for (int index = 0; index < towerPool.Count; index++)
                DestroyOwned(towerPool[index].Root);
            enemyPool.Clear();
            enemyById.Clear();
            towerPool.Clear();
            towerById.Clear();
            nextTowerById.Clear();
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(value);
            else
                UnityEngine.Object.DestroyImmediate(value);
        }

        private sealed class EnemyVisual
        {
            public EnemyVisual(
                GameObject root,
                Renderer renderer,
                GrayboxDefensePickTarget3D target)
            {
                Root = root;
                Renderer = renderer;
                Target = target;
                Properties = new MaterialPropertyBlock();
            }

            public GameObject Root { get; }
            public Renderer Renderer { get; }
            public GrayboxDefensePickTarget3D Target { get; }
            public MaterialPropertyBlock Properties { get; }
        }

        private sealed class TowerVisual
        {
            public TowerVisual(
                GameObject root,
                GrayboxDefensePickTarget3D target,
                LineRenderer tracer,
                IReadOnlyList<Renderer> renderers)
            {
                Root = root;
                Target = target;
                Tracer = tracer;
                Renderers = renderers;
                Properties = new MaterialPropertyBlock();
            }

            public GameObject Root { get; }
            public GrayboxDefensePickTarget3D Target { get; }
            public LineRenderer Tracer { get; }
            public IReadOnlyList<Renderer> Renderers { get; }
            public MaterialPropertyBlock Properties { get; }
            public bool InUse { get; set; }
        }
    }

    internal sealed class GrayboxDefensePickTarget3D : MonoBehaviour
    {
        public GrayboxDefenseWorldView3D Owner { get; private set; }
        public GrayboxDefenseSelectionKind3D Kind { get; private set; }
        public string StableId { get; private set; }

        public void Configure(
            GrayboxDefenseWorldView3D owner,
            GrayboxDefenseSelectionKind3D kind,
            string stableId)
        {
            Owner = owner;
            Kind = kind;
            StableId = stableId;
        }
    }
}
