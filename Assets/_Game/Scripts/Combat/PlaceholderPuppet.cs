using UnityEngine;

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
        private HealthComponent health;
        private float attackRemainder;
        public HealthComponent Health => health;

        public void Configure(Transform cityTransform, int restoredHealth = -1)
        {
            city = cityTransform;
            health = GetComponent<HealthComponent>();
            health.Configure(MaximumHealth, ArmorType.Light);
            if (restoredHealth >= 0) health.Value.Restore(restoredHealth);
            health.Value.Died += () => Destroy(gameObject);
        }

        private void Update()
        {
            PlaceholderEnemy target = null;
            float best = float.MaxValue;
            foreach (var enemy in Object.FindObjectsOfType<PlaceholderEnemy>())
            {
                if (enemy.IsControlled) continue;
                float distance = Vector2.Distance(transform.position, enemy.transform.position);
                if (distance < best) { best = distance; target = enemy; }
            }
            if (target == null)
            {
                if (city != null && Vector2.Distance(transform.position, city.position) > 4f)
                    transform.position = Vector2.MoveTowards(transform.position, city.position, MoveSpeed * Time.deltaTime);
                return;
            }
            if (best > AttackRange) transform.position = Vector2.MoveTowards(transform.position, target.transform.position, MoveSpeed * Time.deltaTime);
            else
            {
                attackRemainder += DamagePerSecond * Time.deltaTime;
                int damage = Mathf.FloorToInt(attackRemainder);
                if (damage > 0)
                {
                    var targetHealth = target.GetComponent<HealthComponent>();
                    targetHealth.Value.Apply(damage, DamageType.Physical, targetHealth.Armor);
                    attackRemainder -= damage;
                }
            }
        }
    }
}
