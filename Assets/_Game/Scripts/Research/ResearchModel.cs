using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WasteCity.Content;
using WasteCity.Economy;

namespace WasteCity.Research
{
    public enum DevelopmentRoute { Technology, Cultivation, BiologicalAscension, Psionics }
    public static class CollectiveConsciousnessRules
    {
        public const float SharedProgressRatio = .2f;
        public static float InheritedProgressRatio(bool unlocked) =>
            unlocked ? SharedProgressRatio : 0f;
    }

    public sealed class ResearchDefinition
    {
        public StableId Id { get; }
        public string Name { get; }
        public DevelopmentRoute Route { get; }
        public string CostId { get; }
        public int Cost { get; }
        public IReadOnlyList<ResourceAmount> Costs { get; }
        public float Duration { get; }
        public string RequiredResearchId { get; }
        public IReadOnlyList<string> RequiredResearchIds { get; }
        public int Tier { get; }
        public string EffectSummary { get; }
        public ResearchDefinition(
            string id,
            string name,
            DevelopmentRoute route,
            string costId,
            int cost,
            float duration,
            string requiredResearchId = null,
            int tier = 1,
            string effectSummary = null,
            params string[] additionalRequirements)
            : this(
                id,
                name,
                route,
                string.IsNullOrWhiteSpace(costId) || cost <= 0
                    ? Array.Empty<ResourceAmount>()
                    : new[] { new ResourceAmount(costId, cost) },
                duration,
                requiredResearchId,
                tier,
                effectSummary,
                minimumDuration: .1f,
                additionalRequirements)
        {
            CostId = costId;
            Cost = Math.Max(0, cost);
        }

        public ResearchDefinition(
            string id,
            string name,
            DevelopmentRoute route,
            IReadOnlyList<ResourceAmount> costs,
            float duration,
            string requiredResearchId = null,
            int tier = 1,
            string effectSummary = null,
            params string[] additionalRequirements)
            : this(
                id,
                name,
                route,
                costs,
                duration,
                requiredResearchId,
                tier,
                effectSummary,
                minimumDuration: 0f,
                additionalRequirements)
        {
        }

        private ResearchDefinition(
            string id,
            string name,
            DevelopmentRoute route,
            IReadOnlyList<ResourceAmount> costs,
            float duration,
            string requiredResearchId,
            int tier,
            string effectSummary,
            float minimumDuration,
            params string[] additionalRequirements)
        {
            Id = new StableId(id);
            Name = name;
            Route = route;
            var costSnapshot = new List<ResourceAmount>();
            if (costs != null)
            {
                for (int index = 0; index < costs.Count; index++)
                {
                    ResourceAmount value = costs[index];
                    if (!string.IsNullOrWhiteSpace(value.ResourceId) &&
                        value.Amount > 0)
                    {
                        costSnapshot.Add(value);
                    }
                }
            }
            Costs = new ReadOnlyCollection<ResourceAmount>(costSnapshot);
            CostId = Costs.Count == 0 ? null : Costs[0].ResourceId;
            Cost = Costs.Count == 0 ? 0 : Costs[0].Amount;
            Duration = Math.Max(minimumDuration, duration);
            RequiredResearchId = requiredResearchId;
            Tier = Math.Max(1, Math.Min(3, tier));
            EffectSummary = effectSummary ?? "规则效果待运行系统接入";
            var requirements = new List<string>();
            if (!string.IsNullOrEmpty(requiredResearchId))
                requirements.Add(requiredResearchId);
            if (additionalRequirements != null)
            {
                requirements.AddRange(additionalRequirements.Where(
                    value => !string.IsNullOrEmpty(value)));
            }
            RequiredResearchIds =
                new ReadOnlyCollection<string>(requirements);
        }
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

    public sealed class ResearchPersistenceSnapshot
    {
        internal ResearchPersistenceSnapshot(
            string[] completedResearchIds,
            string activeResearchId,
            float remainingSeconds)
        {
            CompletedResearchIds = Array.AsReadOnly(
                completedResearchIds ?? Array.Empty<string>());
            ActiveResearchId = activeResearchId;
            RemainingSeconds = remainingSeconds;
        }

