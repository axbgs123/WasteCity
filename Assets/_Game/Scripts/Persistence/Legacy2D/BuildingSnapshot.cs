using System;

namespace WasteCity.Building
{
    [Serializable]
    public sealed class BuildingSnapshot
    {
        public BuildingSnapshot()
        {
        }

        public string definitionId;
        public int x, y, site, health, shield;
        public float constructionRemaining, repairRemaining;
    }
}
