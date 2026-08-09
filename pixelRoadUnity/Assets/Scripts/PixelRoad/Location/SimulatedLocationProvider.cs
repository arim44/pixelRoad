using System.Collections;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelRoad.Location
{
    public sealed class SimulatedLocationProvider : ILocationProvider
    {
        private const double MetersPerLatitudeDegree = 111320.0;
        private GeoLocationSample current;
        private readonly float moveSpeedMetersPerSecond;
        private readonly float fastMoveMultiplier;

        public GeoLocationSample Current
        {
            get { return current; }
        }

        public string StatusText
        {
            get { return "Editor GPS simulation - WASD/Arrow move, Shift fast"; }
        }

        public SimulatedLocationProvider(double latitude, double longitude, float moveSpeedMetersPerSecond = 250f, float fastMoveMultiplier = 4f)
        {
            current = new GeoLocationSample(latitude, longitude, 5f, true);
            this.moveSpeedMetersPerSecond = moveSpeedMetersPerSecond;
            this.fastMoveMultiplier = fastMoveMultiplier;
        }

        public IEnumerator Start()
        {
            yield break;
        }

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
            current = new GeoLocationSample(nextLat, nextLon, 5f, true);
        }

        public void Stop()
        {
        }

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
