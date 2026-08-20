using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelRoad.AR
{
    /// <summary>
    /// 에디터 전용 나침반 시뮬레이션. 실기기에서는 FusedHeadingProvider가 AR 카메라 회전(ARCore가
    /// 갱신)을 그대로 방위각으로 쓰는데, 에디터에는 그 회전을 만들어 줄 실기기 센서가 없다. 그래서
    /// 여기서 직접 AR 카메라 Transform을 돌려 같은 경로(피치는 ARSceneController가 카메라 Transform에서
    /// 바로 읽음)를 그대로 태운다.
    ///
    /// 오른쪽 마우스 버튼을 누른 채 드래그하면 좌우(요)·상하(피치)가 함께 돌아간다 - 왼쪽 버튼은 랜드마크
    /// 핀 탭 등 UI 조작에 그대로 쓸 수 있도록 비워 둔다. Q/E 키로도 좌우 회전은 계속 가능하다.
    /// </summary>
    public sealed class SimulatedHeadingProvider : IHeadingProvider
    {
        private const float KeyRotateDegreesPerSecond = 90f;
        private const float MouseSensitivity = 0.05f;
        private const float MaxPitchDegrees = 80f;

        private readonly Transform cameraTransform;
        private Vector2 lastMousePosition;
        private bool hasLastMousePosition;

        public float HeadingDegrees { get; private set; }
        public bool IsAvailable { get { return true; } }

        public SimulatedHeadingProvider(Transform cameraTransform)
        {
            this.cameraTransform = cameraTransform;
        }

        public void Start()
        {
            HeadingDegrees = cameraTransform.eulerAngles.y;
        }

        public void Tick(float deltaTime)
        {
            float yaw = cameraTransform.eulerAngles.y + ReadRotateKeyInput() * KeyRotateDegreesPerSecond * deltaTime;
            float pitch = NormalizeSigned(cameraTransform.eulerAngles.x);

            if (IsDragging())
            {
                // Mouse.delta는 마우스 하드웨어가 보고하는 raw 카운트라 DPI에 따라 화면 픽셀 이동량과
                // 크게 어긋날 수 있다(고DPI 마우스에서는 감도를 아무리 낮춰도 여전히 빠르게 느껴짐).
                // 그래서 커서의 화면 좌표를 직접 프레임마다 비교해 실제 픽셀 이동량으로 계산한다.
                Vector2 mousePosition = ReadMousePosition();
                if (hasLastMousePosition)
                {
                    Vector2 dragDelta = mousePosition - lastMousePosition;
                    yaw += dragDelta.x * MouseSensitivity;
                    pitch = Mathf.Clamp(pitch - dragDelta.y * MouseSensitivity, -MaxPitchDegrees, MaxPitchDegrees);
                }

                lastMousePosition = mousePosition;
                hasLastMousePosition = true;
            }
            else
            {
                hasLastMousePosition = false;
            }

            // Z(롤)는 항상 0으로 고정한다 - 폰을 옆으로 기울이는 상황은 시뮬레이션 대상이 아니다.
            cameraTransform.eulerAngles = new Vector3(pitch, yaw, 0f);
            HeadingDegrees = cameraTransform.eulerAngles.y;
        }

        public void Stop()
        {
        }

        /// <summary>Transform.eulerAngles.x는 0~360 범위라, 아래로 기울인 각도가 큰 양수(예: 350)로 나온다. -180~180으로 바꿔야 위/아래 한도를 자연스럽게 clamp할 수 있다.</summary>
        private static float NormalizeSigned(float degrees)
        {
            return degrees > 180f ? degrees - 360f : degrees;
        }

        private static float ReadRotateKeyInput()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return 0f;
            }

            float input = 0f;
            if (keyboard.qKey.isPressed)
            {
                input -= 1f;
            }

            if (keyboard.eKey.isPressed)
            {
                input += 1f;
            }

            return input;
#else
            float input = 0f;
            if (Input.GetKey(KeyCode.Q))
            {
                input -= 1f;
            }

            if (Input.GetKey(KeyCode.E))
            {
                input += 1f;
            }

            return input;
#endif
        }

        private static bool IsDragging()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
            return Input.GetMouseButton(1);
#endif
        }

        private static Vector2 ReadMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current.position.ReadValue();
#else
            return Input.mousePosition;
#endif
        }
    }
}
