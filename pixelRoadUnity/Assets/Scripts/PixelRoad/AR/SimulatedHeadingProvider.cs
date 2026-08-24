using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelRoad.AR
{
    /// <summary>에디터 전용 나침반 시뮬레이션. Q/E 키로 기기 방위각을 회전시킨다(WASD/화살표는 SimulatedLocationProvider가 이동에 사용).</summary>
    public sealed class SimulatedHeadingProvider : IHeadingProvider
    {
        private const float RotateDegreesPerSecond = 90f;

        public float HeadingDegrees { get; private set; }
        public bool IsAvailable { get { return true; } }

        public void Start()
        {
        }

        public void Tick(float deltaTime)
        {
            float input = ReadRotateInput();
            if (Mathf.Approximately(input, 0f))
            {
                return;
            }

            HeadingDegrees = (HeadingDegrees + input * RotateDegreesPerSecond * deltaTime + 360f) % 360f;
        }

        public void Stop()
        {
        }

        private static float ReadRotateInput()
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
    }
}
