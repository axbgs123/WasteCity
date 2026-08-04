using System.Collections.Generic;
using UnityEngine;
using WasteCity.Presentation;

namespace WasteCity.Combat
{
    public sealed class EnemyInfectionStatus : MonoBehaviour
    {
        public const float SpreadRadius = 3f;
        public const int SpreadStacks = 5;

        private readonly InfectionModel model = new InfectionModel();
        private PlaceholderEnemy owner;
        private HealthComponent health;
        private static Sprite square;

        public int Stacks => model.Stacks;
        public float Elapsed => model.Elapsed;
        public GameObject Marker { get; private set; }

        public void Configure(PlaceholderEnemy enemy, HealthComponent targetHealth)
        {
            owner = enemy;
            health = targetHealth;
            SyncMarker();
        }

        public void Apply(int stacks)
        {
            Apply(stacks, new HashSet<int>());
        }

        public void Restore(int stacks, float elapsed)
        {
            if (!CanBeInfected())
            {
                Clear();
                return;
            }

            model.Restore(stacks, elapsed);
            SyncMarker();
        }

        public void Clear()
        {
            model.Clear();
            SyncMarker();
        }

        private void Update()
        {
            if (!CanBeInfected())
            {
                if (model.Stacks > 0) Clear();
                return;
            }

            int rawDamage = model.Tick(Time.deltaTime, health.Value.Maximum);
            if (rawDamage > 0) health.Value.Apply(rawDamage, DamageType.Biological, health.Armor);
        }

        private void Apply(int stacks, HashSet<int> burstTargets)
        {
            if (stacks <= 0 || !CanBeInfected()) return;
            int ownerId = owner.GetInstanceID();
            if (burstTargets.Contains(ownerId) && model.Stacks + stacks >= InfectionModel.BurstThreshold)
            {
                model.Restore(InfectionModel.BurstThreshold - 1, model.Elapsed);
                SyncMarker();
                return;
            }

            bool burst = model.AddStacks(stacks);
            SyncMarker();
            if (!burst || !burstTargets.Add(ownerId)) return;
            Spread(burstTargets);
        }

        private void Spread(HashSet<int> burstTargets)
        {
            PlaceholderEnemy[] enemies = Object.FindObjectsOfType<PlaceholderEnemy>();
            var candidates = new InfectionSpreadCandidate[enemies.Length];
            var byId = new Dictionary<int, PlaceholderEnemy>(enemies.Length);
            for (int index = 0; index < enemies.Length; index++)
            {
                PlaceholderEnemy enemy = enemies[index];
                int id = enemy.GetInstanceID();
                candidates[index] = new InfectionSpreadCandidate(
                    id,
                    enemy.transform.position.x,
                    enemy.transform.position.y,
                    enemy.Health != null && !enemy.Health.Value.IsDead,
                    enemy.IsControlled);
                byId[id] = enemy;
            }

            int[] selected = InfectionSpreadRules.SelectTargets(
                owner.GetInstanceID(),
                transform.position.x,
                transform.position.y,
                SpreadRadius,
                candidates);
            foreach (int targetId in selected)
                if (byId.TryGetValue(targetId, out PlaceholderEnemy target))
                    target.Infection?.Apply(SpreadStacks, burstTargets);
        }

        private bool CanBeInfected()
        {
            return owner != null
                && health != null
                && health.Value != null
                && !health.Value.IsDead
                && !owner.IsControlled;
        }

        private void SyncMarker()
        {
            if (model.Stacks <= 0)
            {
                if (Marker != null) Marker.SetActive(false);
                return;
            }

            EnsureMarker();
            Marker.SetActive(true);
            float scale = .22f + model.Stacks * .035f;
            Marker.transform.localScale = Vector3.one * scale;
            var renderer = Marker.GetComponent<SpriteRenderer>();
            renderer.color = new Color(.45f, 1f, .25f, .35f + model.Stacks * .05f);
        }

        private void EnsureMarker()
        {
            if (Marker != null) return;
            if (square == null)
                square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1f);
            Marker = new GameObject("Placeholder_InfectionStatus");
            Marker.transform.SetParent(transform, false);
            Marker.transform.localPosition = Vector3.up * .75f;
            Marker.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            var renderer = Marker.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.sortingOrder = 13;
            VisualSlot.Attach(Marker, "biological.status.infection", renderer, new Color(.45f, 1f, .25f, .6f));
        }
    }
}
