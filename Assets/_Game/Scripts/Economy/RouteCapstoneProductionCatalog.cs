namespace WasteCity.Economy
{
    public static class RouteCapstoneProductionCatalog
    {
        public static PassiveProductionProcess CreatePowerPlant()
        {
            return new PassiveProductionProcess(
                ResourceIds.EnergyCrystal,
                1,
                6f);
        }

        public static ProductionProcess CreateMetabolicFurnace()
        {
            return new ProductionProcess(
                new ProductionRecipe(
                    ResourceIds.Biomass,
                    2,
                    ResourceIds.EnergyCrystal,
                    1,
                    8f));
        }

        public static PassiveProductionProcess CreateConsciousnessNetwork()
        {
            return new PassiveProductionProcess(
                ResourceIds.PsionicAmplifier,
                1,
                10f);
        }
    }
}
