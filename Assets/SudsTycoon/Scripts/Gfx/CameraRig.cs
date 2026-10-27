using UnityEngine;

namespace SudsTycoon
{
    /// <summary>
    /// High three-quarter follow camera, ported from the web build: narrow 30 degree lens, about 46 degrees down
    /// and 20 degrees around, framing more of the lot on wide screens. Works in web coordinates.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I { get; private set; }
        public Camera Cam { get; private set; }
        public float yaw = 0.34f, pitch = 0.8f, fov = 30f, zoom = 1f;
        public const float MinZoom = 0.62f, MaxZoom = 1.55f;
        float dist = 46f, shake;
        Vector3 look;

        void Awake()
        {
            I = this;
            Cam = GetComponent<Camera>();
            Cam.fieldOfView = fov;
            Cam.nearClipPlane = 1f;
            Cam.farClipPlane = 500f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
        }

        public float BaseDist()
        {
            float a = Mathf.Max(0.2f, Cam.aspect);
            float t = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 2f;
            float wantW = U.Lerp(16f, 40f, U.Clamp((a - 0.45f) / (1.6f - 0.45f), 0f, 1f));
            const float wantH = 23f;
            return Mathf.Max(wantH / t, wantW / (t * a));
        }

        /// <summary>Follow a target in web coordinates (x, z). Snap jumps straight there.</summary>
        public void Follow(Vector3 webTarget, float dt, bool snap = false)
        {
            var target = new Vector3(webTarget.x, 0f, webTarget.z);
            look = snap ? target : Vector3.Lerp(look, target, 1f - Mathf.Exp(-6f * dt));
            dist = snap ? BaseDist() * zoom : U.Damp(dist, BaseDist() * zoom, 8f, dt);
            var off = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch) * dist, Mathf.Sin(pitch) * dist, Mathf.Cos(yaw) * Mathf.Cos(pitch) * dist);
            var pos = look + off;
            if (shake > 0.001f)
            {
                shake *= Mathf.Exp(-9f * dt);
                pos += new Vector3(U.Rand(-1f, 1f), U.Rand(-1f, 1f), U.Rand(-1f, 1f)) * shake;
            }
            transform.position = U.P(pos);
            transform.LookAt(U.P(look + new Vector3(0f, 0.5f, 0f)));
        }
        public void Shake(float a) { shake = Mathf.Max(shake, a); }
        public void Zoom(float factor) { zoom = U.Clamp(zoom * factor, MinZoom, MaxZoom); }

        /// <summary>World (web coords) to screen pixels; z &lt; 0 means behind the camera.</summary>
        public Vector3 ToScreen(Vector3 web) => Cam.WorldToScreenPoint(U.P(web));
    }
}