        public IReadOnlyList<string> CompletedResearchIds { get; }
        public string ActiveResearchId { get; }
        public float RemainingSeconds { get; }
    }

    public sealed class ResearchRestorePlan
    {
        internal ResearchRestorePlan(
            ResearchModel owner,
            ulong preparedRevision,
            StableId[] completed,
            string[] orphanCompletedIds,
            ResearchDefinition active,
            string missingActiveResearchId,
            float remainingSeconds)
        {
            Owner = owner;
            PreparedRevision = preparedRevision;
            Completed = completed;
            OrphanCompletedIds = orphanCompletedIds;
            Active = active;
            MissingActiveResearchId = missingActiveResearchId;
            RemainingSeconds = remainingSeconds;
        }

        internal ResearchModel Owner { get; }
        internal ulong PreparedRevision { get; }
        internal StableId[] Completed { get; }
        internal string[] OrphanCompletedIds { get; }
        internal ResearchDefinition Active { get; }
        internal string MissingActiveResearchId { get; }
        internal float RemainingSeconds { get; }
        internal bool Committed { get; set; }
    }

    public sealed class ResearchModel
    {
        private readonly HashSet<StableId> completed = new HashSet<StableId>();
        private readonly SortedSet<string> orphanCompletedIds =
            new SortedSet<string>(StringComparer.Ordinal);
        private string missingActiveResearchId;
        private ulong persistenceRevision;
        public ResearchDefinition Active { get; private set; }
        public float Remaining { get; private set; }
        public int CompletedCount => completed.Count;
        public bool HasMissingActiveResearch =>
            !string.IsNullOrEmpty(missingActiveResearchId);
        public string MissingActiveResearchId => missingActiveResearchId;
        public event Action<ResearchDefinition> Completed;
        public bool Start(ResearchDefinition definition, ResourceInventory inventory, float inheritedProgressRatio = 0f)
        {
            if (Active != null || HasMissingActiveResearch ||
                definition == null || inventory == null ||
                completed.Contains(definition.Id) ||
                definition.RequiredResearchIds.Any(required =>
                    !completed.Any(id => id.Value == required)) ||
                !TrySpendResearchCosts(inventory, definition.Costs))
                return false;
            float ratio=Math.Max(0f,Math.Min(1f,inheritedProgressRatio));
            Active = definition; Remaining = Math.Max(.001f,definition.Duration*(1f-ratio));
            persistenceRevision++;
            return true;
        }
        public bool Start(
            ResearchDefinition definition,
            CityResourceStorageModel cityStorage,
            float inheritedProgressRatio = 0f)
        {
            if (Active != null || HasMissingActiveResearch ||
                definition == null || cityStorage == null ||
                completed.Contains(definition.Id) ||
                definition.RequiredResearchIds.Any(required =>
                    !completed.Any(id => id.Value == required)) ||
                !cityStorage.TryCommitBatch(
                    definition.Costs,
                    Array.Empty<ResourceAmount>()))
            {
                return false;
            }
            float ratio = Math.Max(0f, Math.Min(1f, inheritedProgressRatio));
            Active = definition;
            Remaining = Math.Max(.001f, definition.Duration * (1f - ratio));
            persistenceRevision++;
            return true;
        }
        public bool Tick(float delta)
        {
            if (Active == null) return false; Remaining -= Math.Max(0f, delta); if (Remaining > 0.0001f) return false;
            ResearchDefinition finished = Active; completed.Add(finished.Id); Active = null; Remaining = 0f; persistenceRevision++; Completed?.Invoke(finished); return true;
        }
        public bool IsCompleted(StableId id) => completed.Contains(id);
        public string[] CaptureCompleted()=>completed.Select(id=>id.Value)
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        public ResearchPersistenceSnapshot CaptureForPersistence()
        {
            var ids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (StableId id in completed) ids.Add(id.Value);
            foreach (string id in orphanCompletedIds) ids.Add(id);
            return new ResearchPersistenceSnapshot(
                ids.ToArray(),
                Active?.Id.Value ?? missingActiveResearchId,
                Active != null || HasMissingActiveResearch ? Remaining : 0f);
        }

