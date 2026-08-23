using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WasteCity.Graybox3D.Building
{
    /// <summary>
    /// Reports EventSystem focus for the generated research search field.
    /// </summary>
    public sealed class GrayboxResearchSearchFocus3D :
        MonoBehaviour,
        ISelectHandler,
        IDeselectHandler
    {
        private Action<bool> changed;

        public void Configure(Action<bool> callback)
        {
            changed = callback;
        }

        public void OnSelect(BaseEventData eventData)
        {
            changed?.Invoke(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            changed?.Invoke(false);
        }

        private void OnDestroy()
        {
            changed = null;
        }
    }
}
