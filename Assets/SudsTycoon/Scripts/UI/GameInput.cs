using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SudsTycoon
{
    /// <summary>
    /// Floating joystick, keyboard, mouse-wheel / pinch zoom and taps on the world (port of the input part of ui.js).
    /// Pointer input arrives as UI Toolkit events on the full-screen touch layer behind the HUD.
    /// </summary>
    public static class GameInput
    {
        const float R = 52f;
        class Joy { public int id; public Vector2 start; public float t0; public bool moved; }
        static Joy joy;
        static readonly Dictionary<int, Vector2> pointers = new Dictionary<int, Vector2>();
        static float pinchDist;
        static Vector2 keyMove;

        public static void Init(VisualElement touch)
        {
            touch.RegisterCallback<PointerDownEvent>(OnDown);
            touch.RegisterCallback<PointerMoveEvent>(OnMove);
            touch.RegisterCallback<PointerUpEvent>(OnUp);
            touch.RegisterCallback<PointerCancelEvent>(e => End(e.pointerId, e.localPosition, false));
            touch.RegisterCallback<WheelEvent>(e =>
            {
                if (Blaster.Active || Hud.PanelOpen) return;
                CameraRig.I?.Zoom(e.delta.y > 0 ? 1.08f : 0.93f);
            });
        }

        static void OnDown(PointerDownEvent e)
        {
            var touch = (VisualElement)e.currentTarget;
            pointers[e.pointerId] = e.localPosition;
            touch.CapturePointer(e.pointerId);
            if (Blaster.Active) { Blaster.PointerDown(e.localPosition.x); return; }
            if (Hud.PanelOpen) return;
            if (pointers.Count == 2)
            {
                // second finger: pinch to zoom instead of steering
                CancelJoystick();
                var p = new List<Vector2>(pointers.Values);
                pinchDist = Vector2.Distance(p[0], p[1]);
                return;
            }
            if (joy != null) return;
            joy = new Joy { id = e.pointerId, start = e.localPosition, t0 = Time.unscaledTime };
            Hud.Joystick.style.left = e.localPosition.x;
            Hud.Joystick.style.top = e.localPosition.y;
        }

        static void OnMove(PointerMoveEvent e)
        {
            if (pointers.ContainsKey(e.pointerId)) pointers[e.pointerId] = e.localPosition;
            if (Blaster.Active) { Blaster.PointerMove(e.localPosition.x, e.pointerType == UnityEngine.UIElements.PointerType.mouse); return; }
            if (pointers.Count >= 2 && !Hud.PanelOpen)
            {
                var p = new List<Vector2>(pointers.Values);
                float d = Vector2.Distance(p[0], p[1]);
                if (pinchDist > 1f && d > 1f) CameraRig.I?.Zoom(pinchDist / d);
                pinchDist = d;
                return;
            }
            if (joy == null || e.pointerId != joy.id) return;
            Vector2 dv = (Vector2)e.localPosition - joy.start;
            float dist = dv.magnitude;
            if (dist > 8f && !joy.moved) { joy.moved = true; Hud.Joystick.AddToClassList("on"); }
            float k = dist > R ? R / dist : 1f;
            Hud.JoyKnob.style.translate = new Translate(dv.x * k, dv.y * k, 0f);
            float m = Mathf.Min(1f, dist / R);
            SetMove(dist > 4f ? dv.x / dist * m : 0f, dist > 4f ? dv.y / dist * m : 0f);
        }

        static void OnUp(PointerUpEvent e) => End(e.pointerId, e.localPosition, true);

        static void End(int pointerId, Vector2 pos, bool allowTap)
        {
            pointers.Remove(pointerId);
            Hud.Touch?.ReleasePointer(pointerId);
            if (Blaster.Active) { if (pointers.Count == 0) Blaster.PointerUp(); return; }
            if (joy == null || pointerId != joy.id) return;
            if (allowTap && !joy.moved && Time.unscaledTime - joy.t0 < 0.3f && !Hud.PanelOpen) WorldTap(pos);
            CancelJoystick();
        }

        static void CancelJoystick()
        {
            joy = null;
            Hud.Joystick.RemoveFromClassList("on");
            Hud.JoyKnob.style.translate = new Translate(0f, 0f, 0f);
            SetMove(0f, 0f);
        }

        /// <summary>Screen direction (right, down) to world direction, rotated by the camera yaw.</summary>
        static void SetMove(float jx, float jy)
        {
            float yaw = CameraRig.I ? CameraRig.I.yaw : 0.34f, c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            Game.Move = new Vector2(c * jx + s * jy, -s * jx + c * jy);
        }

        public static void Tick(float dt)
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.anyKey.wasPressedThisFrame) Hud.AnyKey();
            if (kb.escapeKey.wasPressedThisFrame && Hud.PanelOpen) { Hud.CloseFromKey(); return; }
            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float y = (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f) - (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f);
            if (Blaster.Active)
            {
                Blaster.Keys(x, kb.spaceKey.wasPressedThisFrame, kb.eKey.wasPressedThisFrame);
                if (kb.spaceKey.wasReleasedThisFrame) Blaster.KeyFireUp();
                return;
            }
            if (joy != null) return;
            var km = new Vector2(x, y);
            if (km != keyMove)
            {
                keyMove = km;
                float d = km.magnitude;
                if (d > 0f) km /= d;
                SetMove(km.x, km.y);
            }
        }

        /// <summary>Tap a napping worker to wake them, or a machine to open its upgrades (racks have none).</summary>
        static void WorldTap(Vector2 panelPos)
        {
            var cam = CameraRig.I ? CameraRig.I.Cam : null;
            if (!cam || Game.Player == null) return;
            float best = 55f * 55f;
            object hit = null;
            void Try(object o, Vector3 web)
            {
                if (!WorldUI.Project(cam, web, out var p)) return;
                float d = (p - panelPos).sqrMagnitude;
                if (d < best) { best = d; hit = o; }
            }
            foreach (var w in Game.Workers) Try(w, w.Pos + new Vector3(0f, 1.1f, 0f));
            foreach (var l in Game.Lanes) foreach (var st in l.Stations.Values) Try(st, new Vector3(st.X, 2f, st.Z));
            foreach (var r in Game.Racks.Values) Try(r, r.Pos + new Vector3(0f, 1.4f, 0f));
            if (hit is Worker wk) { if (wk.Napping) { wk.Wake(true); Hud.Toast($"{wk.Name} is back to work!", "worker"); } return; }
            if (hit is Station) Hud.OpenPanel("upgrades", new PanelArgs { tab = "machines" });
        }
    }
}
