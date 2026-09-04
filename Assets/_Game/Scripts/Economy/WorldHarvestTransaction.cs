using System;

namespace WasteCity.Economy
{
    public enum WorldHarvestTransactionStatus
    {
        Completed = 0,
        InvalidRequest = 1,
        NodeDepleted = 2,
        BackpackFull = 3,
        CommitFailed = 4,
    }

    public readonly struct WorldHarvestTransactionResult
    {
        private WorldHarvestTransactionResult(
            WorldHarvestTransactionStatus status,
            string resourceId,
            int amount,
            string message)
        {
            Status = status;
            ResourceId = resourceId ?? string.Empty;
            Amount = Math.Max(0, amount);
            Message = message ?? string.Empty;
        }

        public WorldHarvestTransactionStatus Status { get; }
        public string ResourceId { get; }
        public int Amount { get; }
        public string Message { get; }
        public bool Succeeded =>
            Status == WorldHarvestTransactionStatus.Completed && Amount > 0;

        public static WorldHarvestTransactionResult Completed(
            string resourceId)
        {
            return new WorldHarvestTransactionResult(
                WorldHarvestTransactionStatus.Completed,
                resourceId,
                1,
                string.Empty);
        }

        public static WorldHarvestTransactionResult Failed(
            WorldHarvestTransactionStatus status,
            string message)
        {
            return new WorldHarvestTransactionResult(
                status == WorldHarvestTransactionStatus.Completed
                    ? WorldHarvestTransactionStatus.CommitFailed
                    : status,
                string.Empty,
                0,
                message);
        }
    }

    public static class WorldHarvestTransaction
    {
        public static WorldHarvestTransactionResult TryCommitOne(
            string resourceId,
            Func<string, int, bool> canAccept,
            Func<string, int, bool> harvest,
            Func<string, int, bool> addToBackpack,
            Action<string, int> rollbackHarvest)
        {
            if (!ResourceCapacityPolicy.IsRegisteredResource(resourceId) ||
                canAccept == null ||
                harvest == null ||
                addToBackpack == null ||
                rollbackHarvest == null)
            {
                return WorldHarvestTransactionResult.Failed(
                    WorldHarvestTransactionStatus.InvalidRequest,
                    "手采事务参数无效");
            }

            const int amount = 1;
            bool harvested = false;
            bool rollbackAttempted = false;
            try
            {
                if (!canAccept(resourceId, amount))
                {
                    return WorldHarvestTransactionResult.Failed(
                        WorldHarvestTransactionStatus.BackpackFull,
                        "背包已满");
                }
                if (!harvest(resourceId, amount))
                {
                    return WorldHarvestTransactionResult.Failed(
                        WorldHarvestTransactionStatus.NodeDepleted,
                        "矿脉已枯竭");
                }
                harvested = true;
                if (addToBackpack(resourceId, amount))
                    return WorldHarvestTransactionResult.Completed(resourceId);

                rollbackAttempted = true;
                rollbackHarvest(resourceId, amount);
                return WorldHarvestTransactionResult.Failed(
                    WorldHarvestTransactionStatus.CommitFailed,
                    "背包提交失败，矿脉扣减已回滚");
            }
            catch
            {
                if (harvested && !rollbackAttempted)
                {
                    try
                    {
                        rollbackHarvest(resourceId, amount);
                    }
                    catch
                    {
                        // The composition owner reports the failed transaction;
                        // save rollback remains responsible for global recovery.
                    }
                }
                return WorldHarvestTransactionResult.Failed(
                    WorldHarvestTransactionStatus.CommitFailed,
                    "手采事务失败");
            }
        }
    }
}
