using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.World;

namespace WasteCity.Leader
{
    public sealed class FormalLeaderController : MonoBehaviour, ITurretFireRateSource
    {
        [SerializeField] private RescueSiteController rescueSites;
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private Transform visual;
        public LeaderModel Model { get; }=new LeaderModel();
        public float FireRateMultiplier=>Model.Overload.FireRateMultiplier;
        public float AssemblerEfficiency=>Model.AssemblerEfficiency;
        private void Start(){rescueSites.Rescued+=OnRescued;if(visual!=null)visual.gameObject.SetActive(Model.Recruited);}
        private void Update()
        {
            Model.Tick(Time.deltaTime);
            if(visual!=null&&Model.Recruited)visual.position=city.transform.position+new Vector3(1.8f,1.2f,-.2f);
            if(Model.Recruited&&Keyboard.current!=null&&Keyboard.current.qKey.wasPressedThisFrame)Model.Overload.TryActivate();
        }
        private void OnRescued(int index,bool immediate){if(Model.Recruit(immediate)&&visual!=null)visual.gameObject.SetActive(true);}
        public void Restore(bool recruited,bool injured,float cooldown,float boost,float lockout){Model.Restore(recruited,injured,cooldown,boost,lockout);if(visual!=null)visual.gameObject.SetActive(recruited);}
        private void OnGUI()
        {
            if(!Model.Recruited)return;string condition=Model.Injured?"受伤加入 · 过载效果降低":"健康加入";string phase=Model.Overload.Phase==OverloadPhase.Boosting?$"强制过载 {Model.Overload.BoostRemaining:0.0}s":Model.Overload.Phase==OverloadPhase.Lockout?$"炮塔停火 {Model.Overload.LockoutRemaining:0.0}s":Model.Overload.CooldownRemaining>0?$"冷却 {Model.Overload.CooldownRemaining:0.0}s":"就绪";
            GUI.Box(new Rect(18,295,390,72),$"领袖 · 岑烬｜{condition}\n[Q] 强制过载：{phase}\n被动：装配厂效率 ×1.25");
        }
    }
}
