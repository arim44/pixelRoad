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

        /// <summary>손을 뗀 지점이 허용 반경 안이면 탭으로 확정한다. 그 밖은 지도 팬으로 보고 무시한다.</summary>
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

        /// <summary>비어 있지만, 이 핸들러가 있어야 마커가 드래그 대상이 되어 pointerPress를 뺏기지 않는다.</summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        /// <summary>허용 반경을 넘으면 탭을 취소하고, 이동량은 지도로 넘겨 팬이 끊기지 않게 한다.</summary>
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

        /// <summary>드래그 인터페이스를 갖추기 위한 빈 구현. 정리할 상태가 없다.</summary>
        public void OnEndDrag(PointerEventData eventData)
        {
        }

        /// <summary>마커 위에서 굴린 휠도 지도 줌으로 넘겨 마커가 줌을 막지 않게 한다.</summary>
        public void OnScroll(PointerEventData eventData)
        {
            if (mapInput != null)
            {
                mapInput.ForwardScroll(eventData.scrollDelta, eventData.position);
            }
        }

        /// <summary>누른 지점에서 탭 허용 반경을 벗어났는지 판단한다.</summary>
        private bool MovedBeyondTapSlop(Vector2 position)
        {
            return (position - pressPosition).sqrMagnitude > tapSlopSquared;
        }
    }
}
