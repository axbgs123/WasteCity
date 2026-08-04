using UnityEngine;
using WasteCity.Presentation;

namespace WasteCity.Combat
{
    public sealed class EnemySwordIntentStatus : MonoBehaviour
    {
        private readonly SwordIntentModel model = new SwordIntentModel();
        private PlaceholderEnemy owner;
        private HealthComponent health;
        private static Sprite square;

        public int Stacks => model.Stacks;
        public GameObject Marker { get; private set; }

        public void Configure(PlaceholderEnemy enemy, HealthComponent targetHealth)
        {
            owner = enemy;
            health = targetHealth;
            SyncMarker();
        }

        public void Apply()
        {
            if (!CanReceiveSwordIntent()) return;
            SwordIntentHitResult result = model.AddHit(health.Value.Maximum);
            SyncMarker();
            if (result.Executed) health.Value.ApplyTrueDamage(result.TrueDamage);
        }

        public void Restore(int stacks)
        {
            if (!CanReceiveSwordIntent())
            {
                Clear();
                return;
            }

            model.Restore(stacks);
            SyncMarker();
        }

        public void Clear()
        {
            model.Clear();
            SyncMarker();
        }

        private bool CanReceiveSwordIntent()
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
            float scale = .24f + model.Stacks * .025f;
            Marker.transform.localScale = Vector3.one * scale;
            Marker.GetComponent<SpriteRenderer>().color =
                new Color(.25f, .8f, 1f, .45f + model.Stacks * .02f);
        }

        private void EnsureMarker()
        {
            if (Marker != null) return;
            if (square == null)
                square = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0, 0, 1, 1),
                    Vector2.one * .5f,
                    1f);
            Marker = new GameObject("Placeholder_SwordIntentStatus");
            Marker.transform.SetParent(transform, false);
            Marker.transform.localPosition = Vector3.up;
            Marker.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            var renderer = Marker.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.sortingOrder = 13;
            VisualSlot.Attach(
                Marker,
                "cultivation.status.sword-intent",
                renderer,
                new Color(.25f, .8f, 1f, .6f));
        }
    }
}
