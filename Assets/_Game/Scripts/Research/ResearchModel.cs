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
        public IReadOnlyList<string> RequiredResearchIds { get; }
        public int Tier { get; }
        public string EffectSummary { get; }
        public ResearchDefinition(string id, string name, DevelopmentRoute route, string costId, int cost, float duration,string requiredResearchId=null,int tier=1,string effectSummary=null,params string[] additionalRequirements)
        { Id = new StableId(id); Name = name; Route = route; CostId = costId; Cost = Math.Max(0, cost); Duration = Math.Max(0.1f, duration);RequiredResearchId=requiredResearchId;Tier=Math.Max(1,Math.Min(3,tier));EffectSummary=effectSummary??"规则效果待运行系统接入";var requirements=new List<string>();if(!string.IsNullOrEmpty(requiredResearchId))requirements.Add(requiredResearchId);if(additionalRequirements!=null)requirements.AddRange(additionalRequirements.Where(value=>!string.IsNullOrEmpty(value)));RequiredResearchIds=requirements; }
    }
    public static class ResearchCatalog
    {
        private static ResearchDefinition Node(string id,string name,DevelopmentRoute route,int tier,string costId,int cost,float duration,string effect,string required=null,params string[] additional)=>new ResearchDefinition(id,name,route,costId,cost,duration,required,tier,effect,additional);
        public static readonly ResearchDefinition[] All =
        {
            Node("core.research.automated-machinery","基础冶金",DevelopmentRoute.Technology,1,ResourceIds.Iron,10,20,"解锁冶炼厂"),
            Node("core.research.spirit-sensing","灵火淬炼",DevelopmentRoute.Cultivation,1,ResourceIds.EnergyCrystal,10,20,"解锁灵火炉"),
            Node("core.research.adaptive-tissue","菌落培养",DevelopmentRoute.BiologicalAscension,1,ResourceIds.Biomass,10,20,"解锁菌落池"),
            Node("core.research.mind-resonance","意识共振",DevelopmentRoute.Psionics,1,ResourceIds.Water,10,20,"解锁共振炉"),
            Node("core.research.legacy-analysis","遗产解析",DevelopmentRoute.Technology,2,ResourceIds.Alloy,30,60,"满足首循环文明升阶条件","core.research.automated-machinery"),

            Node("core.research.precision-assembly","精密装配",DevelopmentRoute.Technology,2,ResourceIds.Alloy,20,40,"解锁装配厂、弹药量产","core.research.automated-machinery"),
            Node("core.research.automated-defense","自动防御架构",DevelopmentRoute.Technology,2,ResourceIds.Alloy,20,40,"解锁机枪塔","core.research.automated-machinery"),
            Node("core.research.thermal-engineering","热能工程",DevelopmentRoute.Technology,2,ResourceIds.Iron,20,40,"解锁发电站、城墙升级","core.research.automated-machinery"),
            Node("core.research.ballistics","弹道学",DevelopmentRoute.Technology,2,ResourceIds.Iron,20,40,"炮塔射程+20%、弹药伤害+15%","core.research.automated-machinery"),
            Node("core.research.alloy-armor","合金装甲",DevelopmentRoute.Technology,3,ResourceIds.Alloy,35,60,"解锁重型机枪塔、建筑耐久+30%","core.research.precision-assembly"),
            Node("core.research.unmanned-systems","无人系统",DevelopmentRoute.Technology,3,ResourceIds.Alloy,35,60,"解锁侦查无人机、自动维修机甲","core.research.automated-defense"),
            Node("core.research.orbital-supply","轨道补给",DevelopmentRoute.Technology,3,ResourceIds.Alloy,35,60,"物流范围扩大至24格","core.research.thermal-engineering"),
            Node("core.research.energy-weapons","能量武器",DevelopmentRoute.Technology,3,ResourceIds.EnergyCrystal,35,60,"解锁激光塔","core.research.ballistics"),

            Node("core.research.artifact-crafting","炼器基础",DevelopmentRoute.Cultivation,2,ResourceIds.EnergyCrystal,20,40,"解锁炼器坊、飞剑量产","core.research.spirit-sensing"),
            Node("core.research.sword-array","剑阵初解",DevelopmentRoute.Cultivation,2,ResourceIds.EnergyCrystal,20,40,"解锁剑阵台","core.research.spirit-sensing"),
            Node("core.research.spirit-gathering","聚灵术",DevelopmentRoute.Cultivation,2,ResourceIds.EnergyCrystal,20,40,"解锁聚灵阵","core.research.spirit-sensing"),
            Node("core.research.talisman-basics","符箓入门",DevelopmentRoute.Cultivation,2,ResourceIds.Stone,20,40,"城墙附加基础防护符","core.research.spirit-sensing"),
            Node("core.research.sword-riding","御剑术",DevelopmentRoute.Cultivation,3,ResourceIds.EnergyCrystal,35,60,"解锁御剑台、飞剑射程+30%","core.research.artifact-crafting"),
            Node("core.research.alchemy","炼丹术",DevelopmentRoute.Cultivation,3,ResourceIds.Biomass,35,60,"解锁炼丹房","core.research.sword-array"),
            Node("core.research.formation-reinforcement","阵法强化",DevelopmentRoute.Cultivation,3,ResourceIds.EnergyCrystal,35,60,"扩大物流区、聚灵产量+50%","core.research.spirit-gathering"),
            Node("core.research.puppetry","傀儡术",DevelopmentRoute.Cultivation,3,ResourceIds.Alloy,35,60,"解锁傀儡工坊","core.research.talisman-basics"),

            Node("core.research.bio-cultivation","生物培育",DevelopmentRoute.BiologicalAscension,2,ResourceIds.Biomass,20,40,"解锁培育室、基础生物武器","core.research.adaptive-tissue"),
            Node("core.research.spore-dispersal","孢子散布",DevelopmentRoute.BiologicalAscension,2,ResourceIds.Biomass,20,40,"解锁孢子塔","core.research.adaptive-tissue"),
            Node("core.research.metabolic-acceleration","代谢加速",DevelopmentRoute.BiologicalAscension,2,ResourceIds.Biomass,20,40,"怪物尸体回收+50%","core.research.adaptive-tissue"),
            Node("core.research.carapace-growth","甲壳增生",DevelopmentRoute.BiologicalAscension,2,ResourceIds.Biomass,20,40,"城墙消耗生物质缓慢再生","core.research.adaptive-tissue"),
            Node("core.research.behemoth-breeding","巨兽培育",DevelopmentRoute.BiologicalAscension,3,ResourceIds.Biomass,35,60,"解锁巨兽栏","core.research.bio-cultivation"),
            Node("core.research.acid-spit","酸液喷吐",DevelopmentRoute.BiologicalAscension,3,ResourceIds.Biomass,35,60,"解锁酸液塔","core.research.spore-dispersal"),
            Node("core.research.tissue-regeneration","组织再生",DevelopmentRoute.BiologicalAscension,3,ResourceIds.Biomass,35,60,"建筑与军队缓慢回血","core.research.metabolic-acceleration"),
            Node("core.research.gene-splicing","基因剪接",DevelopmentRoute.BiologicalAscension,3,ResourceIds.Biomass,35,60,"领袖临时生物特质","core.research.carapace-growth"),

            Node("core.research.psionic-workshop","灵能工坊",DevelopmentRoute.Psionics,2,ResourceIds.Water,20,40,"解锁灵能工坊、增幅器量产","core.research.mind-resonance"),
            Node("core.research.mind-spire","心灵尖塔",DevelopmentRoute.Psionics,2,ResourceIds.EnergyCrystal,20,40,"解锁心灵尖塔","core.research.mind-resonance"),
            Node("core.research.consciousness-network","意识网络",DevelopmentRoute.Psionics,2,ResourceIds.Water,20,40,"远程通讯免费","core.research.mind-resonance"),
            Node("core.research.thought-acceleration","思维加速",DevelopmentRoute.Psionics,2,ResourceIds.Water,20,40,"研究速度+25%","core.research.mind-resonance"),
            Node("core.research.mind-shield","心灵护盾",DevelopmentRoute.Psionics,3,ResourceIds.EnergyCrystal,35,60,"解锁护盾发生器","core.research.psionic-workshop"),
            Node("core.research.mind-control","精神操控",DevelopmentRoute.Psionics,3,ResourceIds.Water,35,60,"小概率控制普通怪物","core.research.mind-spire"),
            Node("core.research.precognitive-sense","预知感应",DevelopmentRoute.Psionics,3,ResourceIds.Water,35,60,"波次预警提前50%","core.research.consciousness-network"),
            Node("core.research.collective-consciousness","集体意识",DevelopmentRoute.Psionics,3,ResourceIds.Water,35,60,"多城市共享研究进度","core.research.thought-acceleration"),

            Node("core.research.bridge.psionic-mech","灵能机甲",DevelopmentRoute.Technology,3,ResourceIds.Alloy,50,90,"解锁灵能机甲厂","core.research.precision-assembly","core.research.psionic-workshop"),
            Node("core.research.bridge.high-frequency-sword","高周波飞剑",DevelopmentRoute.Cultivation,3,ResourceIds.Alloy,50,90,"解锁飞剑铸造台","core.research.artifact-crafting","core.research.precision-assembly"),
            Node("core.research.bridge.bio-hangar","生物机库",DevelopmentRoute.BiologicalAscension,3,ResourceIds.Biomass,50,90,"解锁生物机库","core.research.bio-cultivation","core.research.precision-assembly"),
            Node("core.research.bridge.spirit-plant","灵植培育",DevelopmentRoute.Cultivation,3,ResourceIds.Biomass,50,90,"解锁灵植园","core.research.artifact-crafting","core.research.bio-cultivation"),
            Node("core.research.bridge.psionic-pulse","精神脉冲武器",DevelopmentRoute.Psionics,3,ResourceIds.EnergyCrystal,50,90,"解锁EMP塔","core.research.psionic-workshop","core.research.precision-assembly"),
            Node("core.research.bridge.flesh-elixir","血肉灵丹",DevelopmentRoute.BiologicalAscension,3,ResourceIds.Biomass,50,90,"活性生物质炼丹","core.research.bio-cultivation","core.research.artifact-crafting")
        };
        public static ResearchDefinition[] Starting=>All;
        public static ResearchDefinition Find(string id)=>All.FirstOrDefault(value=>value.Id.Value==id);
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
            if (Active != null || definition == null || completed.Contains(definition.Id) || definition.RequiredResearchIds.Any(required=>!completed.Any(id=>id.Value==required)) || !inventory.TrySpend(definition.CostId, definition.Cost)) return false;
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
            completed.Clear();if(completedIds!=null)foreach(string id in completedIds){var definition=ResearchCatalog.Find(id);if(definition!=null)completed.Add(definition.Id);}
            Active=ResearchCatalog.Find(activeId);Remaining=Active==null?0f:Math.Max(0.001f,Math.Min(Active.Duration,remaining));
        }
    }
}
