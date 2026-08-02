using WasteCity.Economy;

namespace WasteCity.Combat
{
    public sealed class DefenseTowerDefinition
    {
        public string BuildingId { get; }
        public DamageType DamageType { get; }
        public float DamagePerSecond { get; }
        public float Range { get; }
        public string ConsumableId { get; }
        public float SecondsPerConsumable { get; }
        public DefenseTowerDefinition(string id,DamageType damage,float dps,float range,string consumable,float seconds){BuildingId=id;DamageType=damage;DamagePerSecond=dps;Range=range;ConsumableId=consumable;SecondsPerConsumable=seconds;}
    }
    public static class DefenseTowerCatalog
    {
        private static readonly DefenseTowerDefinition[] All=
        {
            new DefenseTowerDefinition("core.building.machine-gun-turret",DamageType.Physical,20,10,ResourceIds.Ammunition,3),
            new DefenseTowerDefinition("core.building.heavy-machine-gun-turret",DamageType.Physical,60,11,ResourceIds.Ammunition,3),
            new DefenseTowerDefinition("cultivation.building.sword-array-tower",DamageType.TrueEssence,28,12,ResourceIds.EnergyCrystal,5),
            new DefenseTowerDefinition("biological.building.spore-tower",DamageType.Biological,18,9,ResourceIds.Biomass,5),
            new DefenseTowerDefinition("psionics.building.mind-spire",DamageType.Psionic,30,11,ResourceIds.EnergyCrystal,4)
        };
        public static DefenseTowerDefinition For(string buildingId){foreach(var value in All)if(value.BuildingId==buildingId)return value;return null;}
    }
}
