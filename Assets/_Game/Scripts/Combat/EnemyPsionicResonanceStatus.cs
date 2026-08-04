using UnityEngine;
using WasteCity.Presentation;

namespace WasteCity.Combat
{
    public sealed class EnemyPsionicResonanceStatus : MonoBehaviour
    {
        private readonly PsionicResonanceModel model = new PsionicResonanceModel();
        private PlaceholderEnemy owner;
        private HealthComponent health;
        private static Sprite square;

        public float Remaining => model.Remaining;
        public bool Active => model.Active;
        public GameObject Marker { get; private set; }

        public void Configure(PlaceholderEnemy enemy, HealthComponent targetHealth)
        {
            owner = enemy;
            health = targetHealth;
            SyncMarker();
        }

        public void ReceivePrimaryHit(int primaryAppliedDamage)
        {
            if (primaryAppliedDamage <= 0 || !CanParticipate()) return;
            int activeMarkerCount = CountActiveMarkers();
            if (!PsionicResonanceRules.CanMark(model.Active, activeMarkerCount)) return;

            model.Apply();
            SyncMarker();
            int synchronizedRawDamage =
                PsionicResonanceRules.SynchronizedRawDamage(primaryAppliedDamage);
            if (synchronizedRawDamage <= 0) return;

            foreach (var status in Object.FindObjectsOfType<EnemyPsionicResonanceStatus>())
            {
                if (status == this || !status.CanSynchronizeDamage()) continue;
                status.health.Value.Apply(
                    synchronizedRawDamage,
                    DamageType.Psionic,
                    status.health.Armor);
            }
        }

        public void Restore(float remaining)
        {
            if (remaining <= 0f || !CanParticipate())
            {
                Clear();
                return;
            }

            if (!PsionicResonanceRules.CanMark(model.Active, CountActiveMarkers()))
            {
                Clear();
                return;
            }

            model.Restore(remaining);
            SyncMarker();
        }

        public void Clear()
        {
            model.Clear();
            SyncMarker();
        }

        private void Update()
        {
            if (!CanParticipate())
            {
                if (model.Active) Clear();
                return;
            }

            bool wasActive = model.Active;
            model.Tick(Time.deltaTime);
            if (wasActive != model.Active) SyncMarker();
        }

        private int CountActiveMarkers()
        {
            int count = 0;
            foreach (var status in Object.FindObjectsOfType<EnemyPsionicResonanceStatus>())
                if (status.CanSynchronizeDamage()) count++;
            return count;
        }

        private bool CanParticipate()
        {
            return owner != null
                && health != null
                && health.Value != null
                && !health.Value.IsDead
                && !owner.IsControlled;
        }

        private bool CanSynchronizeDamage()
        {
            return model.Active && CanParticipate();
        }

        private void SyncMarker()
        {
            if (!model.Active)
            {
                if (Marker != null) Marker.SetActive(false);
                return;
            }

            EnsureMarker();
            Marker.SetActive(true);
            float phase = model.Remaining / PsionicResonanceModel.DurationSeconds;
            Marker.transform.localScale = Vector3.one * (.36f + phase * .14f);
            Marker.GetComponent<SpriteRenderer>().color =
                new Color(.72f, .28f, 1f, .45f + phase * .3f);
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
            Marker = new GameObject("Placeholder_PsionicResonance");
            Marker.transform.SetParent(transform, false);
            Marker.transform.localPosition = Vector3.up * 1.2f;
            Marker.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            var renderer = Marker.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.sortingOrder = 14;
            VisualSlot.Attach(
                Marker,
                "psionics.status.resonance",
                renderer,
                new Color(.72f, .28f, 1f, .7f));
        }
    }
}
