using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WasteCity.Graybox3D.Building
{
    /// <summary>
    /// Event-driven input surface for the runtime research graph. It forwards
    /// only viewport gestures and never polls devices per frame.
    /// </summary>
    public sealed class GrayboxResearchTreeViewportInput3D :
        MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IInitializePotentialDragHandler,
        IScrollHandler
    {
        private Action<Vector2> panRequested;
        private Action<Vector2, float, Camera> zoomRequested;
        private bool dragAccepted;

        public void Configure(
            Action<Vector2> panCallback,
            Action<Vector2, float, Camera> zoomCallback)
        {
            panRequested = panCallback;
            zoomRequested = zoomCallback;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData == null ||
                eventData.button != PointerEventData.InputButton.Left &&
                eventData.button != PointerEventData.InputButton.Middle)
            {
                return;
            }

            if (!dragAccepted &&
                eventData.pointerPressRaycast.gameObject != null)
                return;
            panRequested?.Invoke(eventData.delta);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData != null)
                eventData.useDragThreshold = true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragAccepted = AcceptsDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            dragAccepted = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (eventData == null ||
                Mathf.Approximately(eventData.scrollDelta.y, 0f))
            {
                return;
            }
            zoomRequested?.Invoke(
                eventData.position,
                eventData.scrollDelta.y,
                eventData.pressEventCamera);
        }

        private void OnDestroy()
        {
            panRequested = null;
            zoomRequested = null;
        }

        private bool AcceptsDrag(PointerEventData eventData)
        {
            if (eventData == null ||
                eventData.button != PointerEventData.InputButton.Left &&
                eventData.button != PointerEventData.InputButton.Middle)
            {
                return false;
            }
            GameObject pressed = eventData.pointerPressRaycast.gameObject;
            return eventData.button == PointerEventData.InputButton.Middle ||
                pressed == null || pressed == gameObject;
        }
    }
}
