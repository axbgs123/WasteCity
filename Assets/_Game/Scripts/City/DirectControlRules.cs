namespace WasteCity.City
{
    public enum DirectControlTarget
    {
        City = 0,
        Leader = 1
    }

    public static class DirectControlRules
    {
        public static DirectControlTarget Resolve(
            CityMode mode,
            bool leaderRecruited)
        {
            return mode == CityMode.Fortress && leaderRecruited
                ? DirectControlTarget.Leader
                : DirectControlTarget.City;
        }
    }
}
