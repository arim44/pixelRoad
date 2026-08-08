using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PixelRoad.UI
{
    /// <summary>
    /// 지도 마커 전용 탭 판정 컴포넌트.
    ///
    /// uGUI 기본 Button을 마커에 붙이면 모바일에서 선택이 되지 않는다.
    /// 마커의 pointerPress는 마커 자신이지만 pointerDrag는 상위 뷰포트(지도 팬 처리)로 잡히는데,
    /// InputSystemUIInputModule.ProcessPointerButtonDrag는 두 값이 다르면
    /// 드래그 임계값(EventSystem.pixelDragThreshold, 기본 10px)을 넘는 순간
    /// eligibleForClick을 false로 만들고 pointerPress를 끊어버린다.
    /// 마우스는 클릭 중 커서가 멈춰 있어 살아남지만, 손가락은 고DPI 화면에서
    /// 10px(약 0.6mm)를 거의 항상 넘기므로 마커 탭이 통째로 사라진다.
    ///
    /// 이 컴포넌트는 마커가 직접 IDragHandler를 구현해 pointerPress == pointerDrag를 만들고,
    /// 드래그/스크롤은 지도 입력으로 그대로 넘겨 팬·줌을 유지하면서
    /// 이동량이 탭 허용 반경 안일 때만 <see cref="Tapped"/>를 발생시킨다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class MapMarkerTapTarget : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IScrollHandler
    {
        /// <summary>탭으로 인정할 최대 이동 거리(인치). 손떨림 여유를 약 2mm로 잡는다.</summary>
        private const float TapSlopInches = 0.08f;
        private const float MinimumTapSlopPixels = 14f;
        private const float MaximumTapSlopPixels = 56f;

        private PixelRoadMapInput mapInput;
        private float tapSlopSquared;
        private Vector2 pressPosition;
        private bool pressed;
        private bool tapCancelled;

        /// <summary>탭(짧은 터치·클릭)이 확정되었을 때 발생한다.</summary>
        public event Action Tapped;

        /// <summary>드래그와 휠 입력을 넘겨줄 지도 입력 컴포넌트를 연결한다.</summary>
        public void Initialize(PixelRoadMapInput input)
        {
            mapInput = input;
        }

        private void Awake()
        {
            float slop = Mathf.Clamp(
                Screen.dpi > 1f ? Screen.dpi * TapSlopInches : MinimumTapSlopPixels,
                MinimumTapSlopPixels,
                MaximumTapSlopPixels);
            tapSlopSquared = slop * slop;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            tapCancelled = false;
            pressPosition = eventData.position;
        }

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

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (pressed && MovedBeyondTapSlop(eventData.position))
            {
                tapCancelled = true;
            }

            if (mapInput != null)
            {
                mapInput.ForwardDrag(eventData.delta);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (mapInput != null)
            {
                mapInput.ForwardScroll(eventData.scrollDelta, eventData.position);
            }
        }

        private bool MovedBeyondTapSlop(Vector2 position)
        {
            return (position - pressPosition).sqrMagnitude > tapSlopSquared;
        }
    }
}