        public bool TryPrepareRestoreForPersistence(
            IReadOnlyList<string> completedResearchIds,
            string activeResearchId,
            float remainingSeconds,
            Func<string, ResearchDefinition> knownDefinitionResolver,
            out ResearchRestorePlan plan,
            out string error)
        {
            plan = null;
            if (completedResearchIds == null ||
                knownDefinitionResolver == null)
            {
                error = "科技恢复数据或目录解析器不能为空";
                return false;
            }
            if (float.IsNaN(remainingSeconds) ||
                float.IsInfinity(remainingSeconds) ||
                remainingSeconds < 0f)
            {
                error = "活动科技剩余时间无效";
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var known = new List<StableId>();
            var unknown = new List<string>();
            for (var index = 0; index < completedResearchIds.Count; index++)
            {
                string id = completedResearchIds[index];
                if (!TryCreateStableId(id, out StableId stableId) ||
                    !seen.Add(id))
                {
                    error = "已完成科技 ID 无效或重复";
                    return false;
                }
                ResearchDefinition definition;
                try
                {
                    definition = knownDefinitionResolver(id);
                }
                catch
                {
                    error = "科技目录解析失败";
                    return false;
                }
                if (definition != null)
                {
                    if (!string.Equals(
                            definition.Id.Value,
                            id,
                            StringComparison.Ordinal))
                    {
                        error = "科技目录返回了不匹配的定义";
                        return false;
                    }
                    known.Add(stableId);
                }
                else
                {
                    unknown.Add(id);
                }
            }

            ResearchDefinition active = null;
            string missingActive = null;
            if (string.IsNullOrEmpty(activeResearchId))
            {
                if (remainingSeconds != 0f)
                {
                    error = "没有活动科技时剩余时间必须为零";
                    return false;
                }
            }
            else
            {
                if (!TryCreateStableId(activeResearchId, out _) ||
                    seen.Contains(activeResearchId))
                {
                    error = "活动科技 ID 无效或已经完成";
                    return false;
                }
                try
                {
                    active = knownDefinitionResolver(activeResearchId);
                }
                catch
                {
                    error = "科技目录解析失败";
                    return false;
                }
                if (active != null && !string.Equals(
                        active.Id.Value,
                        activeResearchId,
                        StringComparison.Ordinal))
                {
                    error = "科技目录返回了不匹配的活动定义";
                    return false;
                }
                if (active == null) missingActive = activeResearchId;
            }

            known.Sort((left, right) => string.CompareOrdinal(
                left.Value,
                right.Value));
            unknown.Sort(StringComparer.Ordinal);
            plan = new ResearchRestorePlan(
                this,
                persistenceRevision,
                known.ToArray(),
                unknown.ToArray(),
                active,
                missingActive,
                remainingSeconds);
            error = string.Empty;
            return true;
        }

        public bool TryCommitRestoreForPersistence(
            ResearchRestorePlan plan,
            out string error)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this))
            {
                error = "科技恢复计划不属于当前模型";
                return false;
            }
            if (plan.Committed)
            {
                error = "科技恢复计划已经提交";
                return false;
            }
            if (plan.PreparedRevision != persistenceRevision)
            {
                error = "科技状态已变化，请重新准备恢复计划";
                return false;
            }

            completed.Clear();
            for (var index = 0; index < plan.Completed.Length; index++)
                completed.Add(plan.Completed[index]);
            orphanCompletedIds.Clear();
            for (var index = 0;
                 index < plan.OrphanCompletedIds.Length;
                 index++)
            {
                orphanCompletedIds.Add(plan.OrphanCompletedIds[index]);
            }
            Active = plan.Active;
            missingActiveResearchId = plan.MissingActiveResearchId;
            Remaining = plan.RemainingSeconds;
            plan.Committed = true;
            persistenceRevision++;
            error = string.Empty;
            return true;
        }

        internal void GrantCompleted(ResearchDefinition definition)
        {
            if (definition != null && completed.Add(definition.Id))
            {
                orphanCompletedIds.Remove(definition.Id.Value);
                persistenceRevision++;
            }
        }

