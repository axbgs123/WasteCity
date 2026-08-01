using UnityEngine;

namespace WasteCity.Combat
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class PlaceholderEnemy : MonoBehaviour
    {
        private HealthComponent health;
        private HealthComponent cityHealth;
        private Transform city;
        private float attackRemainder;
        public void Configure(HealthComponent targetHealth, Transform target, bool heavy)
        {
            health = GetComponent<HealthComponent>(); health.Configure(heavy ? 240 : 70, heavy ? ArmorType.Heavy : ArmorType.Light);
            cityHealth = targetHealth; city = target; health.Value.Died += () => Destroy(gameObject);
        }
        private void Update()
        {
            if (city == null || cityHealth.Value.IsDead) return;
            float distance = Vector2.Distance(transform.position, city.position);
            if (distance > 2f) transform.position = Vector2.MoveTowards(transform.position, city.position, 1.5f * Time.deltaTime);
            else { attackRemainder += 8f * Time.deltaTime; int damage = Mathf.FloorToInt(attackRemainder); if (damage > 0) { cityHealth.Value.Apply(damage, DamageType.Physical, cityHealth.Armor); attackRemainder -= damage; } }
        }
    }
}
