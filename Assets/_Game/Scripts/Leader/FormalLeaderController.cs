using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.World;
using WasteCity.Research;
using WasteCity.Presentation;

namespace WasteCity.Leader
{
    public sealed class FormalLeaderController : MonoBehaviour, ITurretCombatModifierSource
    {
        [SerializeField] private RescueSiteController rescueSites;
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private Transform visual;
        [SerializeField] private ResearchController research;
        [SerializeField] private PlaceholderWorldView world;
        [SerializeField] private float moveSpeed=5f;
        public LeaderModel Model { get; }=new LeaderModel();
        private readonly GeneSpliceAuraModel geneAura=new GeneSpliceAuraModel(5f);
        private bool geneVisualActive;
        private Vector2 manualInput;
        public float FireRateMultiplier=>Model.Overload.FireRateMultiplier;
        public float DamageMultiplier(WasteCity.Combat.DamageType damageType)=>1f;
        public float AssemblerEfficiency=>Model.AssemblerEfficiency;
        public bool GeneSpliceActive=>Model.Recruited&&research!=null&&research.HasGeneSplicing&&Model.Overload.Phase==OverloadPhase.Boosting;
        public DirectControlTarget ControlTarget=>DirectControlRules.Resolve(city?.Deployment?.Mode??CityMode.Mobile,Model.Recruited);
        public Vector2 Position=>visual==null?Vector2.zero:(Vector2)visual.position;
        private void Start(){rescueSites.Rescued+=OnRescued;if(visual!=null)visual.gameObject.SetActive(Model.Recruited);}
        private void Update()
        {
            Model.Tick(Time.deltaTime);
            if(Model.Recruited)
            {
                if(ControlTarget==DirectControlTarget.Leader)
                {
                    if(Keyboard.current==null)ApplyManualInput(Vector2.zero);
                    else ApplyManualInput(new Vector2(
                        (Keyboard.current.dKey.isPressed?1:0)-(Keyboard.current.aKey.isPressed?1:0),
                        (Keyboard.current.wKey.isPressed?1:0)-(Keyboard.current.sKey.isPressed?1:0)));
                }
                TickDirectControl(Time.deltaTime);
            }
            int healing=geneAura.Tick(Time.deltaTime,GeneSpliceActive);if(healing>0)foreach(var building in UnityEngine.Object.FindObjectsOfType<BuildingRuntime>())if(building.Construction.IsComplete&&Vector2.Distance(building.transform.position,city.transform.position)<=8f)building.Health.Value.Heal(healing);
            SyncGeneVisual();
            if(Model.Recruited&&Keyboard.current!=null&&Keyboard.current.qKey.wasPressedThisFrame)Model.Overload.TryActivate();
        }
        private void OnRescued(int index,bool immediate){if(Model.Recruit(immediate)&&visual!=null)visual.gameObject.SetActive(true);}
        public void Restore(bool recruited,bool injured,float cooldown,float boost,float lockout){Model.Restore(recruited,injured,cooldown,boost,lockout);if(visual!=null)visual.gameObject.SetActive(recruited);}
        public void ConfigureWorld(PlaceholderWorldView value)=>world=value;
        public void ApplyManualInput(Vector2 value)=>manualInput=value.sqrMagnitude>1f?value.normalized:value;
        public void TickDirectControl(float delta)
        {
            if(visual==null)return;
            if(ControlTarget!=DirectControlTarget.Leader)
            {
                manualInput=Vector2.zero;AttachToCity();return;
            }
            if(delta<=0f||manualInput.sqrMagnitude<=0f)return;
            Vector2 candidate=(Vector2)visual.position+manualInput.normalized*moveSpeed*delta;
            if(world==null||world.IsPassableWorld(candidate))
                visual.position=new Vector3(candidate.x,candidate.y,visual.position.z);
        }
        public void RestorePosition(float x,float y,bool hasSavedPosition)
        {
            if(visual==null)return;
            if(hasSavedPosition)visual.position=new Vector3(x,y,visual.position.z);
            else AttachToCity();
        }
        private void AttachToCity()
        {
            if(visual!=null&&city!=null)visual.position=city.transform.position+new Vector3(1.8f,1.2f,-.2f);
        }
        private void SyncGeneVisual()
        {
            if(visual==null||geneVisualActive==GeneSpliceActive)return;geneVisualActive=GeneSpliceActive;var renderer=visual.GetComponent<SpriteRenderer>();if(renderer==null)return;Color color=geneVisualActive?new Color(.3f,1f,.35f):new Color(.2f,.85f,.95f);VisualSlot.Attach(visual.gameObject,geneVisualActive?"core.character.cen-jin.gene-spliced":"core.character.cen-jin",renderer,color);
        }
        private void OnGUI()
        {
            if(!Model.Recruited)return;string condition=Model.Injured?"受伤加入 · 过载效果降低":"健康加入";string phase=Model.Overload.Phase==OverloadPhase.Boosting?$"强制过载 {Model.Overload.BoostRemaining:0.0}s":Model.Overload.Phase==OverloadPhase.Lockout?$"炮塔停火 {Model.Overload.LockoutRemaining:0.0}s":Model.Overload.CooldownRemaining>0?$"冷却 {Model.Overload.CooldownRemaining:0.0}s":"就绪";
            GUI.Box(new Rect(18,295,390,72),$"领袖 · 岑烬｜{condition}\n[Q] 强制过载：{phase}\n被动：装配厂效率 ×1.25");
        }
    }
}
