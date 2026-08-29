using System;
using UnityEngine;
using WasteCity.City;
using WasteCity.World;

namespace WasteCity.Graybox3D
{
    public readonly struct GrayboxRuleTimeContext3D
    {
        public GrayboxRuleTimeContext3D(
            float productivityMultiplier,
            float developmentRuleTimeMultiplier)
        {
            ProductivityMultiplier = Normalize(productivityMultiplier);
            DevelopmentRuleTimeMultiplier = Normalize(
                developmentRuleTimeMultiplier);
        }

        public float ProductivityMultiplier { get; }
        public float DevelopmentRuleTimeMultiplier { get; }

        public float EffectiveMultiplier
        {
            get
            {
                double value =
                    (double)ProductivityMultiplier *
                    DevelopmentRuleTimeMultiplier;
                return value >= float.MaxValue
                    ? float.MaxValue
                    : (float)value;
            }
        }

        public float Advance(float deltaSeconds)
        {
            float delta = Normalize(deltaSeconds);
            double value = (double)delta * EffectiveMultiplier;
            return value >= float.MaxValue
                ? float.MaxValue
                : (float)value;
        }

        private static float Normalize(float value)
        {
            return value < 0f || float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : value;
        }
    }

    public interface IGrayboxRuleTimeSource3D
    {
        GrayboxRuleTimeContext3D RuleTimeContext { get; }
    }

    public sealed class GrayboxMobileCityController3D : MonoBehaviour
    {
        private const float ArrivalTolerance = .08f;
        private const float CombatPackingAdvanceMultiplier = .7f;
        private const string CityStableId = "core.city.mobile";

        private static readonly Color MobileColor =
            new Color(.9f, .48f, .1f);
        private static readonly Color FortressColor =
            new Color(.55f, .6f, .65f);
        private static readonly int BaseColorId =
            Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId =
            Shader.PropertyToID("_Color");

        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private GrayboxWorldView3D worldView;
        [SerializeField] private Rigidbody body;
        [SerializeField] private BoxCollider bodyCollider;
        [SerializeField] private MonoBehaviour ruleTimeSourceBehaviour;

        private Vector2 manualInput;
        private CityDeploymentModel deployment;
        private WorldGridPoint[] path = Array.Empty<WorldGridPoint>();
        private int waypointIndex;
        private WorldGridPoint? destination;
        private GrayboxVisualSlot visualSlot;
        private Transform visualTransform;
        private Vector3 visualBaseLocalPosition;
        private Vector3 colliderBaseCenter;
        private bool presentationCaptured;
        private MaterialPropertyBlock visualBlock;
        private FormalWorldPresentationScaleProfile3D presentationProfile;
        private Transform innerCityPlatform;
        private IGrayboxRuleTimeSource3D configuredRuleTimeSource;
        private GrayboxFormalRuleClock3D formalRuleClock;
        private Func<int> aliveEnemyCountSource;

        public CityDeploymentModel Deployment
        {
            get
            {
                EnsureDeployment();
                return deployment;
            }
        }

        public CityMode Mode =>
            deployment?.Mode ?? CityMode.Mobile;
        public bool AutopilotActive { get; private set; }
        public WorldGridPoint? Destination => destination;
        public Vector3 WorldPosition =>
            body == null ? transform.position : body.position;
        public float InnerDeckLocalY
        {
            get
            {
                FormalWorldPresentationScaleProfile3D profile =
                    ResolvePresentationProfile();
                return profile == null
                    ? .15f
                    : profile.DeckLocalY(ResolveFortressFactor());
            }
        }
        public float InnerContentLocalY =>
            InnerDeckLocalY +
            FormalWorldPresentationScaleProfile3D.InnerPlatformThickness +
            FormalWorldPresentationScaleProfile3D.InnerContentLift;
        public CityDeploymentFailure LastDeploymentFailure
        {
            get;
            private set;
        }
        public string LastFailureReason { get; private set; } =
            string.Empty;

        public void Configure(
            GrayboxWorldView3D worldView,
            Rigidbody body,
            BoxCollider bodyCollider)
        {
            EnsureDeployment();
            this.worldView = worldView;
            this.body = body;
            this.bodyCollider = bodyCollider;

            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.constraints =
                    RigidbodyConstraints.FreezePositionY |
                    RigidbodyConstraints.FreezeRotation;
            }

