using System.Collections;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelRoad.Location
{
    /// <summary>
    /// 에디터에서 GPS 대신 쓰는 위치 공급자. 키보드 입력으로 좌표를 옮겨 현장에 나가지 않고도 방문 판정을 확인한다.
    /// </summary>
    public sealed class SimulatedLocationProvider : ILocationProvider
    {
        private const double MetersPerLatitudeDegree = 111320.0;
        private GeoLocation current;
        private readonly float moveSpeedMetersPerSecond;
        private readonly float fastMoveMultiplier;

        public GeoLocation Current
        {
            get { return current; }
        }

        public string StatusText
        {
            get { return "Editor GPS simulation - WASD/Arrow move, Shift fast"; }
        }

        /// <summary>시작 좌표와 이동 속도를 정해 시뮬레이션을 준비한다.</summary>
        public SimulatedLocationProvider(double latitude, double longitude, float moveSpeedMetersPerSecond = 250f, float fastMoveMultiplier = 4f)
        {
            current = new GeoLocation(latitude, longitude, 5f, true);
            this.moveSpeedMetersPerSecond = moveSpeedMetersPerSecond;
            this.fastMoveMultiplier = fastMoveMultiplier;
        }

        /// <summary>시뮬레이션은 준비할 것이 없어 바로 끝난다.</summary>
        public IEnumerator Start()
        {
            yield break;
        }

        /// <summary>키 입력만큼 좌표를 옮긴다. 이동 거리(m)를 위도·경도 변화량으로 환산한다.</summary>
        public void Tick(float deltaTime)
        {
            Vector2 input = ReadMoveInput();
            if (input.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            input = Vector2.ClampMagnitude(input, 1f);
            float speed = moveSpeedMetersPerSecond * ReadSpeedMultiplier(fastMoveMultiplier);
            double latMeters = input.y * speed * deltaTime;
            double lonMeters = input.x * speed * deltaTime;
            double lonScale = System.Math.Cos(current.Latitude * System.Math.PI / 180.0);
            double nextLat = current.Latitude + latMeters / MetersPerLatitudeDegree;
            double nextLon = current.Longitude + lonMeters / System.Math.Max(1.0, MetersPerLatitudeDegree * lonScale);
            current = new GeoLocation(nextLat, nextLon, 5f, true);
        }

        /// <summary>정리할 자원이 없어 아무것도 하지 않는다.</summary>
        public void Stop()
        {
        }

        /// <summary>WASD/방향키를 읽어 이동 방향을 만든다. 입력 시스템 유무에 따라 경로가 갈린다.</summary>
        private static Vector2 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            Vector2 input = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                input.x -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                input.x += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                input.y -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                input.y += 1f;
            }

            return input;
#else
            Vector2 input = Vector2.zero;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                input.x -= 1f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                input.x += 1f;
            }

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                input.y -= 1f;
            }

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                input.y += 1f;
            }

            return input;
#endif
        }

        /// <summary>Shift를 누르고 있으면 빠른 이동 배율을 준다. 먼 스팟까지 옮겨 볼 때 쓴다.</summary>
        private static float ReadSpeedMultiplier(float fastMoveMultiplier)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
            {
                return fastMoveMultiplier;
            }

            return 1f;
#else
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? fastMoveMultiplier : 1f;
#endif
        }
    }
}
