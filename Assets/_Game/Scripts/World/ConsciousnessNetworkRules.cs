namespace WasteCity.World
{
    public static class ConsciousnessNetworkRules
    {
        public static bool RemoteLinkAvailable(
            bool researchCompleted,
            int operationalNetworks)
        {
            return researchCompleted && operationalNetworks > 0;
        }
    }
}
