using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.World;

namespace WasteCity.City
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlaceholderMobileCity : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private PlaceholderWorldView world;
        private Rigidbody2D body;
        private Vector2 manualInput;
        private CityDeploymentModel deployment;
        private readonly List<WorldGridPoint> path = new List<WorldGridPoint>();
        private int waypointIndex;
        public CityDeploymentModel Deployment => deployment;
        public bool LongWorkAllowed => deployment != null && CityOperationalRules.LongWorkAllowed(deployment.Mode);
        public bool AutopilotActive { get; private set; }
        public int DestinationX { get; private set; } = -1;
        public int DestinationY { get; private set; } = -1;
        public string LastMobilityMessage { get; private set; } = "直接驾驶";
        public bool NavigationReady => world != null && world.Model != null;
        public float CurrentTerrainMultiplier
        {
            get
            {
                Vector2 position=body==null?(Vector2)transform.position:body.position;
                return NavigationReady&&world.TryWorldToCell(position,out int x,out int y)
                    ? CityTerrainRules.SpeedMultiplier(world.Model.Get(x,y))
                    : 1f;
            }
        }
        private void Awake() { body = GetComponent<Rigidbody2D>(); deployment = new CityDeploymentModel(3f, 5f); }
        private void Update()
        {
            deployment.Tick(Time.deltaTime);
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) TryToggleDeployment(out _);
            if(deployment.Mode==CityMode.Mobile&&Mouse.current!=null&&Mouse.current.rightButton.wasPressedThisFrame&&Camera.main!=null)
                TrySetDestination(Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()),out _);
            if (Keyboard.current == null) { ApplyManualInput(Vector2.zero); return; }
            if (deployment.Mode != CityMode.Mobile) { manualInput = Vector2.zero; return; }
            ApplyManualInput(new Vector2(
                (Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.aKey.isPressed ? 1 : 0),
                (Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0)));
        }
        private void FixedUpdate()
        {
            if(body==null||deployment==null||deployment.Mode!=CityMode.Mobile)return;
            Vector2 direction=manualInput.sqrMagnitude>0f?manualInput.normalized:DirectionToNextWaypoint();
            if(direction.sqrMagnitude<=0f)return;
            float step=moveSpeed*CurrentTerrainMultiplier*Time.fixedDeltaTime;
            Vector2 candidate;
            if(manualInput.sqrMagnitude>0f)candidate=body.position+direction*step;
            else candidate=Vector2.MoveTowards(body.position,world.CellToWorld(path[waypointIndex].X,path[waypointIndex].Y),step);
            if(world!=null&&!world.IsPassableWorld(candidate))
            {
                if(AutopilotActive)CancelNavigation("自动驾驶失败：路径被阻断");
                return;
            }
            body.MovePosition(candidate);
            if(manualInput.sqrMagnitude<=0f)AdvanceReachedWaypoints(candidate);
        }
        public void ConfigureWorld(PlaceholderWorldView value)=>world=value;
        public void ApplyManualInput(Vector2 value)
        {
            manualInput=value.sqrMagnitude>1f?value.normalized:value;
            if(manualInput.sqrMagnitude>0f&&AutopilotActive)CancelNavigation("直接驾驶：已取消自动驾驶");
        }
        public bool TrySetDestination(Vector2 worldPosition,out string reason)
        {
            if(world==null||!world.TryWorldToCell(worldPosition,out int x,out int y))
            {
                reason=NavigationReady?"自动驾驶失败：目标不可达":"自动驾驶不可用：世界尚未生成";
                LastMobilityMessage=reason;return false;
            }
            return TrySetDestinationCell(x,y,out reason);
        }
        public bool TrySetDestinationCell(int x,int y,out string reason)
        {
            if(deployment.Mode!=CityMode.Mobile){reason="自动驾驶仅在移动态可用";LastMobilityMessage=reason;return false;}
            if(!NavigationReady){reason="自动驾驶不可用：世界尚未生成";LastMobilityMessage=reason;return false;}
            if(!world.TryWorldToCell(transform.position,out int startX,out int startY)||
               !CityPathfinder.TryFindPath(world.Model,startX,startY,x,y,out WorldGridPoint[] route))
            {
                reason="自动驾驶失败：目标不可达";LastMobilityMessage=reason;return false;
            }
            path.Clear();path.AddRange(route);waypointIndex=0;DestinationX=x;DestinationY=y;manualInput=Vector2.zero;
            if(path.Count==0){CancelNavigation("自动驾驶：已到达");reason=LastMobilityMessage;return true;}
            AutopilotActive=true;reason=$"自动驾驶：目标 ({x},{y})";LastMobilityMessage=reason;return true;
        }
        public void RestoreNavigation(bool active,int x,int y)
        {
            CancelNavigation(string.Empty);
            if(active)TrySetDestinationCell(x,y,out _);
        }
        public bool TryToggleDeployment(out string reason)
        {
            if(deployment.Mode==CityMode.Mobile)
            {
                if(NavigationReady)
                {
                    if(!world.TryWorldToCell(transform.position,out int x,out int y))
                    {reason=CityDeploymentRules.FailureReason(CityDeploymentFailure.OutsideWorld);LastMobilityMessage=reason;return false;}
                    CityDeploymentFailure failure=CityDeploymentRules.Validate(world.Model,x,y);
                    if(failure!=CityDeploymentFailure.None)
                    {reason=CityDeploymentRules.FailureReason(failure);LastMobilityMessage=reason;return false;}
                }
                CancelNavigation(string.Empty);bool started=deployment.Toggle();reason=started?"开始展开":"无法展开";LastMobilityMessage=reason;return started;
            }
            if(deployment.Mode==CityMode.Fortress)
            {
                bool started=deployment.Toggle();reason=started?"开始收起":"无法收起";LastMobilityMessage=reason;return started;
            }
            reason="城市形态转换进行中";LastMobilityMessage=reason;return false;
        }
        public void RestoreDeployment(CityMode mode,float remaining)=>deployment.Restore(mode,remaining);
        private Vector2 DirectionToNextWaypoint()
        {
            if(!AutopilotActive||world==null||waypointIndex>=path.Count)return Vector2.zero;
            Vector2 target=world.CellToWorld(path[waypointIndex].X,path[waypointIndex].Y);
            if(Vector2.Distance(body.position,target)<=.08f)
            {
                AdvanceReachedWaypoints(body.position);
                if(!AutopilotActive)return Vector2.zero;
                target=world.CellToWorld(path[waypointIndex].X,path[waypointIndex].Y);
            }
            return (target-body.position).normalized;
        }
        private void AdvanceReachedWaypoints(Vector2 position)
        {
            while(AutopilotActive&&waypointIndex<path.Count&&Vector2.Distance(position,world.CellToWorld(path[waypointIndex].X,path[waypointIndex].Y))<=.08f)waypointIndex++;
            if(AutopilotActive&&waypointIndex>=path.Count)CancelNavigation("自动驾驶：已到达");
        }
        private void CancelNavigation(string message)
        {
            AutopilotActive=false;DestinationX=-1;DestinationY=-1;path.Clear();waypointIndex=0;
            if(!string.IsNullOrEmpty(message))LastMobilityMessage=message;
        }
    }
}
