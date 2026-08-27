using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Combat;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxDefenseSelectionKind3D
    {
        None,
        Tower,
        Enemy,
        Building,
        Ruin,
    }

    public enum GrayboxDefenseTowerVisualKind3D
    {
        MachineGun,
        Laser,
        Spore,
    }

    public sealed class GrayboxDefenseWorldView3D : MonoBehaviour
    {
        private const int FormalEnemyPoolCapacity = 46;
        private const int FormalTowerPoolCapacity = 24;
        private const float EnemyVisualHeight = .55f;
        private const float TowerVisualHeight = .05f;

        [SerializeField] private Transform enemyRoot;
        [SerializeField] private Transform towerRoot;
        [SerializeField] private Material sharedMaterial;

        private readonly List<EnemyVisual> enemyPool =
            new List<EnemyVisual>(FormalEnemyPoolCapacity);
        private readonly Dictionary<string, EnemyVisual> enemyById =
            new Dictionary<string, EnemyVisual>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector3> previousEnemyPositionById =
            new Dictionary<string, Vector3>(
                FormalEnemyPoolCapacity,
                StringComparer.Ordinal);
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
        private ulong lastConsumedAttackEventSequence;
        private ulong lastConsumedSettlementSequence;

        public int EnemyVisualCount { get; private set; }
        public int TowerVisualCount { get; private set; }
        public int PooledEnemyCapacity => enemyPool.Count;
        public int PooledTowerCapacity => towerPool.Count;
        public int PooledTrajectoryCapacity => towerPool.Count;
        public int VisibleTracerCount { get; private set; }
        public int VisibleMachineGunTracerCount { get; private set; }
        public int VisibleLaserBeamCount { get; private set; }
        public int VisibleSporeTrajectoryCount { get; private set; }
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
            EnsureEnemyPool(FormalEnemyPoolCapacity);
            EnsureTowerPool(FormalTowerPoolCapacity);
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

        public bool TryGetTowerVisualKind(
            string stableId,
            out GrayboxDefenseTowerVisualKind3D kind)
        {
            kind = GrayboxDefenseTowerVisualKind3D.MachineGun;
            if (string.IsNullOrWhiteSpace(stableId) ||
                !towerById.TryGetValue(stableId, out TowerVisual visual) ||
                !visual.Root.activeSelf)
            {
                return false;
            }
            kind = visual.Kind;
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
            previousEnemyPositionById.Clear();
            foreach (KeyValuePair<string, EnemyVisual> entry in enemyById)
            {
                if (entry.Value.Root.activeSelf)
                {
                    previousEnemyPositionById[entry.Key] =
                        entry.Value.Root.transform.position;
                }
            }
            enemyById.Clear();
            int requestedCount = snapshot?.Enemies?.Count ?? 0;
            EnsureEnemyPool(FormalEnemyPoolCapacity);
            int visibleCount = Math.Min(requestedCount, enemyPool.Count);
            for (int index = 0; index < enemyPool.Count; index++)
            {
                EnemyVisual visual = enemyPool[index];
                if (index >= visibleCount)
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
            EnsureTowerPool(FormalTowerPoolCapacity);
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
                        continue;
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
                visual.SetKind(ResolveTowerVisualKind(instance));
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
            capacity = Math.Min(capacity, FormalEnemyPoolCapacity);
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
                GameObject outline = GameObject.CreatePrimitive(
                    PrimitiveType.Sphere);
                outline.name = "Defense.Enemy.Outline";
                outline.transform.SetParent(root.transform, false);
                outline.transform.localPosition = Vector3.zero;
                outline.transform.localScale = Vector3.one * 1.18f;
                Collider outlineCollider = outline.GetComponent<Collider>();
                if (outlineCollider != null) DestroyOwned(outlineCollider);
                Renderer outlineRenderer = outline.GetComponent<Renderer>();
                outlineRenderer.sharedMaterial = sharedMaterial;
                outline.SetActive(false);
                var labelObject = new GameObject(
                    "Defense.Enemy.StatusLabel",
                    typeof(TextMesh));
                labelObject.transform.SetParent(root.transform, false);
                labelObject.transform.localPosition = new Vector3(0f, 1.25f, 0f);
                TextMesh label = labelObject.GetComponent<TextMesh>();
                label.anchor = TextAnchor.LowerCenter;
                label.alignment = TextAlignment.Center;
                label.characterSize = .12f;
                label.fontSize = 32;
                label.color = Color.white;
                Renderer labelRenderer = labelObject.GetComponent<Renderer>();
                labelRenderer.sharedMaterial = sharedMaterial;
                labelObject.SetActive(false);
                GameObject healthBar = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                healthBar.name = "Defense.Enemy.WorldHealthBar";
                healthBar.transform.SetParent(root.transform, false);
                healthBar.transform.localPosition = new Vector3(0f, 1.05f, 0f);
                healthBar.transform.localScale = new Vector3(1f, .08f, .08f);
                Collider healthCollider = healthBar.GetComponent<Collider>();
                if (healthCollider != null) DestroyOwned(healthCollider);
                Renderer healthRenderer = healthBar.GetComponent<Renderer>();
                healthRenderer.sharedMaterial = sharedMaterial;
                healthBar.SetActive(false);
                GrayboxDefensePickTarget3D target =
                    root.AddComponent<GrayboxDefensePickTarget3D>();
                target.Configure(
                    this,
                    GrayboxDefenseSelectionKind3D.None,
                    null);
                root.SetActive(false);
                enemyPool.Add(new EnemyVisual(
                    root, renderer, outlineRenderer, label, labelRenderer,
                    healthBar, healthRenderer, target));
            }
        }

        private void EnsureTowerPool(int capacity)
        {
            capacity = Math.Min(capacity, FormalTowerPoolCapacity);
            while (towerPool.Count < capacity)
                towerPool.Add(CreateTowerVisual(towerPool.Count));
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

            var renderers = new List<Renderer>(8);
            GameObject baseVisual = CreatePrimitiveChild(
                root.transform,
                "Defense.Tower.Base",
                PrimitiveType.Cube,
                new Vector3(0f, .25f, 0f),
                new Vector3(.75f, .5f, .75f));
            renderers.Add(baseVisual.GetComponent<Renderer>());

            Transform machineGunGroup = CreateGroup(
                root.transform,
                "Defense.Tower.MachineGun");
            GameObject leftBarrel = CreatePrimitiveChild(
                machineGunGroup,
                "Defense.Tower.MachineGun.Barrel.Left",
                PrimitiveType.Cube,
                new Vector3(-.13f, .7f, .38f),
                new Vector3(.12f, .12f, .95f));
            GameObject rightBarrel = CreatePrimitiveChild(
                machineGunGroup,
                "Defense.Tower.MachineGun.Barrel.Right",
                PrimitiveType.Cube,
                new Vector3(.13f, .7f, .38f),
                new Vector3(.12f, .12f, .95f));
            renderers.Add(leftBarrel.GetComponent<Renderer>());
            renderers.Add(rightBarrel.GetComponent<Renderer>());

            Transform laserGroup = CreateGroup(
                root.transform,
                "Defense.Tower.Laser");
            GameObject laserEmitter = CreatePrimitiveChild(
                laserGroup,
                "Defense.Tower.Laser.Emitter",
                PrimitiveType.Cylinder,
                new Vector3(0f, .68f, .26f),
                new Vector3(.18f, .42f, .18f),
                new Vector3(90f, 0f, 0f));
            GameObject laserLens = CreatePrimitiveChild(
                laserGroup,
                "Defense.Tower.Laser.Lens",
                PrimitiveType.Sphere,
                new Vector3(0f, .68f, .7f),
                new Vector3(.3f, .3f, .16f));
            renderers.Add(laserEmitter.GetComponent<Renderer>());
            renderers.Add(laserLens.GetComponent<Renderer>());

            Transform sporeGroup = CreateGroup(
                root.transform,
                "Defense.Tower.Spore");
            GameObject sporeStalk = CreatePrimitiveChild(
                sporeGroup,
                "Defense.Tower.Spore.Stalk",
                PrimitiveType.Cylinder,
                new Vector3(0f, .55f, 0f),
                new Vector3(.16f, .32f, .16f));
            GameObject sporeBulb = CreatePrimitiveChild(
                sporeGroup,
                "Defense.Tower.Spore.Bulb",
                PrimitiveType.Sphere,
                new Vector3(0f, .92f, 0f),
                new Vector3(.52f, .38f, .52f));
            renderers.Add(sporeStalk.GetComponent<Renderer>());
            renderers.Add(sporeBulb.GetComponent<Renderer>());

            GameObject tracerObject = new GameObject(
                "Defense.Tower.Trajectory");
            tracerObject.transform.SetParent(root.transform, false);
            LineRenderer tracer = tracerObject.AddComponent<LineRenderer>();
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.startWidth = .04f;
            tracer.endWidth = .025f;
            tracer.sharedMaterial = sharedMaterial;
            tracer.enabled = false;
            renderers.Add(tracer);
            var visual = new TowerVisual(
                root,
                target,
                tracer,
                renderers,
                machineGunGroup.gameObject,
                laserGroup.gameObject,
                sporeGroup.gameObject);
            visual.SetKind(GrayboxDefenseTowerVisualKind3D.MachineGun);
            root.SetActive(false);
            return visual;
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private GameObject CreatePrimitiveChild(
            Transform parent,
            string objectName,
            PrimitiveType primitive,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles = default)
        {
            GameObject child = GameObject.CreatePrimitive(primitive);
            child.name = objectName;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localEulerAngles = localEulerAngles;
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
            {
                enemyPool[index].Renderer.sharedMaterial = sharedMaterial;
                enemyPool[index].Outline.sharedMaterial = sharedMaterial;
                enemyPool[index].LabelRenderer.sharedMaterial = sharedMaterial;
                enemyPool[index].HealthRenderer.sharedMaterial = sharedMaterial;
            }
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
            VisibleMachineGunTracerCount = 0;
            VisibleLaserBeamCount = 0;
            VisibleSporeTrajectoryCount = 0;
            for (int index = 0; index < towerPool.Count; index++)
                towerPool[index].Tracer.enabled = false;
            IReadOnlyList<GrayboxDefenseSettledAttackEvent3D> attacks =
                LastSnapshot?.SettledAttackEvents;
            if (attacks == null || attacks.Count == 0)
                return;

            ulong batchSettlementSequence = 0ul;
            for (var index = 0; index < attacks.Count; index++)
            {
                batchSettlementSequence = Math.Max(
                    batchSettlementSequence,
                    attacks[index].SettlementSequence);
            }
            if (batchSettlementSequence < lastConsumedSettlementSequence)
            {
                lastConsumedAttackEventSequence = 0ul;
                lastConsumedSettlementSequence = 0ul;
            }

            for (int index = 0; index < attacks.Count; index++)
            {
                GrayboxDefenseSettledAttackEvent3D attack = attacks[index];
                if (attack.EventSequence <=
                    lastConsumedAttackEventSequence)
                {
                    continue;
                }
                lastConsumedAttackEventSequence = attack.EventSequence;
                lastConsumedSettlementSequence = Math.Max(
                    lastConsumedSettlementSequence,
                    attack.SettlementSequence);
                if (simulationPaused ||
                    !towerById.TryGetValue(
                        attack.TowerStableId,
                        out TowerVisual visual) ||
                    !visual.Root.activeSelf ||
                    !TryResolveAttackEndpoint(
                        attack.TargetStableId,
                        out Vector3 end) ||
                    visual.Tracer.enabled)
                {
                    continue;
                }

                Vector3 start = visual.Root.transform.position;
                visual.Tracer.SetPosition(0, start);
                if (visual.Kind == GrayboxDefenseTowerVisualKind3D.Spore)
                {
                    visual.Tracer.SetPosition(
                        1,
                        (start + end) * .5f + Vector3.up * 1.4f);
                    visual.Tracer.SetPosition(2, end);
                    VisibleSporeTrajectoryCount++;
                }
                else
                {
                    visual.Tracer.SetPosition(1, end);
                    if (visual.Kind ==
                        GrayboxDefenseTowerVisualKind3D.Laser)
                    {
                        VisibleLaserBeamCount++;
                    }
                    else
                    {
                        VisibleMachineGunTracerCount++;
                    }
                }
                visual.Tracer.enabled = true;
                VisibleTracerCount++;
            }
        }

        private bool TryResolveAttackEndpoint(
            string targetStableId,
            out Vector3 position)
        {
            if (enemyById.TryGetValue(
                    targetStableId,
                    out EnemyVisual target) &&
                target.Root.activeSelf)
            {
                position = target.Root.transform.position;
                return true;
            }
            return previousEnemyPositionById.TryGetValue(
                targetStableId,
                out position);
        }

        private static GrayboxDefenseTowerVisualKind3D ResolveTowerVisualKind(
            GrayboxBuildingInstance3D instance)
        {
            string buildingId = instance?.Placement?.Definition?.Id.Value;
            if (string.Equals(
                    buildingId,
                    BuildingCatalog.LaserTower.Id.Value,
                    StringComparison.Ordinal))
            {
                return GrayboxDefenseTowerVisualKind3D.Laser;
            }
            if (string.Equals(
                    buildingId,
                    BuildingCatalog.SporeTower.Id.Value,
                    StringComparison.Ordinal))
            {
                return GrayboxDefenseTowerVisualKind3D.Spore;
            }
            return GrayboxDefenseTowerVisualKind3D.MachineGun;
        }

        private static void ApplyEnemyState(
            EnemyVisual visual,
            GrayboxDefenseEnemySnapshot3D enemy)
        {
            float healthRatio = Mathf.Clamp01(
                enemy.CurrentHealth / (float)enemy.MaximumHealth);
            bool isBoss = string.Equals(
                enemy.EnemyDefinitionId,
                EnemyCatalog.CrystalBroodmother.Id.Value,
                StringComparison.Ordinal);
            bool isBurrower = string.Equals(
                enemy.EnemyDefinitionId,
                EnemyCatalog.Burrower.Id.Value,
                StringComparison.Ordinal);
            visual.Root.name = isBoss
                ? "CrystalBroodmother.Placeholder"
                : "Defense.Enemy." + enemy.StableId;
            visual.Root.transform.localScale = isBoss
                ? new Vector3(2f, 2.4f, 2f)
                : isBurrower
                    ? new Vector3(.9f, .55f, .9f)
                    : new Vector3(.55f, .7f, .55f);
            Color healthy = isBoss
                ? new Color(.82f, .18f, .72f, 1f)
                : isBurrower
                    ? new Color(.58f, .3f, .72f, 1f)
                    : new Color(.9f, .28f, .14f, 1f);
            visual.Properties.SetColor(
                "_Color",
                Color.Lerp(
                    new Color(.25f, .08f, .06f, 1f),
                    healthy,
                    healthRatio));
            visual.Renderer.SetPropertyBlock(visual.Properties);
            visual.Outline.gameObject.SetActive(isBoss);
            visual.Outline.gameObject.name = isBoss
                ? "CrystalBroodmother.Outline"
                : "Defense.Enemy.Outline";
            if (isBoss)
            {
                visual.OutlineProperties.SetColor(
                    "_Color",
                    new Color(.15f, .95f, 1f, 1f));
                visual.Outline.SetPropertyBlock(visual.OutlineProperties);
            }
            visual.Label.gameObject.SetActive(isBoss || isBurrower);
            visual.Label.gameObject.name = isBoss
                ? "CrystalBroodmother.Phase"
                : "Defense.Enemy.StatusLabel";
            visual.Label.text = isBoss
                ? "晶壳母体  " + BossPhase(healthRatio) + "\n" +
                  enemy.CurrentHealth + "/" + enemy.MaximumHealth
                : isBurrower ? "结晶掘地者" : string.Empty;
            visual.HealthBar.SetActive(isBoss);
            visual.HealthBar.name = isBoss
                ? "CrystalBroodmother.WorldHealthBar"
                : "Defense.Enemy.WorldHealthBar";
            if (isBoss)
            {
                visual.HealthBar.transform.localScale = new Vector3(
                    Mathf.Max(.02f, healthRatio), .08f, .08f);
                visual.HealthProperties.SetColor(
                    "_Color",
                    Color.Lerp(Color.red, Color.green, healthRatio));
                visual.HealthRenderer.SetPropertyBlock(
                    visual.HealthProperties);
            }
        }

        private static string BossPhase(float healthRatio)
        {
            if (healthRatio > .7f) return "阶段一";
            return healthRatio > .35f ? "阶段二" : "阶段三";
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
            previousEnemyPositionById.Clear();
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
                Renderer outline,
                TextMesh label,
                Renderer labelRenderer,
                GameObject healthBar,
                Renderer healthRenderer,
                GrayboxDefensePickTarget3D target)
            {
                Root = root;
                Renderer = renderer;
                Outline = outline;
                Label = label;
                LabelRenderer = labelRenderer;
                HealthBar = healthBar;
                HealthRenderer = healthRenderer;
                Target = target;
                Properties = new MaterialPropertyBlock();
                OutlineProperties = new MaterialPropertyBlock();
                HealthProperties = new MaterialPropertyBlock();
            }

            public GameObject Root { get; }
            public Renderer Renderer { get; }
            public Renderer Outline { get; }
            public TextMesh Label { get; }
            public Renderer LabelRenderer { get; }
            public GameObject HealthBar { get; }
            public Renderer HealthRenderer { get; }
            public GrayboxDefensePickTarget3D Target { get; }
            public MaterialPropertyBlock Properties { get; }
            public MaterialPropertyBlock OutlineProperties { get; }
            public MaterialPropertyBlock HealthProperties { get; }
        }

        private sealed class TowerVisual
        {
            public TowerVisual(
                GameObject root,
                GrayboxDefensePickTarget3D target,
                LineRenderer tracer,
                IReadOnlyList<Renderer> renderers,
                GameObject machineGunGroup,
                GameObject laserGroup,
                GameObject sporeGroup)
            {
                Root = root;
                Target = target;
                Tracer = tracer;
                Renderers = renderers;
                MachineGunGroup = machineGunGroup;
                LaserGroup = laserGroup;
                SporeGroup = sporeGroup;
                Properties = new MaterialPropertyBlock();
            }

            public GameObject Root { get; }
            public GrayboxDefensePickTarget3D Target { get; }
            public LineRenderer Tracer { get; }
            public IReadOnlyList<Renderer> Renderers { get; }
            public MaterialPropertyBlock Properties { get; }
            public bool InUse { get; set; }
            public GrayboxDefenseTowerVisualKind3D Kind { get; private set; }

            private GameObject MachineGunGroup { get; }
            private GameObject LaserGroup { get; }
            private GameObject SporeGroup { get; }

            public void SetKind(GrayboxDefenseTowerVisualKind3D kind)
            {
                Kind = kind;
                MachineGunGroup.SetActive(
                    kind == GrayboxDefenseTowerVisualKind3D.MachineGun);
                LaserGroup.SetActive(
                    kind == GrayboxDefenseTowerVisualKind3D.Laser);
                SporeGroup.SetActive(
                    kind == GrayboxDefenseTowerVisualKind3D.Spore);
                if (kind == GrayboxDefenseTowerVisualKind3D.Laser)
                {
                    Tracer.positionCount = 2;
                    Tracer.startWidth = .085f;
                    Tracer.endWidth = .085f;
                }
                else if (kind == GrayboxDefenseTowerVisualKind3D.Spore)
                {
                    Tracer.positionCount = 3;
                    Tracer.startWidth = .09f;
                    Tracer.endWidth = .04f;
                }
                else
                {
                    Tracer.positionCount = 2;
                    Tracer.startWidth = .04f;
                    Tracer.endWidth = .025f;
                }
            }
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
