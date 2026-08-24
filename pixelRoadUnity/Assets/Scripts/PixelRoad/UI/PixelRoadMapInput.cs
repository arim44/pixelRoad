using System;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace PixelRoad.UI
{
    /// <summary>
    /// 지도 뷰포트에 붙어 터치·마우스 입력을 팬·줌·탭으로 해석해 이벤트로 알린다.
    /// 실제 지도 이동은 이 이벤트를 구독하는 쪽이 처리한다.
    /// </summary>
    public sealed class PixelRoadMapInput : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IDragHandler,
        IScrollHandler
    {
        private const float ScrollZoomInFactor = 1.25f;
        private const float ScrollZoomOutFactor = 1f / ScrollZoomInFactor;

        /// <summary>탭으로 인정할 최대 이동 거리(인치). 마커 탭 판정과 같은 기준을 쓴다.</summary>
        private const float TapSlopInches = 0.08f;
        private const float MinimumTapSlopPixels = 14f;
        private const float MaximumTapSlopPixels = 56f;

        /// <summary>지도를 끌었을 때 화면 좌표 이동량과 함께 발생한다.</summary>
        public event Action<Vector2> Dragged;

        /// <summary>줌 배율과 기준이 될 화면 좌표를 함께 전달한다. 휠과 핀치 양쪽에서 발생한다.</summary>
        public event Action<float, Vector2> Zoomed;

        /// <summary>
        /// 지도 빈 곳을 탭했을 때 발생한다. 팬 드래그와 구분하기 위해 이동량이 탭 허용 반경 안일 때만 발생한다.
        /// 마커는 자체 탭 타깃이 이벤트를 먼저 소비하므로 여기로 오지 않는다.
        /// </summary>
        public event Action Tapped;

        private float previousPinchDistance;
        private float tapSlopSquared;
        private Vector2 pressPosition;
        private bool pressed;
        private bool tapCancelled;

        /// <summary>화면 DPI에 맞춰 탭 허용 반경을 픽셀로 환산해 둔다. 매번 계산하지 않도록 제곱값으로 보관한다.</summary>
        private void Awake()
        {
            float slop = Mathf.Clamp(
                Screen.dpi > 1f ? Screen.dpi * TapSlopInches : MinimumTapSlopPixels,
                MinimumTapSlopPixels,
                MaximumTapSlopPixels);
            tapSlopSquared = slop * slop;
        }

        /// <summary>누른 지점을 기록해 탭 판정의 기준으로 삼는다.</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            tapCancelled = false;
            pressPosition = eventData.position;
        }

        /// <summary>손을 뗀 지점이 허용 반경 안이면 탭으로 확정한다. 팬으로 판정된 입력은 무시한다.</summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pressed)
            {
                return;
            }

            pressed = false;
            if (tapCancelled || MovedBeyondTapSlop(eventData.position))
            {
                return;
            }

            Tapped?.Invoke();
        }

        /// <summary>허용 반경을 넘으면 탭을 취소하고, 이동량은 팬으로 넘긴다.</summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (pressed && MovedBeyondTapSlop(eventData.position))
            {
                tapCancelled = true;
            }

            ForwardDrag(eventData.delta);
        }

        /// <summary>누른 지점에서 탭 허용 반경을 벗어났는지 판단한다.</summary>
        private bool MovedBeyondTapSlop(Vector2 position)
        {
            return (position - pressPosition).sqrMagnitude > tapSlopSquared;
        }

        /// <summary>마우스 휠 입력을 줌으로 넘긴다.</summary>
        public void OnScroll(PointerEventData eventData)
        {
            ForwardScroll(eventData.scrollDelta, eventData.position);
        }

        /// <summary>
        /// 마커처럼 자체 IDragHandler를 가진 자식이 가로챈 드래그를 지도로 넘길 때 사용한다.
        /// </summary>
        public void ForwardDrag(Vector2 delta)
        {
            Dragged?.Invoke(delta);
        }

        /// <summary>
        /// 마커처럼 자체 IScrollHandler를 가진 자식이 가로챈 휠 입력을 지도로 넘길 때 사용한다.
        /// </summary>
        public void ForwardScroll(Vector2 scrollDelta, Vector2 screenPosition)
        {
            float zoomFactor = ScrollDeltaToZoomFactor(scrollDelta.y);
            if (Mathf.Approximately(zoomFactor, 1f))
            {
                return;
            }

            Zoomed?.Invoke(zoomFactor, screenPosition);
        }

        /// <summary>휠 방향을 확대/축소 배율로 바꾼다. 움직임이 없으면 1을 돌려 줌을 건너뛰게 한다.</summary>
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

        /// <summary>핀치는 포인터 이벤트로 오지 않으므로 매 프레임 터치 상태를 직접 확인한다.</summary>
        private void Update()
        {
            HandlePinchZoom();
        }

        /// <summary>
        /// 두 손가락 간격의 변화로 줌 배율을 만들어 두 손가락 중점을 기준으로 확대·축소한다.
        /// 새 입력 시스템과 구 Input 양쪽을 지원하고, 손가락이 둘 미만이면 기준 간격을 초기화한다.
        /// </summary>
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
