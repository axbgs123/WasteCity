using UnityEngine;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Progression;
using WasteCity.World;

namespace WasteCity.Legacy
{
    public sealed class LegacyEffectsController : MonoBehaviour
    {
        [SerializeField] private LegacySelectionController selection;
        [SerializeField] private FormalEconomyController economy;
        [SerializeField] private FormalProgressionController progression;
        [SerializeField] private FormalCombatController combat;
        public LegacyEffectModel Model { get; private set; }
        public int GrayChestsOpened { get; private set; }
        public string LastChestReward { get; private set; }
        private bool debtEnabled;
        private int ordinaryKills;
        private void Start()
        {
            Model = new LegacyEffectModel(selection.Model, new WorldSeed(8128));
            combat.EnemyDefeated += OnEnemyDefeated;
            economy.Inventory.DebtIncreased += amount => progression.Observation.Add($"虚空债透支 {amount}", Mathf.Max(1f, amount * .1f));
        }
        private void Update()
        {
            if (!debtEnabled && Model.Active(LegacyEffectModel.VoidDebt)) { economy.Inventory.SetDebtLimit(1000000); debtEnabled = true; }
        }
        private void OnEnemyDefeated(bool heavy)
        {
            if (heavy || !Model.RollsGrayChest(ordinaryKills++)) return;
            string id = Model.ChestResource(ordinaryKills - 1); int amount = 8 + ordinaryKills % 5;
            economy.Inventory.Add(id, amount); GrayChestsOpened++; LastChestReward = $"灰烬宝箱：{id} +{amount} · 叙事碎片 #{ordinaryKills}";
        }
        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(LastChestReward)) GUI.Box(new Rect(Screen.width - 390f, 285f, 370f, 52f), LastChestReward);
            if (debtEnabled) GUI.Box(new Rect(Screen.width - 390f, 345f, 370f, 45f), "虚空债已启用：资源可透支，透支将持续增加异常观测值");
        }
    }
}
