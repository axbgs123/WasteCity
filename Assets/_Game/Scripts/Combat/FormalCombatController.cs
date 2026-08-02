using UnityEngine;
using WasteCity.Progression;
using WasteCity.Economy;
using System;
using WasteCity.Presentation;
using WasteCity.City;
using WasteCity.Building;
using System.Collections.Generic;

namespace WasteCity.Combat
{
    [Serializable] public sealed class EnemySnapshot { public int archetype,waveTrigger,health,shield;public float x,y;public BossEncounterSnapshot boss; }
    public sealed class FormalCombatController : MonoBehaviour
    {
        [SerializeField] private HealthComponent cityHealth;
        [SerializeField] private Transform city;
        [SerializeField] private FormalProgressionController progression;
        [SerializeField] private FormalEconomyController economy;
        [SerializeField] private PlaceholderMobileCity cityState;
        [SerializeField] private PlaceholderBuildingController buildings;
        private float defenseRemainder;
        private readonly WaveDirectorModel waves=new WaveDirectorModel();
        private readonly List<EnemyArchetype> spawnBuffer=new List<EnemyArchetype>();
        public int SpawnedEnemies { get; private set; }
        public event Action<bool> EnemyDefeated;
        private static Sprite square;
        private void Start(){progression.Observation.ThresholdReached+=OnThreshold;buildings.BuildingPlaced+=OnBuildingPlaced;}
        private void Update()
        {
            spawnBuffer.Clear();waves.Tick(Time.deltaTime,spawnBuffer);for(int i=0;i<spawnBuffer.Count;i++)Spawn(spawnBuffer[i],waves.Current?.Trigger??0,SpawnedEnemies);
            PlaceholderEnemy nearest = null; float best = 25f;
            foreach (var enemy in UnityEngine.Object.FindObjectsOfType<PlaceholderEnemy>()) { float sqr = ((Vector2)(enemy.transform.position - city.position)).sqrMagnitude; if (sqr < best) { best = sqr; nearest = enemy; } }
            if (nearest == null) return; defenseRemainder += 8f * CityOperationalRules.DefenseMultiplier(cityState.Deployment.Mode) * Time.deltaTime; int damage = Mathf.FloorToInt(defenseRemainder);
            if (damage > 0) { var health = nearest.GetComponent<HealthComponent>(); health.Value.Apply(damage, DamageType.Physical, health.Armor); defenseRemainder -= damage; }
        }
        private void OnThreshold(int threshold)
        {
            waves.Schedule(threshold);
        }
        private void OnBuildingPlaced(BuildingDefinition definition){if(definition.Id.Value=="core.building.machine-gun-turret")waves.Schedule(0);}
        private PlaceholderEnemy Spawn(EnemyArchetype archetype,int threshold,int slot,Vector2? restoredPosition=null,int restoredHealth=-1,int restoredShield=0,BossEncounterSnapshot restoredBoss=null)
        {
            if (square == null) square = Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1f);
            EnemyDefinition definition=DefinitionFor(archetype);bool heavy=definition.IsHeavy;
            float angle=(slot*47f+threshold)*Mathf.Deg2Rad; var item=new GameObject($"Placeholder_{definition.Name}");
            item.transform.position=restoredPosition??((Vector2)city.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*10f); item.transform.localScale=Vector3.one*(heavy?1.4f:.8f);
            var renderer=item.AddComponent<SpriteRenderer>();renderer.sprite=square;renderer.color=heavy?Color.magenta:Color.red;renderer.sortingOrder=9;
            VisualSlot.Attach(item,definition.Id.Value,renderer,renderer.color);
            item.AddComponent<HealthComponent>();var enemy=item.AddComponent<PlaceholderEnemy>();enemy.Configure(cityHealth,city,definition,economy.Inventory,threshold,value => {waves.RegisterDefeat(threshold);EnemyDefeated?.Invoke(value);});if(restoredHealth>=0)enemy.GetComponent<HealthComponent>().Value.Restore(restoredHealth,restoredShield);if(archetype==EnemyArchetype.CrystalBroodmother)item.AddComponent<PlaceholderBossEncounter>().Configure(cityHealth,city,(type,position)=>Spawn(type,-1,SpawnedEnemies,position),restoredBoss);SpawnedEnemies++;return enemy;
        }
        private static EnemyDefinition DefinitionFor(EnemyArchetype archetype)=>archetype==EnemyArchetype.CrystalBeast?EnemyCatalog.CrystalBeast:archetype==EnemyArchetype.Howler?EnemyCatalog.Howler:archetype==EnemyArchetype.Burrower?EnemyCatalog.Burrower:archetype==EnemyArchetype.CrystalBroodmother?EnemyCatalog.CrystalBroodmother:EnemyCatalog.Gnawer;
        public WaveDirectorSnapshot CaptureWave()=>waves.Capture();
        public EnemySnapshot[] CaptureEnemies(){var values=UnityEngine.Object.FindObjectsOfType<PlaceholderEnemy>();var result=new EnemySnapshot[values.Length];for(int i=0;i<values.Length;i++){var enemy=values[i];var health=enemy.GetComponent<HealthComponent>().Value;result[i]=new EnemySnapshot{archetype=(int)enemy.Definition.Archetype,waveTrigger=enemy.WaveTrigger,health=health.Current,shield=health.Shield,x=enemy.transform.position.x,y=enemy.transform.position.y,boss=enemy.GetComponent<PlaceholderBossEncounter>()?.Capture()};}return result;}
        public void Restore(WaveDirectorSnapshot wave,EnemySnapshot[] enemies)
        {
            waves.Restore(wave);foreach(var existing in UnityEngine.Object.FindObjectsOfType<PlaceholderEnemy>())Destroy(existing.gameObject);if(enemies==null)return;
            for(int i=0;i<enemies.Length;i++){var value=enemies[i];EnemyArchetype archetype=Enum.IsDefined(typeof(EnemyArchetype),value.archetype)?(EnemyArchetype)value.archetype:EnemyArchetype.Gnawer;Spawn(archetype,value.waveTrigger,SpawnedEnemies,new Vector2(value.x,value.y),value.health,value.shield,value.boss);}
        }
        private void OnGUI(){if(waves.Current==null)return;string phase=waves.Phase==WavePhase.Warning?$"预警 {waves.WarningRemaining:0}s":waves.Phase==WavePhase.Spawning?"敌群正在分批抵达":"清剿中";GUI.Box(new Rect(Screen.width*.5f-190f,18f,380f,58f),$"压力波次 {waves.Current.Trigger} · {phase}\n已出现 {waves.SpawnedCount}/{waves.Current.TotalCount} · 已消灭 {waves.DefeatedCount}");}
    }
}
