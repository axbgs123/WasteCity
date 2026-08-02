using UnityEngine;
using WasteCity.Building;
using WasteCity.Economy;
using System;

namespace WasteCity.Combat
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class PlaceholderEnemy : MonoBehaviour
    {
        private HealthComponent health;
        private HealthComponent cityHealth;
        private Transform city;
        private float attackRemainder;
        private ResourceInventory lootInventory;
        private EnemyDefinition definition;
        private EnemyQualityProfile quality;
        public int WaveTrigger { get; private set; }
        private Action<bool> defeated;
        public EnemyDefinition Definition => definition;
        public EnemyQuality Quality => quality.Quality;
        public float MoveSpeedMultiplier { get; set; }=1f;
        public void Configure(HealthComponent targetHealth, Transform target, EnemyDefinition enemyDefinition, ResourceInventory inventory, int waveTrigger = 0, Action<bool> onDefeated = null, EnemyQuality enemyQuality = EnemyQuality.Ordinary)
        {
            definition = enemyDefinition ?? EnemyCatalog.Gnawer; quality=EnemyQualityCatalog.For(enemyQuality);WaveTrigger=waveTrigger; defeated = onDefeated; lootInventory = inventory; health = GetComponent<HealthComponent>(); health.Configure(Mathf.RoundToInt(definition.MaximumHealth*quality.HealthMultiplier), definition.Armor);
            cityHealth = targetHealth; city = target; health.Value.Died += OnDied;
        }
        private void OnDied(){lootInventory?.Add(ResourceIds.Biomass,Mathf.RoundToInt(definition.BiomassDrop*quality.LootMultiplier));defeated?.Invoke(definition.IsHeavy);Destroy(gameObject);}
        private void Update()
        {
            if (city == null || cityHealth.Value.IsDead) return;
            HealthComponent targetHealth = cityHealth; Transform targetTransform = city; float best = definition.TargetPriority==EnemyTargetPriority.Nearest?Vector2.Distance(transform.position, city.position):float.MaxValue;
            foreach(var building in UnityEngine.Object.FindObjectsOfType<BuildingRuntime>())
            {
                if(!CanTarget(building))continue;float distance=Vector2.Distance(transform.position,building.transform.position);
                if(distance<best){best=distance;targetHealth=building.Health;targetTransform=building.transform;}
            }
            if(best==float.MaxValue)best=Vector2.Distance(transform.position,city.position);
            if (best > definition.AttackRange) transform.position = Vector2.MoveTowards(transform.position, targetTransform.position, definition.MoveSpeed * MoveSpeedMultiplier * Time.deltaTime);
            else { attackRemainder += definition.DamagePerSecond * quality.DamageMultiplier * Time.deltaTime; int damage = Mathf.FloorToInt(attackRemainder); if (damage > 0) { targetHealth.Value.Apply(damage, DamageType.Physical, targetHealth.Armor); attackRemainder -= damage; } }
        }
        private bool CanTarget(BuildingRuntime building)
        {
            string id=building.Definition.Id.Value;
            if(definition.TargetPriority==EnemyTargetPriority.Walls)return id=="core.building.wall";
            if(definition.TargetPriority==EnemyTargetPriority.Production)return id=="core.building.mining-station"||id=="core.building.smelter"||id=="core.building.assembler"||id=="core.building.research-station";
            return true;
        }
    }
}
