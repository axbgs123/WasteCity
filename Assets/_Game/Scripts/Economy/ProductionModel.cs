using System;
using System.Collections.Generic;

namespace WasteCity.Economy
{
    public sealed class ProductionRecipe
    {
        public string InputId { get; } public int InputAmount { get; } public string OutputId { get; } public int OutputAmount { get; } public float Duration { get; }
        public ProductionRecipe(string inputId,int input,string outputId,int output,float duration){InputId=inputId;InputAmount=Math.Max(0,input);OutputId=outputId;OutputAmount=Math.Max(1,output);Duration=Math.Max(.1f,duration);}
    }
    public sealed class ProductionProcess
    {
        private readonly ProductionRecipe recipe; private float progress;
        public ProductionProcess(ProductionRecipe recipe)=>this.recipe=recipe;
        public int Tick(float delta, ResourceInventory inventory, int buildingCount)
        {
            if(buildingCount<=0)return 0; progress+=Math.Max(0,delta)*buildingCount;int cycles=0;
            while(progress>=recipe.Duration && inventory.Get(recipe.InputId)>=recipe.InputAmount)
            {if(!inventory.TrySpend(recipe.InputId,recipe.InputAmount))break;int accepted=inventory.Add(recipe.OutputId,recipe.OutputAmount);if(accepted<recipe.OutputAmount){inventory.Add(recipe.InputId,recipe.InputAmount);break;}progress-=recipe.Duration;cycles++;}
            return cycles;
        }
    }
}
