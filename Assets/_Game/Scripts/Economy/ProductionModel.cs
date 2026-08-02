using System;
using System.Collections.Generic;

namespace WasteCity.Economy
{
    public enum ProductionStatus { Running, NoBuildings, MissingInput, OutputFull }

    public sealed class ProductionRecipe
    {
        public string InputId { get; } public int InputAmount { get; } public string OutputId { get; } public int OutputAmount { get; } public float Duration { get; }
        public ProductionRecipe(string inputId,int input,string outputId,int output,float duration){InputId=inputId;InputAmount=Math.Max(0,input);OutputId=outputId;OutputAmount=Math.Max(1,output);Duration=Math.Max(.1f,duration);}
    }
    public sealed class ProductionProcess
    {
        private readonly ProductionRecipe recipe; private float progress;
        public float Progress => progress;
        public float ProgressNormalized => Math.Min(1f, progress / recipe.Duration);
        public ProductionStatus Status { get; private set; } = ProductionStatus.NoBuildings;
        public ProductionProcess(ProductionRecipe recipe)=>this.recipe=recipe;
        public int Tick(float delta, ResourceInventory inventory, int buildingCount)
        {
            if(buildingCount<=0){Status=ProductionStatus.NoBuildings;return 0;} progress+=Math.Max(0,delta)*buildingCount;int cycles=0;Status=ProductionStatus.Running;
            while(progress>=recipe.Duration)
            {
                if(inventory.Get(recipe.InputId)<recipe.InputAmount){Status=ProductionStatus.MissingInput;break;}
                if(inventory.Get(recipe.OutputId)+recipe.OutputAmount>inventory.CapacityPerResource){Status=ProductionStatus.OutputFull;break;}
                if(!inventory.TrySpend(recipe.InputId,recipe.InputAmount))break;
                inventory.Add(recipe.OutputId,recipe.OutputAmount);progress-=recipe.Duration;cycles++;
            }
            return cycles;
        }
        public void Restore(float savedProgress)=>progress=Math.Max(0f,Math.Min(recipe.Duration,savedProgress));
    }
}
