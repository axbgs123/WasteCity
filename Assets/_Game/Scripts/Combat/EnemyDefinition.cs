using System;
using WasteCity.Content;

namespace WasteCity.Combat
{
    public enum EnemyArchetype { Gnawer, CrystalBeast, Howler, Burrower, CrystalBroodmother }
    public enum EnemyTargetPriority { Nearest=0, Walls=1, Production=2, Core=3 }

    public sealed class EnemyDefinition
    {
        public StableId Id { get; }
        public string Name { get; }
        public EnemyArchetype Archetype { get; }
        public int MaximumHealth { get; }
        public float MoveSpeed { get; }
        public float DamagePerSecond { get; }
        public float AttackRange { get; }
        public ArmorType Armor { get; }
        public int BiomassDrop { get; }
        public EnemyTargetPriority TargetPriority { get; }
        public bool IsMechanical { get; }
        public bool IsHeavy => Armor == ArmorType.Heavy || Archetype == EnemyArchetype.CrystalBroodmother;

        public EnemyDefinition(string id,string name,EnemyArchetype archetype,int health,float speed,float dps,float range,ArmorType armor,int biomass,EnemyTargetPriority priority,bool isMechanical=false)
        {
            Id=new StableId(id);Name=name;Archetype=archetype;MaximumHealth=Math.Max(1,health);MoveSpeed=Math.Max(.1f,speed);
            DamagePerSecond=Math.Max(0f,dps);AttackRange=Math.Max(.5f,range);Armor=armor;BiomassDrop=Math.Max(0,biomass);TargetPriority=priority;IsMechanical=isMechanical;
        }
    }

    public static class EnemyCatalog
    {
        public static readonly EnemyDefinition Gnawer=new EnemyDefinition("core.enemy.gnawer","啃噬者",EnemyArchetype.Gnawer,60,1.8f,8,2,ArmorType.Light,1,EnemyTargetPriority.Core);
        public static readonly EnemyDefinition CrystalBeast=new EnemyDefinition("core.enemy.crystal-beast","晶壳兽",EnemyArchetype.CrystalBeast,220,.9f,20,2,ArmorType.Heavy,3,EnemyTargetPriority.Walls);
        public static readonly EnemyDefinition Howler=new EnemyDefinition("core.enemy.howler","啸叫者",EnemyArchetype.Howler,100,1.2f,12,7,ArmorType.Light,2,EnemyTargetPriority.Production);
        public static readonly EnemyDefinition Burrower=new EnemyDefinition("core.enemy.crystal-burrower","结晶掘地者",EnemyArchetype.Burrower,500,1.4f,25,2,ArmorType.Heavy,8,EnemyTargetPriority.Production,true);
        public static readonly EnemyDefinition CrystalBroodmother=new EnemyDefinition("core.enemy.crystal-broodmother","晶壳母体",EnemyArchetype.CrystalBroodmother,4000,.6f,35,3,ArmorType.Heavy,30,EnemyTargetPriority.Nearest);
        public static readonly EnemyDefinition[] All={Gnawer,CrystalBeast,Howler,Burrower,CrystalBroodmother};
    }
}
