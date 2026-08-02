using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.Economy;
using WasteCity.Building;
using WasteCity.City;
using System.Text;
using System;

namespace WasteCity.Research
{
    public sealed class ResearchController : MonoBehaviour
    {
        [SerializeField] private FormalEconomyController economy;
        [SerializeField] private PlaceholderBuildingController buildings;
        [SerializeField] private PlaceholderMobileCity city;
        public ResearchModel Model { get; private set; } = new ResearchModel();
        public float TurretRangeMultiplier=>Model.IsCompleted(new Content.StableId("core.research.ballistics"))?1.2f:1f;
        public float TurretDamageMultiplier=>Model.IsCompleted(new Content.StableId("core.research.ballistics"))?1.15f:1f;
        public bool HasMetabolicAcceleration => Model.IsCompleted(new Content.StableId("core.research.metabolic-acceleration"));
        public bool HasCarapaceGrowth => Model.IsCompleted(new Content.StableId("core.research.carapace-growth"));
        public bool HasTissueRegeneration => Model.IsCompleted(new Content.StableId("core.research.tissue-regeneration"));
        public bool HasPrecognitiveSense => Model.IsCompleted(new Content.StableId("core.research.precognitive-sense"));
        private bool visible;
        private int selectedIndex;
        private void Update()
        {
            float researchSpeed=Model.IsCompleted(new Content.StableId("core.research.thought-acceleration"))?1.25f:1f;if(city.LongWorkAllowed)Model.Tick(Time.deltaTime*researchSpeed);
            if (Keyboard.current == null) return;
            if (Keyboard.current.kKey.wasPressedThisFrame) visible = !visible;
            if (!visible) return;
            if (buildings.CompletedCount("core.building.research-station") <= 0) return;
            if(!city.LongWorkAllowed)return;
            if(Keyboard.current.upArrowKey.wasPressedThisFrame)selectedIndex=Math.Max(0,selectedIndex-1);
            if(Keyboard.current.downArrowKey.wasPressedThisFrame)selectedIndex=Math.Min(ResearchCatalog.All.Length-1,selectedIndex+1);
            if(Keyboard.current.enterKey.wasPressedThisFrame||Keyboard.current.numpadEnterKey.wasPressedThisFrame)Model.Start(ResearchCatalog.All[selectedIndex],economy.Inventory);
            if (Keyboard.current.digit5Key.wasPressedThisFrame) Model.Start(ResearchCatalog.Starting[0], economy.Inventory);
            if (Keyboard.current.digit6Key.wasPressedThisFrame) Model.Start(ResearchCatalog.Starting[1], economy.Inventory);
            if (Keyboard.current.digit7Key.wasPressedThisFrame) Model.Start(ResearchCatalog.Starting[2], economy.Inventory);
            if (Keyboard.current.digit8Key.wasPressedThisFrame) Model.Start(ResearchCatalog.Starting[3], economy.Inventory);
            if (Keyboard.current.digit9Key.wasPressedThisFrame) Model.Start(ResearchCatalog.Starting[4], economy.Inventory);
        }
        private void OnGUI()
        {
            if (!visible) return;
            string status = buildings.CompletedCount("core.building.research-station")<=0?"锁定：需要已完成研究站":!city.LongWorkAllowed?"暂停：长周期研究仅在堡垒态推进":Model.Active == null ? $"已完成 {Model.CompletedCount}" : $"研究中：{Model.Active.Name} · {Model.Remaining:0.0}s";
            var text=new StringBuilder("正式科技树 [K] · ↑↓选择 · Enter研究\n").Append(status).Append("\n\n");int first=Math.Max(0,Math.Min(selectedIndex-3,ResearchCatalog.All.Length-7));for(int i=first;i<Math.Min(first+7,ResearchCatalog.All.Length);i++){var node=ResearchCatalog.All[i];bool done=Model.IsCompleted(node.Id);bool blocked=node.RequiredResearchIds.Count>0&&System.Linq.Enumerable.Any(node.RequiredResearchIds,id=>!Model.IsCompleted(new Content.StableId(id)));text.Append(i==selectedIndex?"▶ ":"  ").Append(done?"✓ ":blocked?"🔒 ":"○ ").Append($"T{node.Tier} {node.Name} [{RouteName(node.Route)}] · {node.Cost}{ResourceName(node.CostId)}\n");}var selected=ResearchCatalog.All[selectedIndex];text.Append($"\n{selected.EffectSummary}");GUI.Box(new Rect(Screen.width-570f,Screen.height-390f,550f,365f),text.ToString());
        }
        private static string RouteName(DevelopmentRoute route)=>route==DevelopmentRoute.Technology?"科技":route==DevelopmentRoute.Cultivation?"修仙":route==DevelopmentRoute.BiologicalAscension?"血肉":"灵能";
        private static string ResourceName(string id)=>id==ResourceIds.Iron?"铁":id==ResourceIds.Alloy?"合金":id==ResourceIds.EnergyCrystal?"能晶":id==ResourceIds.Biomass?"生物质":"水";
    }
}
