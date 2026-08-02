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
        private Action<bool> defeated;
        public EnemyDefinition Definition => definition;
        public void Configure(HealthComponent targetHealth, Transform target, EnemyDefinition enemyDefinition, ResourceInventory inventory, Action<bool> onDefeated = null)
        {
            definition = enemyDefinition ?? EnemyCatalog.Gnawer; defeated = onDefeated; lootInventory = inventory; health = GetComponent<HealthComponent>(); health.Configure(definition.MaximumHealth, definition.Armor);
            cityHealth = targetHealth; city = target; health.Value.Died += OnDied;
        }
        private void OnDied(){lootInventory?.Add(ResourceIds.Biomass,definition.BiomassDrop);defeated?.Invoke(definition.IsHeavy);Destroy(gameObject);}
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
            if (best > definition.AttackRange) transform.position = Vector2.MoveTowards(transform.position, targetTransform.position, definition.MoveSpeed * Time.deltaTime);
            else { attackRemainder += definition.DamagePerSecond * Time.deltaTime; int damage = Mathf.FloorToInt(attackRemainder); if (damage > 0) { targetHealth.Value.Apply(damage, DamageType.Physical, targetHealth.Armor); attackRemainder -= damage; } }
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
