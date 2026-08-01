using UnityEngine;

namespace WasteCity.UI
{
    public sealed class FormalPlaceholderHud : MonoBehaviour
    {
        private void OnGUI()
        {
            GUI.Box(new Rect(18, 18, 430, 120), "废土移动城市 · 正式版技术原型\n世界种子 8128 · 一级行星文明\nWASD 驾驶占位城市\n所有视觉为待替换建模占位符");
        }
    }
}
