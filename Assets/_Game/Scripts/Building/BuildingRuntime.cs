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
    public interface ITurretCombatModifierSource
    {
        float FireRateMultiplier { get; }
        float DamageMultiplier(DamageType damageType);
    }
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
        private ResearchController research;
        private readonly BuildingRegenerationModel regeneration = new BuildingRegenerationModel();
        public ConstructionProgress Construction { get; private set; }
        public RepairProcess Repair { get; private set; }
        public bool HasLogistics { get; private set; } = true;
        public event Action<BuildingRuntime> Completed;
        public event Action<BuildingRuntime> Removed;
        public void Configure(BuildingDefinition definition, FormalEconomyController economy = null, FormalPopulationController population = null, IProductivitySource productivity = null, ILocalTimeScaleSource localTime = null, PlaceholderMobileCity city = null, ResearchController research = null)
        {
            Definition = definition; this.economy = economy; this.population = population; this.productivity = productivity; this.localTime = localTime; this.city=city; this.research=research; visual = GetComponent<SpriteRenderer>(); Health = GetComponent<HealthComponent>();
            Health.Configure(RouteTechnologyEffects.BuildingMaximumHealth(definition.MaximumHealth,research!=null&&research.HasAlloyArmor), definition.Id.Value == "core.building.wall" ? ArmorType.Heavy : ArmorType.Light);
            Health.Value.Died += () => Destroy(gameObject);
            Construction = new ConstructionProgress(definition.BuildSeconds);
            if (visual != null) visual.color = Color.Lerp(visual.color, Color.gray, .65f);
            SyncResearchEffects();
        }
        private void Update()
        {
            SyncResearchEffects();
            if(Construction != null && Construction.IsComplete && research != null)regeneration.Tick(Time.deltaTime,Definition.Id.Value=="core.building.wall",research.HasTissueRegeneration,research.HasCarapaceGrowth,Health.Value,economy?.Inventory);
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
        public void RestoreState(int health, float remaining, float repairRemaining = 0f, int shield = 0) { Health.Value.Restore(health,shield); Construction.Restore(remaining); if (Construction.IsComplete) FinishConstruction(); if (repairRemaining > 0f) { Repair = new RepairProcess(); Repair.Restore(repairRemaining); } }
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
        private void SyncResearchEffects()
        {
            if(research==null||Health?.Value==null||Definition==null)return;
            int maximum=RouteTechnologyEffects.BuildingMaximumHealth(Definition.MaximumHealth,research.HasAlloyArmor);
            if(Health.Value.Maximum!=maximum)Health.Value.SetMaximum(maximum,true);
            Health.Value.SetPhysicalDamagePercent(RouteTechnologyEffects.PhysicalDamagePercent(Definition.Id.Value,research.HasTalismanBasics));
        }
    }

    public sealed class PlaceholderTurret : MonoBehaviour
    {
        private FormalEconomyController economy;
        private TurretWeaponModel weapon;
        private readonly InfectionEmitterModel infectionEmitter = new InfectionEmitterModel();
        private readonly SwordIntentEmitterModel swordIntentEmitter = new SwordIntentEmitterModel();
        private BuildingRuntime runtime; private ILocalTimeScaleSource localTime;private ITurretCombatModifierSource combatModifier;private ResearchController research;private DefenseTowerDefinition profile;private float mindControlClock;
        public void Configure(FormalEconomyController value, BuildingRuntime building = null, ILocalTimeScaleSource time = null,ITurretCombatModifierSource modifier = null,ResearchController researchController=null) { economy = value; runtime = building; localTime = time;combatModifier=modifier;research=researchController;profile=DefenseTowerCatalog.For(building?.Definition?.Id.Value)??DefenseTowerCatalog.For("core.building.machine-gun-turret");weapon=new TurretWeaponModel(profile.DamagePerSecond,profile.SecondsPerConsumable,profile.DamageType,profile.ConsumableId); }
        public void SetCombatModifierSource(ITurretCombatModifierSource value)=>combatModifier=value;
        private void Update()
        {
            if (economy == null || runtime == null || !runtime.HasLogistics) return;infectionEmitter.Tick(Time.deltaTime,false);swordIntentEmitter.Tick(Time.deltaTime,false); PlaceholderEnemy nearest = null; bool physical=profile.DamageType==DamageType.Physical;float range=profile.Range*(physical?(research?.TurretRangeMultiplier??1f):1f)*RouteTechnologyEffects.TowerRangeMultiplier(runtime.Definition.Id.Value,research!=null&&research.HasSwordRiding);float best = range*range;
            foreach (var enemy in UnityEngine.Object.FindObjectsOfType<PlaceholderEnemy>())
            { var health = enemy.GetComponent<HealthComponent>(); if (enemy.IsControlled||health.Value.IsDead) continue; float sqr = ((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude; if (sqr < best) { best = sqr; nearest = enemy; } }
            if (nearest != null)
            {
                float delta=Time.deltaTime*(localTime?.MultiplierFor(runtime)??1f)*(combatModifier?.FireRateMultiplier??1f);var targetHealth=nearest.GetComponent<HealthComponent>();float researchDamage=physical?(research?.TurretDamageMultiplier??1f):1f;float routeDamage=combatModifier?.DamageMultiplier(profile.DamageType)??1f;int dealt=weapon.Tick(delta,economy.Inventory,targetHealth.Value,targetHealth.Armor,researchDamage*routeDamage);
                if(infectionEmitter.Tick(0f,dealt>0&&profile.DamageType==DamageType.Biological))nearest.ApplyInfection(1);
                if(swordIntentEmitter.Tick(0f,dealt>0&&profile.ConsumableId==ResourceIds.FlyingSword))nearest.ApplySwordIntent();
                if(dealt>0&&profile.DamageType==DamageType.Psionic)nearest.ApplyPsionicResonance(dealt);
                if(dealt>0&&profile.DamageType==DamageType.Psionic&&research!=null&&research.HasMindControl){mindControlClock+=Time.deltaTime;if(mindControlClock>=1f){mindControlClock%=1f;if(MindControlModel.ShouldConvert(true,nearest.Quality,nearest.Definition.IsHeavy,UnityEngine.Random.Range(0,100)))nearest.TryConvert();}}
            }
        }
    }

    public sealed class PlaceholderShieldGenerator : MonoBehaviour
    {
        private readonly ShieldPulseModel pulse = new ShieldPulseModel(8f);
        private BuildingRuntime runtime;
        public void Configure(BuildingRuntime building) => runtime = building;
        private void Update()
        {
            if(runtime==null||!runtime.HasLogistics||!pulse.Tick(Time.deltaTime))return;
            foreach(var building in UnityEngine.Object.FindObjectsOfType<BuildingRuntime>())
                if(building.Construction.IsComplete&&((Vector2)(building.transform.position-transform.position)).sqrMagnitude<=36f)building.Health.Value.GrantShield(25,100);
        }
    }

    public sealed class PlaceholderAutomatedRepairBay : MonoBehaviour
    {
        private readonly AutomatedRepairModel repair = new AutomatedRepairModel(6f,20);
        private BuildingRuntime runtime;
        private static Sprite square;
        private GameObject repairMarker;
        private float repairVisualAngle;
        public void Configure(BuildingRuntime building){runtime=building;EnsureRepairMarker();}
        private void Update()
        {
            repairVisualAngle+=Time.deltaTime*90f;if(repairMarker!=null)repairMarker.transform.localPosition=new Vector3(Mathf.Cos(repairVisualAngle*Mathf.Deg2Rad)*.8f,Mathf.Sin(repairVisualAngle*Mathf.Deg2Rad)*.8f,-.15f);
            if(runtime==null||!runtime.HasLogistics||!repair.Tick(Time.deltaTime))return;
            foreach(var building in UnityEngine.Object.FindObjectsOfType<BuildingRuntime>())
                if(building.Construction.IsComplete&&((Vector2)(building.transform.position-transform.position)).sqrMagnitude<=36f)repair.Repair(building.Health.Value);
        }
        private void EnsureRepairMarker()
        {
            if(repairMarker!=null)return;if(square==null)square=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1f);
            repairMarker=new GameObject("TechnologyRepairMech");repairMarker.transform.SetParent(transform,false);repairMarker.transform.localScale=new Vector3(.38f,.38f,1f);
            var renderer=repairMarker.AddComponent<SpriteRenderer>();renderer.sprite=square;renderer.color=new Color(.25f,1f,.65f);renderer.sortingOrder=11;
            WasteCity.Presentation.VisualSlot.Attach(repairMarker,"technology.unit.repair-mech",renderer,renderer.color);
        }
    }
}
