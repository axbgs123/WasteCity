using UnityEngine;
using WasteCity.Progression;
using WasteCity.Economy;
using System;
using WasteCity.Presentation;
using WasteCity.City;

namespace WasteCity.Combat
{
    public sealed class FormalCombatController : MonoBehaviour
    {
        [SerializeField] private HealthComponent cityHealth;
        [SerializeField] private Transform city;
        [SerializeField] private FormalProgressionController progression;
        [SerializeField] private FormalEconomyController economy;
        [SerializeField] private PlaceholderMobileCity cityState;
        private float defenseRemainder;
        public int SpawnedEnemies { get; private set; }
        public event Action<bool> EnemyDefeated;
        private static Sprite square;
        private void Start() => progression.Observation.ThresholdReached += OnThreshold;
        private void Update()
        {
            PlaceholderEnemy nearest = null; float best = 25f;
            foreach (var enemy in UnityEngine.Object.FindObjectsOfType<PlaceholderEnemy>()) { float sqr = ((Vector2)(enemy.transform.position - city.position)).sqrMagnitude; if (sqr < best) { best = sqr; nearest = enemy; } }
            if (nearest == null) return; defenseRemainder += 8f * CityOperationalRules.DefenseMultiplier(cityState.Deployment.Mode) * Time.deltaTime; int damage = Mathf.FloorToInt(defenseRemainder);
            if (damage > 0) { var health = nearest.GetComponent<HealthComponent>(); health.Value.Apply(damage, DamageType.Physical, health.Armor); defenseRemainder -= damage; }
        }
        private void OnThreshold(int threshold)
        {
            int count = threshold == 30 ? 6 : threshold == 60 ? 10 : 14;
            for (int i = 0; i < count; i++) Spawn(i, threshold, threshold >= 60 && i % 4 == 0);
        }
        private void Spawn(int slot, int threshold, bool heavy)
        {
            if (square == null) square = Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1f);
            float angle=(slot*47f+threshold)*Mathf.Deg2Rad; var item=new GameObject(heavy?"PlaceholderHeavyEnemy":"PlaceholderEnemy");
            item.transform.position=(Vector2)city.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*10f; item.transform.localScale=Vector3.one*(heavy?1.4f:.8f);
            var renderer=item.AddComponent<SpriteRenderer>();renderer.sprite=square;renderer.color=heavy?Color.magenta:Color.red;renderer.sortingOrder=9;
            VisualSlot.Attach(item,heavy?"core.enemy.heavy-placeholder":"core.enemy.light-placeholder",renderer,renderer.color);
            item.AddComponent<HealthComponent>();item.AddComponent<PlaceholderEnemy>().Configure(cityHealth,city,heavy,economy.Inventory, value => EnemyDefeated?.Invoke(value));SpawnedEnemies++;
        }
    }
}