        public void GrantCompletedForDevelopment(
            ResearchDefinition definition)
        {
            GrantCompleted(definition);
        }
        internal bool TryCancel(
            ResourceInventory inventory,
            ResourceCapacityPolicy capacity,
            int activeWarehouseCount,
            float refundRatio)
        {
            if (Active == null || inventory == null || capacity == null)
                return false;
            ResourceAmount[] refund = Active.Costs
                .Select(value => new ResourceAmount(
                    value.ResourceId,
                    (int)Math.Floor(
                        value.Amount * Math.Max(0f, refundRatio))))
                .Where(value => value.Amount > 0)
                .ToArray();
            if (refund.Length > 0 &&
                !ResourceTransaction.TryCommitBatch(
                    inventory,
                    Array.Empty<ResourceAmount>(),
                    inventory,
                    capacity,
                    activeWarehouseCount,
                    refund))
            {
                return false;
            }
            Active = null;
            Remaining = 0f;
            persistenceRevision++;
            return true;
        }

        internal bool TryCancel(
            CityResourceStorageModel cityStorage,
            float refundRatio)
        {
            if (Active == null || cityStorage == null) return false;
            ResourceAmount[] refund = Active.Costs
                .Select(value => new ResourceAmount(
                    value.ResourceId,
                    (int)Math.Floor(
                        value.Amount * Math.Max(0f, refundRatio))))
                .Where(value => value.Amount > 0)
                .ToArray();
            if (refund.Length > 0 && !cityStorage.TryCommitBatch(
                    Array.Empty<ResourceAmount>(),
                    refund))
            {
                return false;
            }
            Active = null;
            Remaining = 0f;
            persistenceRevision++;
            return true;
        }

        private static bool TrySpendResearchCosts(
            ResourceInventory inventory,
            IReadOnlyList<ResourceAmount> costs)
        {
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            if (costs != null)
            {
                for (int index = 0; index < costs.Count; index++)
                {
                    ResourceAmount cost = costs[index];
                    if (!ResourceCapacityPolicy.IsRegisteredResource(
                            cost.ResourceId) ||
                        cost.Amount <= 0)
                    {
                        return false;
                    }

                    totals.TryGetValue(cost.ResourceId, out int existing);
                    long aggregate = (long)existing + cost.Amount;
                    if (aggregate > int.MaxValue) return false;
                    totals[cost.ResourceId] = (int)aggregate;
                }
            }

            foreach (KeyValuePair<string, int> cost in totals)
                if (!inventory.CanSpend(cost.Key, cost.Value))
                    return false;

            var before = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string resourceId in totals.Keys)
                before[resourceId] = inventory.Get(resourceId);

            try
            {
                foreach (KeyValuePair<string, int> cost in totals)
                {
                    if (!inventory.TrySpend(cost.Key, cost.Value))
                    {
                        RestoreResearchCosts(inventory, before);
                        return false;
                    }
                }
                return true;
            }
            catch
            {
                RestoreResearchCosts(inventory, before);
                return false;
            }
        }

        private static void RestoreResearchCosts(
            ResourceInventory inventory,
            Dictionary<string, int> before)
        {
            foreach (KeyValuePair<string, int> value in before)
                inventory.Restore(value.Key, value.Value);
        }

        public void Restore(string[] completedIds,string activeId,float remaining)
        {
            completed.Clear();if(completedIds!=null)foreach(string id in completedIds){var definition=ResearchCatalog.Find(id);if(definition!=null)completed.Add(definition.Id);}
            orphanCompletedIds.Clear();missingActiveResearchId=null;
            Active=ResearchCatalog.Find(activeId);Remaining=Active==null?0f:Math.Max(0.001f,Math.Min(Active.Duration,remaining));persistenceRevision++;
        }

        private static bool TryCreateStableId(
            string value,
            out StableId stableId)
        {
            try
            {
                stableId = new StableId(value);
                return true;
            }
            catch (ArgumentException)
            {
                stableId = default;
                return false;
            }
        }
    }
}
