using UnityEngine;

namespace WasteCity.Legacy
{
    public static class RewindAnchorRules
    {
        public static float AttentionAfterLoad(float current) =>
            Mathf.Min(100f, Mathf.Max(0f, current) + 3f);
    }
}
