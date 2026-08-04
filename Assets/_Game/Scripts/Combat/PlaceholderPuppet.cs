using UnityEngine;
using WasteCity.Economy;
using WasteCity.Presentation;
using WasteCity.Research;

namespace WasteCity.Combat
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class PlaceholderPuppet : MonoBehaviour
    {
        public const int MaximumHealth = 180;
        private const float MoveSpeed = 2.2f;
        private const float AttackRange = 1.5f;
        private const float DamagePerSecond = 18f;
        private Transform city;
        private ResearchController research;
        private HealthComponent health;
        private ResourceInventory maintenanceInventory;
        private FriendlyUnitAgent agent;
        private SpriteRenderer visual;
        private readonly PuppetMaintenanceModel maintenance = new PuppetMaintenanceModel();
        private bool visualStateInitialized;
        private bool lastActive;
        public HealthComponent Health => health;
        public PuppetMaintenanceModel Maintenance => maintenance;

        public void Configure(
            Transform cityTransform,
            int restoredHealth = -1,
            ResearchController researchController = null,
            FriendlyUnitCommandModel commandModel = null,
            ResourceInventory inventory = null,
            float maintenanceElapsed = 0f,
            bool maintenanceActive = true)
        {
            city = cityTransform;
            research = researchController;
            maintenanceInventory = inventory;
            health = GetComponent<HealthComponent>();
            visual = GetComponent<SpriteRenderer>();
            health.Configure(MaximumHealth, ArmorType.Light);
            if (restoredHealth >= 0) health.Value.Restore(restoredHealth);
            health.Value.Died += () => Destroy(gameObject);
            agent = GetComponent<FriendlyUnitAgent>() ?? gameObject.AddComponent<FriendlyUnitAgent>();
            agent.Configure(health, city, commandModel, FriendlyUnitKind.Puppet, research, MoveSpeed, AttackRange, DamagePerSecond, DamageType.Physical, 1.25f);
            maintenance.Restore(maintenanceElapsed, maintenanceActive);
            SyncMaintenanceState();
        }

        private void Update()
        {
            maintenance.Tick(Time.deltaTime, maintenanceInventory);
            SyncMaintenanceState();
        }

        private void SyncMaintenanceState()
        {
            bool active = maintenance.Active;
            if (agent != null) agent.enabled = active;
            if (visual == null || visualStateInitialized && lastActive == active) return;
            lastActive = active;
            visualStateInitialized = true;
            Color color = active ? new Color(.3f, .85f, 1f) : new Color(.25f, .3f, .38f);
            VisualSlot.Attach(
                gameObject,
                active ? "cultivation.unit.puppet" : "cultivation.unit.puppet.dormant",
                visual,
                color);
        }
    }
}
