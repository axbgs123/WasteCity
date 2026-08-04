using UnityEngine;
using WasteCity.Research;

namespace WasteCity.Combat
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class PlaceholderBehemoth : MonoBehaviour
    {
        public const int MaximumHealth = 650;
        private const float MoveSpeed = 1.35f;
        private const float AttackRange = 1.8f;
        private const float DamagePerSecond = 34f;
        private Transform city;
        private ResearchController research;
        private HealthComponent health;
        public HealthComponent Health => health;

        public void Configure(Transform cityTransform, ResearchController researchController, int restoredHealth = -1, FriendlyUnitCommandModel commandModel = null)
        {
            city = cityTransform; research = researchController; health = GetComponent<HealthComponent>(); health.Configure(MaximumHealth, ArmorType.Heavy);
            if (restoredHealth >= 0) health.Value.Restore(restoredHealth);
            health.Value.Died += () => Destroy(gameObject);
            var agent = GetComponent<FriendlyUnitAgent>() ?? gameObject.AddComponent<FriendlyUnitAgent>();
            agent.Configure(health, city, commandModel, FriendlyUnitKind.Behemoth, research, MoveSpeed, AttackRange, DamagePerSecond, DamageType.Biological, 1.75f);
        }
    }
}