            visualSlot =
                GetComponentInChildren<GrayboxVisualSlot>(true);
            visualTransform = visualSlot?.Renderer?.transform;
            innerCityPlatform = transform.Find("InnerCityPlatform");
            if (!presentationCaptured)
            {
                if (visualTransform != null)
                    visualBaseLocalPosition =
                        visualTransform.localPosition;
                if (bodyCollider != null)
                    colliderBaseCenter = bodyCollider.center;
                presentationCaptured = true;
            }

            UpdatePresentation();
        }

        public void ConfigureRuleTimeSource(
            IGrayboxRuleTimeSource3D ruleTimeSource)
        {
            configuredRuleTimeSource = ruleTimeSource;
            if (ruleTimeSource is MonoBehaviour behaviour)
                ruleTimeSourceBehaviour = behaviour;
        }

        public void ConfigureRuleClock(GrayboxFormalRuleClock3D ruleClock)
        {
            formalRuleClock = ruleClock ??
                throw new ArgumentNullException(nameof(ruleClock));
        }

        public void ConfigureAliveEnemyCountSource(
            Func<int> source)
        {
            aliveEnemyCountSource = source;
        }

        public void ApplyManualInput(Vector2 input)
        {
            manualInput = Vector2.ClampMagnitude(input, 1f);
            if (manualInput.sqrMagnitude > 0f)
                CancelAutopilot("直接驾驶：已取消自动驾驶");
        }

        public bool TrySetDestinationCell(
            int cellX,
            int cellY,
            out string failureReason)
        {
            EnsureDeployment();
            if (Mode != CityMode.Mobile)
            {
                failureReason = "自动驾驶仅在移动态可用";
                LastFailureReason = failureReason;
                return false;
            }
            if (!NavigationReady ||
                !worldView.Coordinates.TryWorldToCell(
                    body.position,
                    out int startX,
                    out int startY) ||
                !CityPathfinder.TryFindPath(
                    worldView.Model,
                    startX,
                    startY,
                    cellX,
                    cellY,
                    out WorldGridPoint[] route))
            {
                failureReason = NavigationReady
                    ? "自动驾驶失败：目标不可达"
                    : "自动驾驶不可用：世界尚未生成";
                LastFailureReason = failureReason;
                return false;
            }

            path = route;
            waypointIndex = 0;
            destination = new WorldGridPoint(cellX, cellY);
            manualInput = Vector2.zero;
            LastFailureReason = string.Empty;
            if (path.Length == 0)
            {
                CancelAutopilot(string.Empty);
                failureReason = string.Empty;
                return true;
            }

            AutopilotActive = true;
            failureReason = string.Empty;
            return true;
        }

        public bool TryToggleDeployment(out string failureReason)
        {
            EnsureDeployment();
            if (Mode == CityMode.Mobile)
            {
                CityDeploymentFailure failure =
                    ResolveDeploymentFailure();
                LastDeploymentFailure = failure;
                if (failure != CityDeploymentFailure.None)
                {
                    failureReason =
                        CityDeploymentRules.FailureReason(failure);
                    LastFailureReason = failureReason;
                    return false;
                }

                CancelAutopilot(string.Empty);
                bool started = deployment.Toggle();
                failureReason = started ? string.Empty : "无法展开";
                LastFailureReason = failureReason;
                UpdatePresentation();
                return started;
            }

            if (Mode == CityMode.Fortress)
            {
                LastDeploymentFailure = CityDeploymentFailure.None;
                bool started = deployment.Toggle();
                failureReason = started ? string.Empty : "无法收起";
                LastFailureReason = failureReason;
                UpdatePresentation();
                return started;
            }

            if (Mode == CityMode.Deploying || Mode == CityMode.Packing)
            {
                LastDeploymentFailure = CityDeploymentFailure.None;
                bool cancelled = deployment.Toggle();
                failureReason = cancelled
                    ? string.Empty
                    : "无法取消城市形态转换";
                LastFailureReason = failureReason;
                UpdatePresentation();
                return cancelled;
            }

            LastDeploymentFailure = CityDeploymentFailure.None;
            failureReason = "城市形态转换进行中";
            LastFailureReason = failureReason;
            return false;
        }

