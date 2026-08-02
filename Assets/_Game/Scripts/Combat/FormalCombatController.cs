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
        private void Spawn(EnemyArchetype archetype,int threshold,int slot)
        {
            if (square == null) square = Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1f);
            EnemyDefinition definition=DefinitionFor(archetype);bool heavy=definition.IsHeavy;
            float angle=(slot*47f+threshold)*Mathf.Deg2Rad; var item=new GameObject($"Placeholder_{definition.Name}");
            item.transform.position=(Vector2)city.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*10f; item.transform.localScale=Vector3.one*(heavy?1.4f:.8f);
            var renderer=item.AddComponent<SpriteRenderer>();renderer.sprite=square;renderer.color=heavy?Color.magenta:Color.red;renderer.sortingOrder=9;
            VisualSlot.Attach(item,definition.Id.Value,renderer,renderer.color);
            item.AddComponent<HealthComponent>();item.AddComponent<PlaceholderEnemy>().Configure(cityHealth,city,definition,economy.Inventory, value => {waves.RegisterDefeat(threshold);EnemyDefeated?.Invoke(value);});SpawnedEnemies++;
        }
        private static EnemyDefinition DefinitionFor(EnemyArchetype archetype)=>archetype==EnemyArchetype.CrystalBeast?EnemyCatalog.CrystalBeast:archetype==EnemyArchetype.Howler?EnemyCatalog.Howler:archetype==EnemyArchetype.Burrower?EnemyCatalog.Burrower:archetype==EnemyArchetype.CrystalBroodmother?EnemyCatalog.CrystalBroodmother:EnemyCatalog.Gnawer;
        private void OnGUI(){if(waves.Current==null)return;string phase=waves.Phase==WavePhase.Warning?$"预警 {waves.WarningRemaining:0}s":waves.Phase==WavePhase.Spawning?"敌群正在分批抵达":"清剿中";GUI.Box(new Rect(Screen.width*.5f-190f,18f,380f,58f),$"压力波次 {waves.Current.Trigger} · {phase}\n已出现 {waves.SpawnedCount}/{waves.Current.TotalCount} · 已消灭 {waves.DefeatedCount}");}
    }
}
