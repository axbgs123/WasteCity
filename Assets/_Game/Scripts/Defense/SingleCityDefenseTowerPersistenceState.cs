namespace WasteCity.Defense
{
    public sealed class SingleCityDefenseTowerPersistenceState
    {
        public SingleCityDefenseTowerPersistenceState(
            string stableInstanceId,
            string buildingId,
            float x,
            float z,
            int localConsumableAmount,
            float activeConsumableSeconds,
            float damageRemainder,
            string targetStableEnemyId,
            bool isLogisticsConnected,
            bool isPlayerPaused)
        {
            StableInstanceId = stableInstanceId;
            BuildingId = buildingId;
            X = x;
            Z = z;
            LocalConsumableAmount = localConsumableAmount;
            ActiveConsumableSeconds = activeConsumableSeconds;
            DamageRemainder = damageRemainder;
            TargetStableEnemyId = targetStableEnemyId;
            IsLogisticsConnected = isLogisticsConnected;
            IsPlayerPaused = isPlayerPaused;
        }

        public string StableInstanceId { get; }
        public string BuildingId { get; }
        public float X { get; }
        public float Z { get; }
        public int LocalConsumableAmount { get; }
        public float ActiveConsumableSeconds { get; }
        public float DamageRemainder { get; }
        public string TargetStableEnemyId { get; }
        public bool IsLogisticsConnected { get; }
        public bool IsPlayerPaused { get; }
    }
}
