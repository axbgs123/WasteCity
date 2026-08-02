using System;

namespace WasteCity.Progression
{
    public enum AdvancementSequenceStage { None, Scanning, Confirmed, Warning, Results, Continued }

    public sealed class AdvancementSequenceModel
    {
        public AdvancementSequenceStage Stage { get; private set; }
        public float Remaining { get; private set; }
        public bool IsPresenting => Stage >= AdvancementSequenceStage.Scanning && Stage <= AdvancementSequenceStage.Results;
        public event Action<AdvancementSequenceStage> Changed;

        public bool Start()
        {
            if (Stage != AdvancementSequenceStage.None) return false;
            Set(AdvancementSequenceStage.Scanning, 2.5f); return true;
        }

        public void Tick(float delta)
        {
            if (delta <= 0f || Stage == AdvancementSequenceStage.None || Stage >= AdvancementSequenceStage.Results) return;
            float carry = delta;
            while (carry > 0f && Stage < AdvancementSequenceStage.Results)
            {
                if (carry < Remaining) { Remaining -= carry; return; }
                carry -= Remaining;
                if (Stage == AdvancementSequenceStage.Scanning) Set(AdvancementSequenceStage.Confirmed, 3f);
                else if (Stage == AdvancementSequenceStage.Confirmed) Set(AdvancementSequenceStage.Warning, 4f);
                else Set(AdvancementSequenceStage.Results, 0f);
            }
        }

        public bool Continue()
        {
            if (Stage != AdvancementSequenceStage.Results) return false;
            Set(AdvancementSequenceStage.Continued, 0f); return true;
        }

        public void Restore(int stage, float remaining)
        {
            Stage = Enum.IsDefined(typeof(AdvancementSequenceStage), stage) ? (AdvancementSequenceStage)stage : AdvancementSequenceStage.None;
            Remaining = Math.Max(0f, remaining); Changed?.Invoke(Stage);
        }

        private void Set(AdvancementSequenceStage stage, float remaining)
        { Stage = stage; Remaining = remaining; Changed?.Invoke(stage); }
    }
}