        public void TickMovement(float fixedDeltaTime)
        {
            if (Mode != CityMode.Mobile ||
                !NavigationReady ||
                body == null)
                return;

            bool usingAutopilot =
                manualInput.sqrMagnitude <= 0f && AutopilotActive;
            Vector2 plane =
                worldView.Coordinates.WorldToPlane(body.position);
            if (!worldView.Coordinates.TryWorldToCell(
                    body.position,
                    out int currentX,
                    out int currentY))
                return;

            float step =
                moveSpeed *
                CityTerrainRules.SpeedMultiplier(
                    worldView.Model.Get(currentX, currentY)) *
                Mathf.Max(0f, fixedDeltaTime);
            if (step <= 0f)
                return;

            Vector2 candidate;
            if (usingAutopilot)
            {
                if (!TryGetNextWaypoint(out Vector2 target))
                    return;
                candidate = Vector2.MoveTowards(plane, target, step);
            }
            else
            {
                if (manualInput.sqrMagnitude <= 0f)
                    return;
                candidate =
                    plane + manualInput.normalized * step;
            }

            Vector3 candidateWorld =
                worldView.Coordinates.PlaneToWorld(
                    candidate,
                    body.position.y);
            if (!worldView.Coordinates.TryWorldToCell(
                    candidateWorld,
                    out int nextX,
                    out int nextY) ||
                !CityTerrainRules.IsPassable(
                    worldView.Model.Get(nextX, nextY)))
            {
                if (usingAutopilot)
                    CancelAutopilot(
                        "自动驾驶失败：路径被阻断");
                return;
            }

            body.MovePosition(candidateWorld);
            if (usingAutopilot)
                AdvanceReachedWaypoints(candidate);
        }

        public void TickDeployment(float ruleDeltaSeconds)
        {
            EnsureDeployment();
            int aliveEnemyCount = Math.Max(
                0,
                aliveEnemyCountSource?.Invoke() ?? 0);
            float advance = ResolveRuleTimeContext().Advance(
                ruleDeltaSeconds);
            if (Mode == CityMode.Packing &&
                aliveEnemyCount > 0)
            {
                advance *= CombatPackingAdvanceMultiplier;
            }
            deployment.Tick(advance);
            UpdatePresentation();
        }

        public bool RestoreDeploymentForDevelopment(CityMode mode)
        {
            EnsureDeployment();
            if (mode != CityMode.Mobile && mode != CityMode.Fortress)
                return false;
            deployment.Restore(mode, 0f);
            UpdatePresentation();
            return true;
        }

        public bool CompleteDeploymentTransitionForDevelopment()
        {
            EnsureDeployment();
            if (Mode != CityMode.Deploying && Mode != CityMode.Packing)
                return false;
            deployment.Tick(float.MaxValue);
            UpdatePresentation();
            return true;
        }

        public bool TryGetCurrentCell(out int cellX, out int cellY)
        {
            cellX = -1;
            cellY = -1;
            return NavigationReady &&
                   worldView.Coordinates.TryWorldToCell(
                       WorldPosition,
                       out cellX,
                       out cellY);
        }

