using System;
using System.Collections.Generic;
using WasteCity.Content;
using WasteCity.Economy;
using System.Linq;

namespace WasteCity.Research
{
    public enum DevelopmentRoute { Technology, Cultivation, BiologicalAscension, Psionics }
    public sealed class ResearchDefinition
    {
        public StableId Id { get; }
        public string Name { get; }
        public DevelopmentRoute Route { get; }
        public string CostId { get; }
        public int Cost { get; }
        public float Duration { get; }
        public string RequiredResearchId { get; }
        public ResearchDefinition(string id, string name, DevelopmentRoute route, string costId, int cost, float duration,string requiredResearchId=null)
        { Id = new StableId(id); Name = name; Route = route; CostId = costId; Cost = Math.Max(0, cost); Duration = Math.Max(0.1f, duration);RequiredResearchId=requiredResearchId; }
    }
    public static class ResearchCatalog
    {
        public static readonly ResearchDefinition[] Starting =
        {
            new ResearchDefinition("core.research.automated-machinery", "自动机械", DevelopmentRoute.Technology, ResourceIds.Iron, 10, 20f),
            new ResearchDefinition("core.research.spirit-sensing", "灵气感知", DevelopmentRoute.Cultivation, ResourceIds.EnergyCrystal, 10, 20f),
            new ResearchDefinition("core.research.adaptive-tissue", "适应组织", DevelopmentRoute.BiologicalAscension, ResourceIds.Biomass, 10, 20f),
            new ResearchDefinition("core.research.mind-resonance", "意识共鸣", DevelopmentRoute.Psionics, ResourceIds.Water, 10, 20f),
            new ResearchDefinition("core.research.legacy-analysis", "遗产解析", DevelopmentRoute.Technology, ResourceIds.Alloy, 30, 60f,"core.research.automated-machinery")
        };
    }
    public sealed class ResearchModel
    {
        private readonly HashSet<StableId> completed = new HashSet<StableId>();
        public ResearchDefinition Active { get; private set; }
        public float Remaining { get; private set; }
        public int CompletedCount => completed.Count;
        public event Action<ResearchDefinition> Completed;
        public bool Start(ResearchDefinition definition, ResourceInventory inventory)
        {
            if (Active != null || definition == null || completed.Contains(definition.Id) || (!string.IsNullOrEmpty(definition.RequiredResearchId)&&!completed.Any(id=>id.Value==definition.RequiredResearchId)) || !inventory.TrySpend(definition.CostId, definition.Cost)) return false;
            Active = definition; Remaining = definition.Duration; return true;
        }
        public bool Tick(float delta)
        {
            if (Active == null) return false; Remaining -= Math.Max(0f, delta); if (Remaining > 0.0001f) return false;
            ResearchDefinition finished = Active; completed.Add(finished.Id); Active = null; Remaining = 0f; Completed?.Invoke(finished); return true;
        }
        public bool IsCompleted(StableId id) => completed.Contains(id);
        public string[] CaptureCompleted()=>completed.Select(id=>id.Value).ToArray();
        public void Restore(string[] completedIds,string activeId,float remaining)
        {
            completed.Clear();if(completedIds!=null)foreach(string id in completedIds)foreach(var definition in ResearchCatalog.Starting)if(definition.Id.Value==id)completed.Add(definition.Id);
            Active=ResearchCatalog.Starting.FirstOrDefault(value=>value.Id.Value==activeId);Remaining=Active==null?0f:Math.Max(0.001f,Math.Min(Active.Duration,remaining));
        }
    }
}
