using UnityEngine;
using WasteCity.Building;
using WasteCity.Economy;
using System;
using WasteCity.Research;
using WasteCity.Presentation;

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
        private ResearchController research;
        private SpriteRenderer visual;
        private bool defeatReported;
        public int WaveTrigger { get; private set; }
        private Action<bool> defeated;
        private Action converted;
        private FriendlyUnitCommandModel friendlyCommands;
        public EnemyDefinition Definition => definition;
        public EnemyQuality Quality => quality.Quality;
        public bool IsControlled { get; private set; }
        public HealthComponent Health => health;
        public float MoveSpeedMultiplier { get; set; }=1f;
        public void Configure(HealthComponent targetHealth, Transform target, EnemyDefinition enemyDefinition, ResourceInventory inventory, int waveTrigger = 0, Action<bool> onDefeated = null, EnemyQuality enemyQuality = EnemyQuality.Ordinary, ResearchController researchController = null, Action onConverted = null, FriendlyUnitCommandModel commandModel = null)
        {
            definition = enemyDefinition ?? EnemyCatalog.Gnawer; quality=EnemyQualityCatalog.For(enemyQuality);WaveTrigger=waveTrigger; defeated = onDefeated; converted=onConverted; lootInventory = inventory; research=researchController; health = GetComponent<HealthComponent>(); visual=GetComponent<SpriteRenderer>(); health.Configure(Mathf.RoundToInt(definition.MaximumHealth*quality.HealthMultiplier), definition.Armor);
            cityHealth = targetHealth; city = target; friendlyCommands=commandModel; health.Value.Died += OnDied;
        }
        private void OnDied(){if(!IsControlled)lootInventory?.Add(ResourceIds.Biomass,RouteTechnologyEffects.BiomassDrop(definition.BiomassDrop,quality.LootMultiplier,research!=null&&research.HasMetabolicAcceleration));if(!defeatReported)defeated?.Invoke(definition.IsHeavy);Destroy(gameObject);}
        public bool TryConvert(bool reportDefeat=true)
        {
            if(IsControlled||!MindControlModel.ShouldConvert(research!=null&&research.HasMindControl,Quality,definition.IsHeavy,0))return false;
            IsControlled=true;MoveSpeedMultiplier=1.15f;
            if(!defeatReported){defeatReported=true;if(reportDefeat)converted?.Invoke();}
            if(visual!=null){Color color=new Color(.2f,1f,.75f);VisualSlot.Attach(gameObject,$"{definition.Id.Value}.controlled",visual,color);}
            FriendlyUnitCommandModel commands=friendlyCommands??UnityEngine.Object.FindObjectOfType<FormalFriendlyUnitController>()?.Commands??new FriendlyUnitCommandModel();
            var agent=GetComponent<FriendlyUnitAgent>()??gameObject.AddComponent<FriendlyUnitAgent>();
            agent.Configure(health,city,commands,FriendlyUnitKind.Controlled,research,definition.MoveSpeed*MoveSpeedMultiplier,definition.AttackRange,definition.DamagePerSecond*.75f,DamageType.Psionic,1.25f);
            return true;
        }
        private void Update()
        {
            if (city == null || cityHealth.Value.IsDead) return;
            if(IsControlled)return;
            HealthComponent targetHealth = cityHealth; Transform targetTransform = city; float best = definition.TargetPriority==EnemyTargetPriority.Nearest?Vector2.Distance(transform.position, city.position):float.MaxValue;
            foreach(var ally in UnityEngine.Object.FindObjectsOfType<FriendlyUnitAgent>())
            {
                float distance=Vector2.Distance(transform.position,ally.transform.position);
                if(distance<best){best=distance;targetHealth=ally.Health;targetTransform=ally.transform;}
            }
            foreach(var building in UnityEngine.Object.FindObjectsOfType<BuildingRuntime>())
            {
                if(!CanTarget(building))continue;float distance=Vector2.Distance(transform.position,building.transform.position);
                if(distance<best){best=distance;targetHealth=building.Health;targetTransform=building.transform;}
            }
            if(best==float.MaxValue)best=Vector2.Distance(transform.position,city.position);
            if (best > definition.AttackRange) transform.position = Vector2.MoveTowards(transform.position, targetTransform.position, definition.MoveSpeed * MoveSpeedMultiplier * Time.deltaTime);
            else { attackRemainder += definition.DamagePerSecond * quality.DamageMultiplier * Time.deltaTime; int damage = Mathf.FloorToInt(attackRemainder); if (DamageMatrix.Apply(damage,DamageType.Physical,targetHealth.Armor)>0) { targetHealth.Value.Apply(damage, DamageType.Physical, targetHealth.Armor); attackRemainder -= damage; } }
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
