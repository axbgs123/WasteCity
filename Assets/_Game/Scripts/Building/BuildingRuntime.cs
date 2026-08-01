using UnityEngine;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Population;
using System;

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
        private IProductivitySource productivity;
        private SpriteRenderer visual;
        private bool suppressRemoval;
        public ConstructionProgress Construction { get; private set; }
        public event Action<BuildingRuntime> Completed;
        public event Action<BuildingRuntime> Removed;
        public void Configure(BuildingDefinition definition, FormalEconomyController economy = null, FormalPopulationController population = null, IProductivitySource productivity = null)
        {
            Definition = definition; this.economy = economy; this.population = population; this.productivity = productivity; visual = GetComponent<SpriteRenderer>(); Health = GetComponent<HealthComponent>();
            Health.Configure(definition.MaximumHealth, definition.Id.Value == "core.building.wall" ? ArmorType.Heavy : ArmorType.Light);
            Health.Value.Died += () => Destroy(gameObject);
            Construction = new ConstructionProgress(definition.BuildSeconds);
            if (visual != null) visual.color = Color.Lerp(visual.color, Color.gray, .65f);
        }
        private void Update()
        {
            if (Construction == null || Construction.IsComplete) return;
            if (!Construction.Tick(Time.deltaTime, productivity?.ConstructionMultiplier ?? 1f)) return;
            FinishConstruction();
        }
        private void FinishConstruction() { if (effectApplied) return; ApplyEffect(1); effectApplied = true; if (visual != null) visual.color = Color.Lerp(visual.color, Color.white, .35f); Completed?.Invoke(this); }
        public void RestoreState(int health, float remaining) { Health.Value.Restore(health); Construction.Restore(remaining); if (Construction.IsComplete) FinishConstruction(); }
        public void PrepareForRestore() { if (effectApplied) { ApplyEffect(-1); effectApplied = false; } Removed?.Invoke(this); suppressRemoval = true; }
        private void OnDestroy() { if (effectApplied) ApplyEffect(-1); if (!suppressRemoval) Removed?.Invoke(this); }
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
            foreach (var enemy in UnityEngine.Object.FindObjectsOfType<PlaceholderEnemy>())
            { var health = enemy.GetComponent<HealthComponent>(); if (health.Value.IsDead) continue; float sqr = ((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude; if (sqr < best) { best = sqr; nearest = health; } }
            if (nearest != null) weapon.Tick(Time.deltaTime, economy.Inventory, nearest.Value, nearest.Armor);
        }
    }
}