        public bool TryRestoreForPersistence(
            Vector3 position,
            CityMode mode,
            CityMode transitionReturnMode,
            float transitionRemainingSeconds,
            bool autopilotActive,
            int destinationX,
            int destinationY,
            out string error)
        {
            EnsureDeployment();
            if (!CanRestoreForPersistence(
                    worldView == null ? null : worldView.Model,
                    out error))
                return false;
            if (!IsFinite(position.x) || !IsFinite(position.y) ||
                !IsFinite(position.z))
            {
                error = "城市位置无效";
                return false;
            }

            var deploymentProbe = new CityDeploymentModel(
                CityDeploymentRules.FormalDeployDurationSeconds,
                CityDeploymentRules.FormalPackDurationSeconds);
            if (!deploymentProbe.TryRestore(
                    mode,
                    transitionReturnMode,
                    transitionRemainingSeconds,
                    out error))
                return false;
            if (!worldView.Coordinates.TryWorldToCell(
                    position,
                    out int startX,
                    out int startY))
            {
                error = "城市位置不在正式世界内";
                return false;
            }

            WorldGridPoint[] restoredPath = Array.Empty<WorldGridPoint>();
            if (autopilotActive)
            {
                if (mode != CityMode.Mobile)
                {
                    error = "只有移动态城市可以恢复自动驾驶";
                    return false;
                }
                if (!CityPathfinder.TryFindPath(
                        worldView.Model,
                        startX,
                        startY,
                        destinationX,
                        destinationY,
                        out restoredPath) ||
                    restoredPath.Length == 0)
                {
                    error = "存档中的自动驾驶目标不可达";
                    return false;
                }
            }

            body.position = position;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            manualInput = Vector2.zero;
            path = restoredPath;
            waypointIndex = 0;
            destination = autopilotActive
                ? new WorldGridPoint(destinationX, destinationY)
                : (WorldGridPoint?)null;
            AutopilotActive = autopilotActive;
            deployment.TryRestore(
                mode,
                transitionReturnMode,
                transitionRemainingSeconds,
                out _);
            LastDeploymentFailure = CityDeploymentFailure.None;
            LastFailureReason = string.Empty;
            UpdatePresentation();
            error = string.Empty;
            return true;
        }

