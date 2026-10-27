using UnityEngine;

namespace SudsTycoon
{
    /// <summary>The manager the player walks around the lot (port of Player in entities.js). Positions are web coordinates.</summary>
    public class Player
    {
        public readonly PersonRig Rig;
        public Vector3 Pos, Vel;
        public float Facing = Mathf.PI, IdleT;
        public const float Radius = 0.45f;
        public readonly CarryStack Stack;
        readonly WLabel maxLabel;
        readonly Transform shadow;

        public Player(Transform parent)
        {
            Rig = new PersonRig("manager", parent, Store.S.hats.on, 1.08f);
            Pos = new Vector3(Cfg.PlayerStart.x, 0f, Cfg.PlayerStart.y);
            Stack = new CarryStack(Rig.Body ? Rig.Body : Rig.Root, Game.MgrCarry);
            maxLabel = WorldUI.Label(() => Stack.SlotWorld(Stack.Count) + new Vector3(0f, 0.4f, 0f));
            maxLabel.Set("max", e => e.Add(WorldUI.Txt("MAX", "carry-max")));
            maxLabel.Show(false);
            shadow = Art.GroundDecal("PlayerShadow", Art.ShadowMat, 1.2f, parent, Pos.x, 0.03f, Pos.z);
            Sync();
        }

        public float Speed01 => U.Clamp(new Vector2(Vel.x, Vel.z).magnitude / Game.MgrSpeed(), 0f, 1f);
        public bool Near(float x, float z, float r) => U.Dist2(Pos.x, Pos.z, x, z) < r * r;

        /// <param name="input">Joystick direction in web x / z (length up to 1).</param>
        public void Update(float dt, Vector2 input)
        {
            float spd = Game.MgrSpeed();
            var want = new Vector2(input.x, input.y);
            float mag = Mathf.Min(1f, want.magnitude);
            if (mag > 0.01f) want = want.normalized * (spd * mag);
            float k = mag > 0.01f ? 14f : 18f;
            Vel.x = U.Damp(Vel.x, want.x, k, dt);
            Vel.z = U.Damp(Vel.z, want.y, k, dt);
            Pos.x += Vel.x * dt; Pos.z += Vel.z * dt;
            var b = Cfg.WalkBounds;
            Pos.x = U.Clamp(Pos.x, b.xMin, b.xMax); Pos.z = U.Clamp(Pos.z, b.yMin, b.yMax);
            Nav.Resolve(ref Pos, Radius);
            if (Vel.x * Vel.x + Vel.z * Vel.z > 0.05f) Facing = U.AngleLerp(Facing, Mathf.Atan2(Vel.x, Vel.z), 1f - Mathf.Exp(-14f * dt));
            float s01 = Speed01;
            Rig.Animate(dt, s01, Stack.Count > 0, Game.PolishingPlayer ? "polish" : null);
            Stack.Update(dt, s01);
            maxLabel.Show(Stack.Full && Stack.Count > 0);
            Sync();
            if (s01 < 0.05f) IdleT += dt; else IdleT = 0f;
        }

        public void Sync()
        {
            Rig.Root.position = U.P(Pos);
            Rig.Root.rotation = U.Yaw(Facing);
            shadow.position = U.P(Pos.x, 0.03f, Pos.z);
        }

        public void Destroy()
        {
            Stack.Clear();
            maxLabel.Remove();
            if (Rig.Root) Object.Destroy(Rig.Root.gameObject);
            if (shadow) Object.Destroy(shadow.gameObject);
        }
    }
}
