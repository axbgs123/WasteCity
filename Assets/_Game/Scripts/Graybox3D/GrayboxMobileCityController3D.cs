using System;
using UnityEngine;
using WasteCity.City;
using WasteCity.World;

namespace WasteCity.Graybox3D
{
    public sealed class GrayboxMobileCityController3D : MonoBehaviour
    {
        private const float ArrivalTolerance = .08f;
        private const string CityStableId = "core.city.mobile";

        private static readonly Vector3 MobileSize =
            new Vector3(3f, 1f, 2f);
        private static readonly Vector3 FortressSize =
            new Vector3(3f, 1.5f, 3f);
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

        public void TickDeployment(float deltaTime)
        {
            EnsureDeployment();
            deployment.Tick(deltaTime);
            UpdatePresentation();
        }

        private bool NavigationReady =>
            worldView != null &&
            worldView.Model != null &&
            worldView.Coordinates != null &&
            body != null;

        private void Awake()
        {
            EnsureDeployment();
        }

        private void Update()
        {
            if (Time.timeScale > 0f)
                TickDeployment(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (Time.timeScale > 0f)
                TickMovement(Time.fixedDeltaTime);
        }

        private void EnsureDeployment()
        {
            if (deployment == null)
                deployment = new CityDeploymentModel(3f, 5f);
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
            Vector3 size =
                Vector3.Lerp(MobileSize, FortressSize, fortressFactor);
            float verticalOffset =
                (size.y - MobileSize.y) * .5f;

            if (visualTransform != null)
            {
                visualTransform.localScale = size;
                visualTransform.localPosition =
                    visualBaseLocalPosition +
                    Vector3.up * verticalOffset;
            }

            if (bodyCollider != null)
            {
                bodyCollider.size = size;
                bodyCollider.center =
                    colliderBaseCenter +
                    Vector3.up * verticalOffset;
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