        public bool CanRestoreForPersistence(
            WorldMapModel expectedCurrentWorld,
            out string error)
        {
            if (!NavigationReady)
            {
                error = "城市导航尚未初始化";
                return false;
            }
            if (expectedCurrentWorld == null ||
                !ReferenceEquals(worldView.Model, expectedCurrentWorld))
            {
                error = "城市控制器与世界所有者不一致";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private bool NavigationReady =>
            worldView != null &&
            worldView.Model != null &&
            worldView.Coordinates != null &&
            body != null;

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void Awake()
        {
            EnsureDeployment();
        }

        private void Update()
        {
            float ruleDeltaSeconds = ResolveRuleDelta(
                Time.unscaledDeltaTime);
            TickDeployment(ruleDeltaSeconds);
        }

        private void FixedUpdate()
        {
            float ruleDeltaSeconds = ResolveRuleDelta(
                Time.fixedUnscaledDeltaTime);
            TickMovement(ruleDeltaSeconds);
        }

        private float ResolveRuleDelta(float unscaledDeltaSeconds)
        {
            if (formalRuleClock != null)
                return formalRuleClock.ResolveRuleDelta(
                    unscaledDeltaSeconds);
            return GrayboxFormalRuleClock3D
                .ResolveCompatibilityRuleDelta(unscaledDeltaSeconds);
        }

        private void EnsureDeployment()
        {
            if (deployment == null)
                deployment = new CityDeploymentModel(
                    CityDeploymentRules.FormalDeployDurationSeconds,
                    CityDeploymentRules.FormalPackDurationSeconds);
        }

        private GrayboxRuleTimeContext3D ResolveRuleTimeContext()
        {
            IGrayboxRuleTimeSource3D source =
                configuredRuleTimeSource ??
                ruleTimeSourceBehaviour as IGrayboxRuleTimeSource3D;
            return source == null
                ? new GrayboxRuleTimeContext3D(1f, 1f)
                : source.RuleTimeContext;
        }

        private CityDeploymentFailure ResolveDeploymentFailure()
        {
            if (!NavigationReady ||
                !worldView.Coordinates.TryWorldToCell(
                    body.position,
                    out int cellX,
                    out int cellY))
                return CityDeploymentFailure.OutsideWorld;

            return CityDeploymentRules.Validate(
                worldView.Model,
                cellX,
                cellY);
        }

        private bool TryGetNextWaypoint(out Vector2 target)
        {
            target = default;
            while (AutopilotActive && waypointIndex < path.Length)
            {
                if (!worldView.Coordinates.TryCellToWorld(
                        path[waypointIndex].X,
                        path[waypointIndex].Y,
                        body.position.y,
                        out Vector3 waypoint))
                {
                    CancelAutopilot(
                        "自动驾驶失败：路径被阻断");
                    return false;
                }

                target =
                    worldView.Coordinates.WorldToPlane(waypoint);
                Vector2 plane =
                    worldView.Coordinates.WorldToPlane(body.position);
                if (Vector2.Distance(plane, target) >
                    ArrivalTolerance)
                    return true;
                waypointIndex++;
            }

            if (AutopilotActive && waypointIndex >= path.Length)
                CancelAutopilot(string.Empty);
            return false;
        }

        private void AdvanceReachedWaypoints(Vector2 position)
        {
            while (AutopilotActive && waypointIndex < path.Length)
            {
                worldView.Coordinates.TryCellToWorld(
                    path[waypointIndex].X,
                    path[waypointIndex].Y,
                    body.position.y,
                    out Vector3 waypoint);
                Vector2 target =
                    worldView.Coordinates.WorldToPlane(waypoint);
                if (Vector2.Distance(position, target) >
                    ArrivalTolerance)
                    break;
                waypointIndex++;
            }

            if (AutopilotActive && waypointIndex >= path.Length)
                CancelAutopilot(string.Empty);
        }

        private void CancelAutopilot(string failureReason)
        {
            AutopilotActive = false;
            destination = null;
            path = Array.Empty<WorldGridPoint>();
            waypointIndex = 0;
            if (!string.IsNullOrEmpty(failureReason))
                LastFailureReason = failureReason;
        }

        private void UpdatePresentation()
        {
            float fortressFactor = ResolveFortressFactor();
            FormalWorldPresentationScaleProfile3D profile =
                ResolvePresentationProfile();
            Vector3 visualSize = profile == null
                ? new Vector3(8.6f, .65f, 6.6f)
                : profile.CityVisualSize(Mode, fortressFactor);
            Vector3 colliderSize = profile == null
                ? Vector3.Lerp(
                    new Vector3(3f, 1f, 2f),
                    new Vector3(3f, 1.5f, 3f),
                    fortressFactor)
                : profile.GameplayColliderSize(fortressFactor);
            float visualCenterY = visualSize.y * .5f - .5f;
            float colliderCenterY = colliderSize.y * .5f - .5f;

            if (visualTransform != null)
            {
                visualTransform.localScale = visualSize;
                visualTransform.localPosition = new Vector3(
                    visualBaseLocalPosition.x,
                    visualCenterY,
                    visualBaseLocalPosition.z);
            }

            if (bodyCollider != null)
            {
                bodyCollider.size = colliderSize;
                bodyCollider.center = new Vector3(
                    colliderBaseCenter.x,
                    colliderCenterY,
                    colliderBaseCenter.z);
            }

            if (innerCityPlatform != null)
            {
                Vector2 platformSize = profile == null
                    ? new Vector2(8f, 6f)
                    : profile.InnerPlatformSize;
                float deckY = profile == null
                    ? visualSize.y - .5f
                    : profile.DeckLocalY(fortressFactor);
                innerCityPlatform.localPosition = new Vector3(
                    0f,
                    deckY +
                        FormalWorldPresentationScaleProfile3D
                            .InnerPlatformThickness * .5f,
                    0f);
                innerCityPlatform.localRotation = Quaternion.identity;
                innerCityPlatform.localScale = new Vector3(
                    platformSize.x,
                    FormalWorldPresentationScaleProfile3D
                        .InnerPlatformThickness,
                    platformSize.y);
            }

            MeshRenderer renderer = visualSlot?.Renderer;
            if (renderer == null)
                return;

            if (visualBlock == null)
                visualBlock = new MaterialPropertyBlock();
            Color color =
                Color.Lerp(
                    MobileColor,
                    FortressColor,
                    fortressFactor);
            visualBlock.Clear();
            visualBlock.SetColor(BaseColorId, color);
            visualBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(visualBlock);
        }

        private FormalWorldPresentationScaleProfile3D
            ResolvePresentationProfile()
        {
            if (presentationProfile == null)
            {
                presentationProfile = Resources.Load<
                    FormalWorldPresentationScaleProfile3D>(
                    FormalWorldPresentationScaleProfile3D.ResourcesPath);
            }
            return presentationProfile;
        }

        private float ResolveFortressFactor()
        {
            switch (Mode)
            {
                case CityMode.Deploying:
                    return deployment.Progress;
                case CityMode.Fortress:
                    return 1f;
                case CityMode.Packing:
                    return 1f - deployment.Progress;
                default:
                    return 0f;
            }
        }
    }
}
