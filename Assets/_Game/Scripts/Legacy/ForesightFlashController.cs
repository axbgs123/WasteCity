using UnityEngine;
using WasteCity.Core;
using WasteCity.World;

namespace WasteCity.Legacy
{
    public sealed class ForesightFlashController : MonoBehaviour
    {
        [SerializeField] private LegacySelectionController legacy;
        [SerializeField] private FormalGameClockController clock;
        public ForesightFlashModel Model { get; private set; }
        public string CurrentFragment { get; private set; }
        private float displayRemaining;
        private static readonly string[] Fragments = { "命运碎片：红色目标正在迷雾外集结。", "命运碎片：某段城墙在未来留下灼痕。", "命运碎片：废墟中的呼救声突然中断。", "命运碎片：库存数字跌破零，警报却没有响。" };
        private void Start() => Model = new ForesightFlashModel(new WorldSeed(8128), clock.Model.SecondsPerDay);
        private void Update()
        {
            if (legacy?.Model?.Selected?.Id.Value == "core.legacy.foresight-delay" && Model.TryFlash(clock.Model.Day, clock.Model.SecondsIntoDay)) { CurrentFragment = Fragments[new WorldSeed(8128).Sample(clock.Model.Day, 5, 912) % Fragments.Length]; displayRemaining = 3f; }
            displayRemaining = Mathf.Max(0f, displayRemaining - Time.deltaTime);
        }
        public void Restore(int lastFlashedDay) => Model?.Restore(lastFlashedDay);
        private void OnGUI() { if (displayRemaining > 0f) GUI.Box(new Rect(Screen.width * .2f, Screen.height * .38f, Screen.width * .6f, 70f), $"预知迟滞 · 3 秒闪现\n{CurrentFragment}"); }
    }
}
