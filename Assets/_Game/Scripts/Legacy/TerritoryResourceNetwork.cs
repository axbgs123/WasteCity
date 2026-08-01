using WasteCity.Economy;

namespace WasteCity.Legacy
{
    public sealed class TerritoryResourceNetwork
    {
        private readonly ResourceInventory city;
        private readonly ResourceInventory local;
        public TerritoryResourceNetwork(ResourceInventory cityInventory, int localCapacity = 150) { city=cityInventory;local=new ResourceInventory(localCapacity); }
        public int Deposit(string id,int amount,bool quantumEntangled)=>quantumEntangled?city.Add(id,amount):local.Add(id,amount);
        public int Local(string id)=>local.Get(id);
        public int Collect(string id){int amount=local.Get(id);int accepted=city.Add(id,amount);if(accepted>0)local.TrySpend(id,accepted);return accepted;}
        public void Restore(string id,int amount)=>local.Restore(id,amount);
    }
    public sealed class TerritoryExtractionModel
    {
        private float progress;
        public int Tick(float delta){progress+=System.Math.Max(0f,delta);int cycles=(int)(progress/3f);progress-=cycles*3f;return cycles;}
        public float Progress=>progress;
        public void Restore(float value)=>progress=System.Math.Max(0f,System.Math.Min(2.999f,value));
    }
}
