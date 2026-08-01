using WasteCity.World;

namespace WasteCity.Legacy
{
    public sealed class LegacyEffectModel
    {
        public const string PocketUniverse = "core.legacy.pocket-universe";
        public const string VoidDebt = "core.legacy.void-debt";
        public const string CausalTransparency = "core.legacy.causal-transparency";
        public const string VoidChest = "core.legacy.void-chest";
        private readonly LegacySelectionModel selection;
        private readonly WorldSeed seed;
        public LegacyEffectModel(LegacySelectionModel selection, WorldSeed seed) { this.selection = selection; this.seed = seed; }
        public bool Active(string id) => selection?.Selected?.Id.Value == id;
        public int ProductionUnits(int completedBuildings) => Active(PocketUniverse) && completedBuildings > 0 ? completedBuildings + 1 : completedBuildings;
        public bool RollsGrayChest(int ordinaryKillIndex) => Active(VoidChest) && seed.Sample(ordinaryKillIndex, 0, 809) % 100 == 0;
        public string ChestResource(int ordinaryKillIndex)
        {
            string[] ids = { Economy.ResourceIds.Iron, Economy.ResourceIds.Stone, Economy.ResourceIds.Water, Economy.ResourceIds.Biomass };
            return ids[seed.Sample(ordinaryKillIndex, 1, 809) % ids.Length];
        }
    }
}
