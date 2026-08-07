using System;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace PixelRoad.UI
{
    public sealed class PixelRoadMapInput : MonoBehaviour, IDragHandler, IScrollHandler
    {
        private const float ScrollZoomInFactor = 1.25f;
        private const float ScrollZoomOutFactor = 1f / ScrollZoomInFactor;

        public event Action<Vector2> Dragged;
        public event Action<float, Vector2> Zoomed;

        private float previousPinchDistance;

        public void OnDrag(PointerEventData eventData)
        {
            Dragged?.Invoke(eventData.delta);
        }

        public void OnScroll(PointerEventData eventData)
        {
            float zoomFactor = ScrollDeltaToZoomFactor(eventData.scrollDelta.y);
            if (Mathf.Approximately(zoomFactor, 1f))
            {
                return;
            }

            Zoomed?.Invoke(zoomFactor, eventData.position);
        }

        public static float ScrollDeltaToZoomFactor(float scrollDeltaY)
        {
            if (scrollDeltaY > 0f)
            {
                return ScrollZoomInFactor;
            }

            if (scrollDeltaY < 0f)
            {
                return ScrollZoomOutFactor;
            }

            return 1f;
        }

        private void Update()
        {
            HandlePinchZoom();
        }

        private void HandlePinchZoom()
        {
#if ENABLE_INPUT_SYSTEM
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                previousPinchDistance = 0f;
                return;
            }

            int activeCount = 0;
            Vector2 first = Vector2.zero;
            Vector2 second = Vector2.zero;
            foreach (TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.isPressed)
                {
                    continue;
                }

                if (activeCount == 0)
                {
                    first = touch.position.ReadValue();
                }
                else if (activeCount == 1)
                {
                    second = touch.position.ReadValue();
                }

                activeCount++;
                if (activeCount >= 2)
                {
                    break;
                }
            }

            if (activeCount < 2)
            {
                previousPinchDistance = 0f;
                return;
            }

            float distance = Vector2.Distance(first, second);
            if (previousPinchDistance > 0f)
            {
                float zoomFactor = Mathf.Clamp(distance / previousPinchDistance, 0.85f, 1.15f);
                Zoomed?.Invoke(zoomFactor, (first + second) * 0.5f);
            }

            previousPinchDistance = distance;
#else
            if (Input.touchCount < 2)
            {
                previousPinchDistance = 0f;
                return;
            }

            Touch firstTouch = Input.GetTouch(0);
            Touch secondTouch = Input.GetTouch(1);
            float distance = Vector2.Distance(firstTouch.position, secondTouch.position);
            if (previousPinchDistance > 0f)
            {
                float zoomFactor = Mathf.Clamp(distance / previousPinchDistance, 0.85f, 1.15f);
                Zoomed?.Invoke(zoomFactor, (firstTouch.position + secondTouch.position) * 0.5f);
            }

            previousPinchDistance = distance;
#endif
        }
    }
}
