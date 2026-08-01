using UnityEngine;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Population;

namespace WasteCity.Building
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class BuildingRuntime : MonoBehaviour
    {
        public BuildingDefinition Definition { get; private set; }
        public HealthComponent Health { get; private set; }
        private FormalEconomyController economy;
        private FormalPopulationController population;
        private bool effectApplied;
        public void Configure(BuildingDefinition definition, FormalEconomyController economy = null, FormalPopulationController population = null)
        {
            Definition = definition; this.economy = economy; this.population = population; Health = GetComponent<HealthComponent>();
            int hp = definition.Id.Value == "core.building.wall" ? 500 : definition.Id.Value == "core.building.machine-gun-turret" ? 250 : 300;
            Health.Configure(hp, definition.Id.Value == "core.building.wall" ? ArmorType.Heavy : ArmorType.Light);
            Health.Value.Died += () => Destroy(gameObject);
            ApplyEffect(1); effectApplied = true;
        }
        private void OnDestroy() { if (effectApplied) ApplyEffect(-1); }
        private void ApplyEffect(int direction)
        {
            if (Definition.Id.Value == "core.building.housing") population?.AddCapacity(50 * direction);
            if (Definition.Id.Value == "core.building.warehouse") economy?.Inventory.AddCapacity(150 * direction);
        }
    }

    public sealed class PlaceholderTurret : MonoBehaviour
    {
        private FormalEconomyController economy;
        private readonly TurretWeaponModel weapon = new TurretWeaponModel(20f, 3f);
        public void Configure(FormalEconomyController value) => economy = value;
        private void Update()
        {
            if (economy == null) return; HealthComponent nearest = null; float best = 100f;
            foreach (var enemy in Object.FindObjectsOfType<PlaceholderEnemy>())
            { var health = enemy.GetComponent<HealthComponent>(); if (health.Value.IsDead) continue; float sqr = ((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude; if (sqr < best) { best = sqr; nearest = health; } }
            if (nearest != null) weapon.Tick(Time.deltaTime, economy.Inventory, nearest.Value, nearest.Armor);
        }
    }
}
