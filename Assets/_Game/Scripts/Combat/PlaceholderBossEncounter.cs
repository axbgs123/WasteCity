using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Presentation;

namespace WasteCity.Combat
{
    [RequireComponent(typeof(PlaceholderEnemy),typeof(HealthComponent))]
    public sealed class PlaceholderBossEncounter : MonoBehaviour
    {
        private readonly BossEncounterModel model=new BossEncounterModel();
        private readonly List<BossAction> actions=new List<BossAction>();
        private HealthComponent health,cityHealth;private PlaceholderEnemy enemy;private Transform city;private Action<EnemyArchetype,Vector2> spawn;
        private string warning;private float warningRemaining;private static Sprite square;
        public BossPhase Phase=>model.Phase;
        public void Configure(HealthComponent cityTarget,Transform cityTransform,Action<EnemyArchetype,Vector2> spawnEnemy,BossEncounterSnapshot snapshot=null){health=GetComponent<HealthComponent>();enemy=GetComponent<PlaceholderEnemy>();cityHealth=cityTarget;city=cityTransform;spawn=spawnEnemy;model.Restore(snapshot);}
        private void Update()
        {
            if(health==null||health.Value.IsDead)return;actions.Clear();model.Tick(Time.deltaTime,(float)health.Value.Current/health.Value.Maximum,actions);enemy.MoveSpeedMultiplier=model.SpeedMultiplier;
            health.Value.SetPhysicalDamagePercent(health.Value.Shield>0?50:-1);if(warningRemaining>0)warningRemaining-=Time.deltaTime;else warning=null;foreach(var action in actions)Execute(action);
        }
        private void Execute(BossAction action)
        {
            if(action.Type==BossActionType.GrantShield){health.Value.GrantShield(action.Amount);return;}if(action.Type==BossActionType.SummonGnawers){Summon(EnemyArchetype.Gnawer,action.Amount);return;}if(action.Type==BossActionType.SummonHowlers){Summon(EnemyArchetype.Howler,action.Amount);return;}
            if(action.Type==BossActionType.GroundSlamWarning){Warn("地面冲击",action.Duration,"core.vfx.boss-ground-slam-warning",new Color(1f,.35f,.1f),16);return;}if(action.Type==BossActionType.GroundSlam){if(Vector2.Distance(transform.position,city.position)<=8)cityHealth.Value.Apply(action.Amount,DamageType.Physical,cityHealth.Armor);return;}
            if(action.Type==BossActionType.CrystalHazard){Warn("结晶危险区",action.Duration,"core.vfx.boss-crystal-hazard",new Color(.7f,.1f,1f,.65f),6,city.position);return;}if(action.Type==BossActionType.ChargeWarning){Warn("母体冲锋",action.Duration,"core.vfx.boss-charge-warning",Color.yellow,3);return;}if(action.Type==BossActionType.Charge)transform.position=Vector2.MoveTowards(transform.position,city.position,5f);
        }
        private void Summon(EnemyArchetype archetype,int count){for(int i=0;i<count;i++){float angle=i*Mathf.PI*2/Mathf.Max(1,count);spawn?.Invoke(archetype,(Vector2)transform.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*2);}}
        private void Warn(string text,float duration,string visualId,Color color,float size,Vector3? position=null)
        {
            warning=text;warningRemaining=duration;if(square==null)square=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1);var item=new GameObject($"Placeholder_{text}");item.transform.position=position??transform.position;item.transform.localScale=Vector3.one*size;
            var renderer=item.AddComponent<SpriteRenderer>();renderer.sprite=square;renderer.color=color;renderer.sortingOrder=7;VisualSlot.Attach(item,visualId,renderer,color);item.AddComponent<PlaceholderTimedVisual>().Configure(duration);
        }
        public BossEncounterSnapshot Capture()=>model.Capture();
        private void OnGUI(){if(health==null||health.Value.IsDead)return;GUI.Box(new Rect(18,18,330,76),$"晶壳母体 · 阶段 {(int)model.Phase}\n生命 {health.Value.Current}/{health.Value.Maximum} · 护盾 {health.Value.Shield}\n{(string.IsNullOrEmpty(warning)?"行为运行中":$"预警：{warning} {warningRemaining:0.0}s")}");}
    }
    public sealed class PlaceholderTimedVisual:MonoBehaviour{private float remaining;public void Configure(float duration)=>remaining=Mathf.Max(.05f,duration);private void Update(){remaining-=Time.deltaTime;if(remaining<=0)Destroy(gameObject);}}
}
