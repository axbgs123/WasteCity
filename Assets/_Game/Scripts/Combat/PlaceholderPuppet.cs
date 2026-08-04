using UnityEngine;
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
        public HealthComponent Health => health;

        public void Configure(Transform cityTransform, int restoredHealth = -1, ResearchController researchController = null, FriendlyUnitCommandModel commandModel = null)
        {
            city = cityTransform;
            research = researchController;
            health = GetComponent<HealthComponent>();
            health.Configure(MaximumHealth, ArmorType.Light);
            if (restoredHealth >= 0) health.Value.Restore(restoredHealth);
            health.Value.Died += () => Destroy(gameObject);
            var agent = GetComponent<FriendlyUnitAgent>() ?? gameObject.AddComponent<FriendlyUnitAgent>();
            agent.Configure(health, city, commandModel, FriendlyUnitKind.Puppet, research, MoveSpeed, AttackRange, DamagePerSecond, DamageType.Physical, 1.25f);
        }
    }
}
