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
        private bool heavy;
        private Action<bool> defeated;
        public void Configure(HealthComponent targetHealth, Transform target, bool isHeavy, ResourceInventory inventory, Action<bool> onDefeated = null)
        {
            heavy = isHeavy; defeated = onDefeated; lootInventory = inventory; health = GetComponent<HealthComponent>(); health.Configure(heavy ? 240 : 70, heavy ? ArmorType.Heavy : ArmorType.Light);
            cityHealth = targetHealth; city = target; health.Value.Died += OnDied;
        }
        private void OnDied(){lootInventory?.Add(ResourceIds.Biomass,heavy?3:1);defeated?.Invoke(heavy);Destroy(gameObject);}
        private void Update()
        {
            if (city == null || cityHealth.Value.IsDead) return;
            HealthComponent targetHealth = cityHealth; Transform targetTransform = city; float best = Vector2.Distance(transform.position, city.position);
            foreach(var building in UnityEngine.Object.FindObjectsOfType<BuildingRuntime>()){float distance=Vector2.Distance(transform.position,building.transform.position);if(distance<best){best=distance;targetHealth=building.Health;targetTransform=building.transform;}}
            if (best > 2f) transform.position = Vector2.MoveTowards(transform.position, targetTransform.position, 1.5f * Time.deltaTime);
            else { attackRemainder += 8f * Time.deltaTime; int damage = Mathf.FloorToInt(attackRemainder); if (damage > 0) { targetHealth.Value.Apply(damage, DamageType.Physical, targetHealth.Armor); attackRemainder -= damage; } }
        }
    }
}
