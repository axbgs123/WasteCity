namespace WasteCity.Building
{
    public sealed class BuildingUpgradeDefinition
    {
        public BuildingDefinition Source { get; }
        public BuildingDefinition Target { get; }
        public string CostId { get; }
        public int Cost { get; }
        public int RequiredCivilizationLevel { get; }
        public BuildingUpgradeDefinition(BuildingDefinition source,BuildingDefinition target,string costId,int cost,int level){Source=source;Target=target;CostId=costId;Cost=cost;RequiredCivilizationLevel=level;}
    }
    public static class BuildingUpgradeCatalog
    {
        private static readonly BuildingUpgradeDefinition HeavyTurret=new BuildingUpgradeDefinition(BuildingCatalog.MachineGunTurret,BuildingCatalog.HeavyMachineGunTurret,Economy.ResourceIds.Alloy,20,2);
        public static BuildingUpgradeDefinition For(BuildingDefinition source,int civilizationLevel)=>source==HeavyTurret.Source&&civilizationLevel>=HeavyTurret.RequiredCivilizationLevel?HeavyTurret:null;
    }
}
