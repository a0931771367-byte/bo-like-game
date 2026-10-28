using BoLike.Core;
using BoLike.Sim;
using UnityEngine;

namespace BoLike.Game
{
    /// <summary>
    /// Orthographic camera showing 720 px of height; width follows the device aspect. Follows the player
    /// with a look-ahead offset and clamps to the stage, like the Phaser camera with startFollow/setBounds.
    /// </summary>
    public class GameCamera : MonoBehaviour
    {
        public Camera cam;
        public World world;
        float followOffset = -150f;
        float centerX;
        float shakeTime, shakeDuration, shakeAmp;
        Vector2 shakeOffset;

        public static GameCamera Create(World world)
        {
            var go = new GameObject("GameCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Units.ViewHeightPx / 2f * Space.Scale;
            cam.nearClipPlane = -10;
            cam.farClipPlane = 10;
            cam.backgroundColor = Colors.Rgb(0x0b0d14);
            cam.clearFlags = CameraClearFlags.SolidColor;
            go.tag = "MainCamera";
            go.AddComponent<AudioListener>();
            var gc = go.AddComponent<GameCamera>();
            gc.cam = cam;
            gc.world = world;
            gc.centerX = Units.ViewWidthPx / 2f;
            return gc;
        }

        /// <summary>Visible width in px for the current aspect.</summary>
        public float ViewW => cam.orthographicSize * 2f * cam.aspect / Space.Scale;

        public float ScrollX => centerX - ViewW / 2f;

        public void SnapTo(float playerX)
        {
            centerX = playerX + 150;
            Apply();
        }

        public void Shake(float seconds, float intensity)
        {
            if (intensity * seconds < shakeAmp * (shakeDuration - shakeTime)) return;
            shakeTime = 0;
            shakeDuration = seconds;
            shakeAmp = intensity;
        }

        public void Follow(float dt)
        {
            var p = world.Player;
            if (p != null)
            {
                followOffset = Mathf.Lerp(followOffset, -150f * p.facing, 1 - Mathf.Pow(1 - 0.03f, dt * 60f));
                float target = p.x - followOffset;
                centerX = Mathf.Lerp(centerX, target, 1 - Mathf.Pow(1 - 0.12f, dt * 60f));
            }
            float half = ViewW / 2f;
            centerX = Mathf.Clamp(centerX, half, Mathf.Max(half, world.StageWidth - half));
            if (shakeTime < shakeDuration)
            {
                shakeTime += dt;
                float k = 1 - shakeTime / shakeDuration;
                shakeOffset = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * shakeAmp * Units.ViewWidthPx * k;
            }
            else shakeOffset = Vector2.zero;
            Apply();
        }

        void Apply()
        {
            world.CamX = ScrollX;
            world.ViewW = ViewW;
            transform.position = new Vector3((centerX + shakeOffset.x) * Space.Scale, -(Units.ViewHeightPx / 2f + shakeOffset.y) * Space.Scale, -5f);
        }
    }
}
