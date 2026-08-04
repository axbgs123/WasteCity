using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.Building;
using WasteCity.Leader;
using WasteCity.Presentation;
using WasteCity.Research;

namespace WasteCity.Combat
{
    public sealed class FormalTechnologyRouteController : MonoBehaviour, ITurretCombatModifierSource
    {
        [SerializeField] private ResearchController research;
        [SerializeField] private FormalLeaderController leader;
        [SerializeField] private Transform city;

        private static Sprite square;
        private GameObject marker;

        public TechnologyOverloadModel Model { get; } = new TechnologyOverloadModel();
        public GameObject Marker => marker;
        public float FireRateMultiplier => TurretCombatModifierRules.ResolveFireRate(
            leader?.FireRateMultiplier ?? 1f,
            Model.FireRateMultiplier);

        public float DamageMultiplier(DamageType damageType) =>
            TurretCombatModifierRules.ResolveDamage(damageType, Model.DamageMultiplier(damageType));

        public void Configure(ResearchController researchController, FormalLeaderController leaderController, Transform cityTransform)
        {
            research = researchController;
            leader = leaderController;
            city = cityTransform;
            EnsureMarker();
            SyncMarker();
        }

        private void Update()
        {
            Model.Tick(Time.deltaTime);
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
                TryActivate();
            SyncMarker();
        }

        public bool TryActivate()
        {
            bool activated = Model.TryActivate(research != null && research.HasEnergyWeapons);
            SyncMarker();
            return activated;
        }

        public void Restore(float cooldown, float boost, float lockout)
        {
            Model.Restore(research != null && research.HasEnergyWeapons, cooldown, boost, lockout);
            SyncMarker();
        }

        private void EnsureMarker()
        {
            if (marker != null || city == null) return;
            if (square == null)
                square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1f);
            marker = new GameObject("TechnologyOverloadMarker");
            marker.transform.SetParent(city, false);
            marker.transform.localPosition = new Vector3(0f, 0f, -.25f);
            marker.transform.localScale = new Vector3(3.7f, 2.7f, 1f);
            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = new Color(1f, .45f, .08f, .35f);
            renderer.sortingOrder = 13;
            VisualSlot.Attach(marker, "technology.status.overload", renderer, renderer.color);
        }

        private void SyncMarker()
        {
            EnsureMarker();
            if (marker != null) marker.SetActive(Model.Phase == TechnologyOverloadPhase.Boosting);
        }

        private void OnGUI()
        {
            if (research == null || !research.HasEnergyWeapons) return;
            string phase = Model.Phase == TechnologyOverloadPhase.Boosting
                ? $"过载 {Model.BoostRemaining:0.0}s"
                : Model.Phase == TechnologyOverloadPhase.Lockout
                    ? $"过热停火 {Model.LockoutRemaining:0.0}s"
                    : Model.CooldownRemaining > 0f
                        ? $"冷却 {Model.CooldownRemaining:0.0}s"
                        : "就绪";
            GUI.Box(new Rect(18f, 373f, 390f, 44f), $"[T] 科技过载：{phase}");
        }
    }
}
