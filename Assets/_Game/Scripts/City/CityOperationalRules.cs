namespace WasteCity.City
{
    public static class CityOperationalRules
    {
        public const float FortressProductionMultiplier = 1.25f;
        public const float FortressDefenseMultiplier = 1.25f;
        public static bool LongWorkAllowed(CityMode mode)=>mode==CityMode.Fortress;
        public static float ProductionMultiplier(CityMode mode)=>mode==CityMode.Fortress?FortressProductionMultiplier:1f;
        public static float DefenseMultiplier(CityMode mode)=>mode==CityMode.Fortress?FortressDefenseMultiplier:1f;
    }
}
