using UnityEngine;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Population;
using System;
using WasteCity.City;
using WasteCity.Research;

namespace WasteCity.Building
{
    public interface ILocalTimeScaleSource { float MultiplierFor(BuildingRuntime runtime); }
    public interface ITurretFireRateSource { float FireRateMultiplier { get; } }
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
        private ILocalTimeScaleSource localTime;
        private PlaceholderMobileCity city;
        public ConstructionProgress Construction { get; private set; }
        public RepairProcess Repair { get; private set; }
        public bool HasLogistics { get; private set; } = true;
        public event Action<BuildingRuntime> Completed;
        public event Action<BuildingRuntime> Removed;
        public void Configure(BuildingDefinition definition, FormalEconomyController economy = null, FormalPopulationController population = null, IProductivitySource productivity = null, ILocalTimeScaleSource localTime = null, PlaceholderMobileCity city = null)
        {
            Definition = definition; this.economy = economy; this.population = population; this.productivity = productivity; this.localTime = localTime; this.city=city; visual = GetComponent<SpriteRenderer>(); Health = GetComponent<HealthComponent>();
            Health.Configure(definition.MaximumHealth, definition.Id.Value == "core.building.wall" ? ArmorType.Heavy : ArmorType.Light);
            Health.Value.Died += () => Destroy(gameObject);
            Construction = new ConstructionProgress(definition.BuildSeconds);
            if (visual != null) visual.color = Color.Lerp(visual.color, Color.gray, .65f);
        }
        private void Update()
        {
            if(city!=null&&!city.LongWorkAllowed)return;
            float multiplier = (productivity?.ConstructionMultiplier ?? 1f) * (localTime?.MultiplierFor(this) ?? 1f);
            if (Construction != null && !Construction.IsComplete) { if (Construction.Tick(Time.deltaTime, multiplier)) FinishConstruction(); return; }
            if (Repair != null && !Repair.IsComplete && Repair.Tick(Time.deltaTime, multiplier)) { Health.Value.Heal(Repair.HealAmount); Repair = null; }
        }
        private void FinishConstruction() { if (effectApplied) return; if(HasLogistics){ApplyEffect(1);effectApplied=true;} if (visual != null) visual.color = Color.Lerp(visual.color, Color.white, .35f); Completed?.Invoke(this); }
        public bool TryStartRepair()
        {
            if (!Construction.IsComplete || !HasLogistics || Repair != null || Health.Value.Current >= Health.Value.Maximum || economy == null || !economy.Inventory.TrySpend(ResourceIds.Biomass, 1)) return false;
            Repair = new RepairProcess(); return true;
        }
        public void RestoreState(int health, float remaining, float repairRemaining = 0f) { Health.Value.Restore(health); Construction.Restore(remaining); if (Construction.IsComplete) FinishConstruction(); if (repairRemaining > 0f) { Repair = new RepairProcess(); Repair.Restore(repairRemaining); } }
        public void PrepareForRestore() { if (effectApplied) { ApplyEffect(-1); effectApplied = false; } Removed?.Invoke(this); suppressRemoval = true; }
        public void PrepareForUpgrade(){if(effectApplied){ApplyEffect(-1);effectApplied=false;}suppressRemoval=true;}
        public void SetLocalTimeSource(ILocalTimeScaleSource value) => localTime = value;
        public void SetLogistics(bool connected){if(HasLogistics==connected)return;HasLogistics=connected;if(!Construction.IsComplete)return;if(connected&&!effectApplied){ApplyEffect(1);effectApplied=true;}else if(!connected&&effectApplied){ApplyEffect(-1);effectApplied=false;}}
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
        private TurretWeaponModel weapon;
        private BuildingRuntime runtime; private ILocalTimeScaleSource localTime;private ITurretFireRateSource fireRate;private ResearchController research;
        public void Configure(FormalEconomyController value, BuildingRuntime building = null, ILocalTimeScaleSource time = null,ITurretFireRateSource rate = null,ResearchController researchController=null) { economy = value; runtime = building; localTime = time;fireRate=rate;research=researchController;weapon=new TurretWeaponModel(building!=null&&building.Definition.Id.Value=="core.building.heavy-machine-gun-turret"?60f:20f,3f); }
        public void SetFireRateSource(ITurretFireRateSource value)=>fireRate=value;
        private void Update()
        {
            if (economy == null || runtime == null || !runtime.HasLogistics) return; HealthComponent nearest = null; float range=10f*(research?.TurretRangeMultiplier??1f);float best = range*range;
            foreach (var enemy in UnityEngine.Object.FindObjectsOfType<PlaceholderEnemy>())
            { var health = enemy.GetComponent<HealthComponent>(); if (health.Value.IsDead) continue; float sqr = ((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude; if (sqr < best) { best = sqr; nearest = health; } }
            if (nearest != null) weapon.Tick(Time.deltaTime * (localTime?.MultiplierFor(runtime) ?? 1f)*(fireRate?.FireRateMultiplier??1f), economy.Inventory, nearest.Value, nearest.Armor,research?.TurretDamageMultiplier??1f);
        }
    }
}
