using System;
using System.Collections.Generic;
using WasteCity.Content;
using WasteCity.Economy;

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
        public ResearchDefinition(string id, string name, DevelopmentRoute route, string costId, int cost, float duration)
        { Id = new StableId(id); Name = name; Route = route; CostId = costId; Cost = Math.Max(0, cost); Duration = Math.Max(0.1f, duration); }
    }
    public static class ResearchCatalog
    {
        public static readonly ResearchDefinition[] Starting =
        {
            new ResearchDefinition("core.research.automated-machinery", "自动机械", DevelopmentRoute.Technology, ResourceIds.Iron, 10, 20f),
            new ResearchDefinition("core.research.spirit-sensing", "灵气感知", DevelopmentRoute.Cultivation, ResourceIds.EnergyCrystal, 10, 20f),
            new ResearchDefinition("core.research.adaptive-tissue", "适应组织", DevelopmentRoute.BiologicalAscension, ResourceIds.Biomass, 10, 20f),
            new ResearchDefinition("core.research.mind-resonance", "意识共鸣", DevelopmentRoute.Psionics, ResourceIds.Water, 10, 20f)
        };
    }
    public sealed class ResearchModel
    {
        private readonly HashSet<StableId> completed = new HashSet<StableId>();
        public ResearchDefinition Active { get; private set; }
        public float Remaining { get; private set; }
        public int CompletedCount => completed.Count;
        public bool Start(ResearchDefinition definition, ResourceInventory inventory)
        {
            if (Active != null || definition == null || completed.Contains(definition.Id) || !inventory.TrySpend(definition.CostId, definition.Cost)) return false;
            Active = definition; Remaining = definition.Duration; return true;
        }
        public bool Tick(float delta)
        {
            if (Active == null) return false; Remaining -= Math.Max(0f, delta); if (Remaining > 0.0001f) return false;
            completed.Add(Active.Id); Active = null; Remaining = 0f; return true;
        }
        public bool IsCompleted(StableId id) => completed.Contains(id);
    }
}
