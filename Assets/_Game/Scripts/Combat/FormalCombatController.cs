using UnityEngine;
using WasteCity.Progression;
using WasteCity.Economy;
using System;
using WasteCity.Presentation;
using WasteCity.City;
using WasteCity.Building;
using System.Collections.Generic;
using WasteCity.Research;

namespace WasteCity.Combat
{
    [Serializable] public sealed class EnemySnapshot { public int archetype,quality,waveTrigger,health,shield,infectionStacks,swordIntentStacks;public float x,y,infectionElapsed,psionicResonanceRemaining;public bool controlled;public BossEncounterSnapshot boss; }
    public sealed class FormalCombatController : MonoBehaviour
    {
        [SerializeField] private HealthComponent cityHealth;
        [SerializeField] private Transform city;
        [SerializeField] private FormalProgressionController progression;
        [SerializeField] private FormalEconomyController economy;
        [SerializeField] private PlaceholderMobileCity cityState;
        [SerializeField] private PlaceholderBuildingController buildings;
        [SerializeField] private ResearchController research;
        private float defenseRemainder;
        private readonly WaveDirectorModel waves=new WaveDirectorModel();
        private readonly List<EnemyArchetype> spawnBuffer=new List<EnemyArchetype>();
        public int SpawnedEnemies { get; private set; }
        public event Action<bool> EnemyDefeated;
        public event Action<EnemyArchetype> EnemyArchetypeDefeated;
        public event Action<EnemyArchetype> EnemyConverted;
        public event Action<int> WaveCompleted;
        private static Sprite square;
        private void Start(){progression.Observation.ThresholdReached+=OnThreshold;progression.Advanced+=OnCivilizationAdvanced;buildings.BuildingPlaced+=OnBuildingPlaced;}
        private void Update()
        {
            spawnBuffer.Clear();waves.Tick(Time.deltaTime,spawnBuffer);for(int i=0;i<spawnBuffer.Count;i++)Spawn(spawnBuffer[i],waves.Current?.Trigger??0,SpawnedEnemies);
            PlaceholderEnemy nearest = null; float best = 25f;
            foreach (var enemy in UnityEngine.Object.FindObjectsOfType<PlaceholderEnemy>()) { if(enemy.IsControlled)continue;float sqr = ((Vector2)(enemy.transform.position - city.position)).sqrMagnitude; if (sqr < best) { best = sqr; nearest = enemy; } }
            if (nearest == null) return; defenseRemainder += 8f * CityOperationalRules.DefenseMultiplier(cityState.Deployment.Mode) * Time.deltaTime; int damage = Mathf.FloorToInt(defenseRemainder);
            if (damage > 0) { var health = nearest.GetComponent<HealthComponent>(); health.Value.Apply(damage, DamageType.Physical, health.Armor); defenseRemainder -= damage; }
        }
        private void OnThreshold(int threshold)
        {
            RefreshWarningMultiplier();
            waves.Schedule(threshold);
        }
        private void OnBuildingPlaced(BuildingDefinition definition){if(DefenseTowerCatalog.For(definition.Id.Value)!=null){RefreshWarningMultiplier();waves.Schedule(0);}}
        private void OnCivilizationAdvanced(){RefreshWarningMultiplier();waves.Schedule(120);}
        private void RefreshWarningMultiplier()=>waves.SetWarningMultiplier(RouteTechnologyEffects.WarningMultiplier(research!=null&&research.HasPrecognitiveSense));
        private PlaceholderEnemy Spawn(EnemyArchetype archetype,int threshold,int slot,Vector2? restoredPosition=null,int restoredHealth=-1,int restoredShield=0,BossEncounterSnapshot restoredBoss=null,int restoredQuality=-1,bool restoredControlled=false,int restoredInfectionStacks=0,float restoredInfectionElapsed=0f,int restoredSchema=28,int restoredSwordIntentStacks=0,float restoredPsionicResonanceRemaining=0f)
        {
            if (square == null) square = Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1f);
            EnemyDefinition definition=DefinitionFor(archetype);bool heavy=definition.IsHeavy;
            EnemyQuality quality=restoredQuality>=0&&Enum.IsDefined(typeof(EnemyQuality),restoredQuality)?(EnemyQuality)restoredQuality:archetype==EnemyArchetype.CrystalBroodmother?EnemyQuality.Ordinary:EnemyQualityRoller.ForSpawn(slot,threshold,progression.Civilization.Level);
            var profile=EnemyQualityCatalog.For(quality);float angle=(slot*47f+threshold)*Mathf.Deg2Rad; var item=new GameObject($"Placeholder_{profile.DisplayName}_{definition.Name}");
            item.transform.position=restoredPosition??((Vector2)city.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*10f); item.transform.localScale=Vector3.one*(heavy?1.4f+.1f*(int)quality:.8f+.08f*(int)quality);
            var renderer=item.AddComponent<SpriteRenderer>();renderer.sprite=square;renderer.color=QualityColor(quality,heavy);renderer.sortingOrder=9;
            VisualSlot.Attach(item,$"{definition.Id.Value}.quality.{quality.ToString().ToLowerInvariant()}",renderer,renderer.color);
            item.AddComponent<HealthComponent>();var enemy=item.AddComponent<PlaceholderEnemy>();enemy.Configure(cityHealth,city,definition,economy.Inventory,threshold,value => {if(waves.RegisterDefeat(threshold))WaveCompleted?.Invoke(threshold);EnemyDefeated?.Invoke(value);EnemyArchetypeDefeated?.Invoke(archetype);},quality,research,()=>{if(waves.RegisterDefeat(threshold))WaveCompleted?.Invoke(threshold);EnemyConverted?.Invoke(archetype);});if(restoredHealth>=0)enemy.GetComponent<HealthComponent>().Value.Restore(restoredHealth,restoredShield);if(restoredControlled)enemy.TryConvert(false);if(restoredSchema>=25&&!enemy.IsControlled)enemy.RestoreInfection(restoredInfectionStacks,restoredInfectionElapsed);if(restoredSchema>=27&&!enemy.IsControlled)enemy.RestoreSwordIntent(restoredSwordIntentStacks);if(restoredSchema>=28&&!enemy.IsControlled)enemy.RestorePsionicResonance(restoredPsionicResonanceRemaining);if(archetype==EnemyArchetype.CrystalBroodmother)item.AddComponent<PlaceholderBossEncounter>().Configure(cityHealth,city,(type,position)=>Spawn(type,-1,SpawnedEnemies,position),restoredBoss);SpawnedEnemies++;return enemy;
        }
        private static Color QualityColor(EnemyQuality quality,bool heavy)=>quality==EnemyQuality.Legendary?Color.yellow:quality==EnemyQuality.Epic?new Color(.7f,.2f,1f):quality==EnemyQuality.Rare?Color.cyan:quality==EnemyQuality.Excellent?new Color(1f,.45f,.1f):heavy?Color.magenta:Color.red;
        private static EnemyDefinition DefinitionFor(EnemyArchetype archetype)=>archetype==EnemyArchetype.CrystalBeast?EnemyCatalog.CrystalBeast:archetype==EnemyArchetype.Howler?EnemyCatalog.Howler:archetype==EnemyArchetype.Burrower?EnemyCatalog.Burrower:archetype==EnemyArchetype.CrystalBroodmother?EnemyCatalog.CrystalBroodmother:EnemyCatalog.Gnawer;
        public WaveDirectorSnapshot CaptureWave()=>waves.Capture();
        public EnemySnapshot[] CaptureEnemies(){var values=UnityEngine.Object.FindObjectsOfType<PlaceholderEnemy>();var result=new EnemySnapshot[values.Length];for(int i=0;i<values.Length;i++){var enemy=values[i];var health=enemy.GetComponent<HealthComponent>().Value;result[i]=new EnemySnapshot{archetype=(int)enemy.Definition.Archetype,quality=(int)enemy.Quality,waveTrigger=enemy.WaveTrigger,health=health.Current,shield=health.Shield,x=enemy.transform.position.x,y=enemy.transform.position.y,controlled=enemy.IsControlled,infectionStacks=enemy.Infection?.Stacks??0,infectionElapsed=enemy.Infection?.Elapsed??0,swordIntentStacks=enemy.SwordIntent?.Stacks??0,psionicResonanceRemaining=enemy.PsionicResonance?.Remaining??0f,boss=enemy.GetComponent<PlaceholderBossEncounter>()?.Capture()};}return result;}
        public void Restore(WaveDirectorSnapshot wave,EnemySnapshot[] enemies,int schema)
        {
            RefreshWarningMultiplier();waves.Restore(wave);foreach(var existing in UnityEngine.Object.FindObjectsOfType<PlaceholderEnemy>())Destroy(existing.gameObject);if(enemies==null)return;
            for(int i=0;i<enemies.Length;i++){var value=enemies[i];EnemyArchetype archetype=Enum.IsDefined(typeof(EnemyArchetype),value.archetype)?(EnemyArchetype)value.archetype:EnemyArchetype.Gnawer;Spawn(archetype,value.waveTrigger,SpawnedEnemies,new Vector2(value.x,value.y),value.health,value.shield,value.boss,value.quality,value.controlled,value.infectionStacks,value.infectionElapsed,schema,value.swordIntentStacks,value.psionicResonanceRemaining);}
        }
        private void OnGUI(){if(waves.Current==null)return;string phase=waves.Phase==WavePhase.Warning?$"预警 {waves.WarningRemaining:0}s":waves.Phase==WavePhase.Spawning?"敌群正在分批抵达":"清剿中";GUI.Box(new Rect(Screen.width*.5f-190f,18f,380f,58f),$"压力波次 {waves.Current.Trigger} · {phase}\n已出现 {waves.SpawnedCount}/{waves.Current.TotalCount} · 已消灭 {waves.DefeatedCount}");}
    }
}
