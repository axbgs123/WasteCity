using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxUiInputGuard3D
    {
        private readonly List<RaycastResult> raycastResults =
            new List<RaycastResult>();
        private GraphicRaycaster[] raycasters;
        private int escapeConsumedFrame = -1;

        public bool HasKeyboardFocus(EventSystem eventSystem)
        {
            if (eventSystem == null) return false;
            if (Time.frameCount <= escapeConsumedFrame) return true;

            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy) return false;
            Selectable selectable = selected.GetComponentInParent<Selectable>();
            return selectable != null &&
                   selectable.IsActive() &&
                   selectable.IsInteractable();
        }

        public bool IsPointerOverUi(
            EventSystem eventSystem,
            Vector2 screenPosition)
        {
            if (eventSystem == null) return false;
            var pointer = new PointerEventData(eventSystem)
            {
                position = screenPosition
            };
            raycastResults.Clear();
            eventSystem.RaycastAll(pointer, raycastResults);
            bool hit = false;
            for (var index = 0; index < raycastResults.Count; index++)
            {
                GraphicRaycaster raycaster =
                    raycastResults[index].module as GraphicRaycaster;
                if (raycaster == null || !raycaster.isActiveAndEnabled)
                    continue;
                Canvas canvas = raycaster.GetComponent<Canvas>();
                if (canvas == null || !canvas.isActiveAndEnabled)
                    continue;
                hit = true;
                break;
            }
            raycastResults.Clear();
            if (hit) return true;

            if (raycasters == null || raycasters.Length == 0)
                raycasters =
                    UnityEngine.Object.FindObjectsOfType<GraphicRaycaster>();
            for (var raycasterIndex = 0;
                 raycasterIndex < raycasters.Length;
                 raycasterIndex++)
            {
                GraphicRaycaster raycaster = raycasters[raycasterIndex];
                if (raycaster == null || !raycaster.isActiveAndEnabled)
                    continue;
                Canvas canvas = raycaster.GetComponent<Canvas>();
                if (canvas == null ||
                    !canvas.isActiveAndEnabled)
                    continue;
                IList<Graphic> graphics =
                    GraphicRegistry.GetGraphicsForCanvas(canvas);
                for (var graphicIndex = 0;
                     graphicIndex < graphics.Count;
                     graphicIndex++)
                {
                    Graphic graphic = graphics[graphicIndex];
                    if (!graphic.raycastTarget ||
                        !graphic.isActiveAndEnabled)
                        continue;
                    Camera eventCamera =
                        canvas.renderMode == RenderMode.ScreenSpaceOverlay
                            ? null
                            : canvas.worldCamera;
                    if (RectTransformUtility.RectangleContainsScreenPoint(
                            graphic.rectTransform,
                            screenPosition,
                            eventCamera) &&
                        graphic.Raycast(screenPosition, eventCamera))
                        return true;
                }
            }
            return false;
        }

        public bool ConsumeFocusedEscape(EventSystem eventSystem)
        {
            if (!HasKeyboardFocus(eventSystem)) return false;

            GameObject selected = eventSystem.currentSelectedGameObject;
            InputField input = selected == null
                ? null
                : selected.GetComponentInParent<InputField>();
            if (input != null)
                input.DeactivateInputField();
            eventSystem.SetSelectedGameObject(null);
            escapeConsumedFrame = Time.frameCount;
            return true;
        }
    }
}
