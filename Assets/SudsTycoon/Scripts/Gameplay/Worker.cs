using System.Collections.Generic;
using UnityEngine;

namespace SudsTycoon
{
    /// <summary>A refill job: carry supplies from a rack to a hopper.</summary>
    public class StockTask
    {
        public Hopper hopper;
        public Rack rack;
        public string supply;
        public int amount;
    }

    /// <summary>Staff: stocker / cashier / polisher / turbo (temporary super stocker). Port of Worker in entities.js.</summary>
    public class Worker
    {
        public readonly string Role, Name;
        public readonly int Lane;
        public readonly bool Turbo;
        public readonly PersonRig Rig;
        public Vector3 Pos;
        public float Facing, SpeedNow, Life = float.PositiveInfinity;
        public string State = "idle";
        public bool Napping, AtPost;
        public StockTask Task;
        public Station Post;
        public readonly CarryStack Stack;

        List<Vector2> path = new List<Vector2>();
        Vector2? goal;
        float wait, napIn, napT, loadT, waitedRack;
        readonly WLabel tag, zzz;
        readonly Transform shadow;
        bool dead;

        public Worker(string role, Transform parent, int lane = 0, string name = "Worker")
        {
            Role = role; Lane = lane; Name = name;
            Turbo = role == "turbo";
            Rig = new PersonRig(role, parent, "cap");
            var door = Game.StaffDoor();
            Pos = new Vector3(door.x + U.Rand(-0.5f, 0.5f), 0f, door.y + U.Rand(0f, 0.6f));
            Stack = new CarryStack(Rig.Body ? Rig.Body : Rig.Root, () => Turbo ? 10 : Game.StaffCarry());
            napIn = U.Rand(70f, 140f);
            shadow = Art.GroundDecal("WorkerShadow", Art.ShadowMat, 1.1f, parent, Pos.x, 0.03f, Pos.z);
            tag = WorldUI.Label(Rig.Root, 2.3f);
            tag.Set(name, e => e.Add(WorldUI.Txt(name, "name-tag")));
            zzz = WorldUI.Label(Rig.Root, 2.0f);
            zzz.Set("zzz", e => e.Add(WorldUI.Txt("z z Z", "zzz")));
            zzz.Show(false);
            if (Turbo) Life = Cfg.Boosts["turbo"].x;
            Sync(0f);
        }

        float Spd => Turbo ? 9f : Game.StaffSpeed();

        void GoTo(float x, float z) { path = Nav.Find(Pos.x, Pos.z, x, z); goal = new Vector2(x, z); }

        bool Walk(float dt)
        {
            if (path.Count == 0) { SpeedNow = U.Damp(SpeedNow, 0f, 10f, dt); return true; }
            var p = path[0];
            float dx = p.x - Pos.x, dz = p.y - Pos.z, d = Mathf.Sqrt(dx * dx + dz * dz);
            float step = Spd * dt;
            if (d <= step) { Pos.x = p.x; Pos.z = p.y; path.RemoveAt(0); }
            else
            {
                Pos.x += dx / d * step; Pos.z += dz / d * step;
                Facing = U.AngleLerp(Facing, Mathf.Atan2(dx, dz), 1f - Mathf.Exp(-12f * dt));
            }
            SpeedNow = U.Damp(SpeedNow, 1f, 10f, dt);
            return path.Count == 0;
        }

        void Sleep()
        {
            if (Turbo || Napping) return;
            Napping = true; napT = U.Rand(8f, 14f); zzz.Show(true);
            if (Game.Player.Near(Pos.x, Pos.z, 9f)) Sfx.Play("snore");
            Bus.Emit("worker:nap", this);
        }

        public void Wake(bool byPlayer)
        {
            if (!Napping) return;
            Napping = false; zzz.Show(false);
            napIn = U.Rand(70f, 140f) / Game.NapMul();
            if (byPlayer)
            {
                Sfx.Play("wake");
                WorldUI.Text(new Vector3(Pos.x, 2.4f, Pos.z), "!", "big", 0.7f, 30f);
                Bus.Emit("worker:woke", this);
            }
        }

        public void Update(float dt)
        {
            if (dead) return;
            if (Turbo)
            {
                Life -= dt;
                if (Life <= 0f) { Dismiss(); return; }
            }
            if (Napping)
            {
                napT -= dt;
                if (Game.Player.Near(Pos.x, Pos.z, 2.2f)) Wake(true);
                else if (napT <= 0f) Wake(false);
                Sync(dt); return;
            }
            if (!Turbo)
            {
                napIn -= dt * Game.NapMul();
                if (napIn <= 0f) { Sleep(); Sync(dt); return; }
            }
            if (Role == "stocker" || Turbo) StockerAI(dt); else PostAI(dt);
            Sync(dt);
        }

        void Sync(float dt)
        {
            Rig.Root.position = U.P(Pos);
            Rig.Root.rotation = U.Yaw(Facing);
            string extra = Napping ? "sleep" : (Role == "polisher" && AtPost && Post != null && Post.Busy) ? "polish" : null;
            Rig.Animate(dt, Napping ? 0f : SpeedNow, Stack.Count > 0, extra);
            Stack.Update(dt, SpeedNow);
            shadow.position = U.P(Pos.x, 0.03f, Pos.z);
        }

        // cashier / polisher: walk to the post and stay there
        void PostAI(float dt)
        {
            var st = Game.PostFor(Role, Lane);
            Post = st;
            if (st == null) { AtPost = false; SpeedNow = U.Damp(SpeedNow, 0f, 10f, dt); return; }
            var spot = st.OperatorSpot;
            if (!AtPost)
            {
                if (path.Count == 0 || goal == null || U.Dist2(goal.Value.x, goal.Value.y, spot.x, spot.z) > 0.01f) GoTo(spot.x, spot.z);
                if (Walk(dt) || U.Dist2(Pos.x, Pos.z, spot.x, spot.z) < 0.04f) { AtPost = true; path.Clear(); }
            }
            else
            {
                SpeedNow = U.Damp(SpeedNow, 0f, 10f, dt);
                Facing = U.AngleLerp(Facing, st.OperatorFacing, 1f - Mathf.Exp(-8f * dt));
                if (U.Dist2(Pos.x, Pos.z, spot.x, spot.z) > 0.25f) AtPost = false;
            }
        }

        void StockerAI(float dt)
        {
            if (wait > 0f) { wait -= dt; SpeedNow = U.Damp(SpeedNow, 0f, 10f, dt); return; }
            switch (State)
            {
                case "idle":
                {
                    var t = Game.PickStockTask(this);
                    if (t != null)
                    {
                        Task = t; t.hopper.Incoming += t.amount;
                        if (Stack.CountOf(t.supply) >= 1) { State = "toHopper"; GoTo(t.hopper.Mat.x, t.hopper.Mat.y); }
                        else { State = "toRack"; GoTo(t.rack.Mat.x, t.rack.Mat.y); }
                    }
                    else if (Stack.Count > 0) { State = "wander"; GoTo(U.Rand(-10f, 6f), U.Rand(4.6f, 5.6f)); wait = 0f; }
                    else { State = "wander"; GoTo(U.Rand(-14f, 8f), U.Rand(4.5f, 5.8f)); }
                    break;
                }
                case "wander":
                    if (Walk(dt)) { wait = U.Rand(1.0f, 2.5f); State = "idle"; }
                    break;
                case "toRack":
                    if (Walk(dt)) { State = "loading"; loadT = 0f; waitedRack = 0f; }
                    break;
                case "loading":
                {
                    var r = Task.rack;
                    SpeedNow = U.Damp(SpeedNow, 0f, 10f, dt);
                    Facing = U.AngleLerp(Facing, 0f, 1f - Mathf.Exp(-8f * dt));
                    loadT -= dt;
                    int have = Stack.CountOf(Task.supply);
                    if (have >= Task.amount || Stack.Full) { State = "toHopper"; GoTo(Task.hopper.Mat.x, Task.hopper.Mat.y); break; }
                    if (loadT <= 0f && r.Stock > 0)
                    {
                        if (r.Take(out var from)) { Stack.Add(Task.supply, from); Sfx.Play("pop"); }
                        loadT = Turbo ? 0.06f : 0.14f;
                    }
                    if (r.Stock <= 0)
                    {
                        waitedRack += dt;
                        if (waitedRack > 3f && have > 0) { State = "toHopper"; GoTo(Task.hopper.Mat.x, Task.hopper.Mat.y); }
                    }
                    break;
                }
                case "toHopper":
                    if (Walk(dt)) { State = "unloading"; loadT = 0f; }
                    break;
                case "unloading":
                {
                    var h = Task.hopper;
                    SpeedNow = U.Damp(SpeedNow, 0f, 10f, dt);
                    Facing = U.AngleLerp(Facing, Mathf.PI, 1f - Mathf.Exp(-8f * dt));
                    loadT -= dt;
                    if (loadT <= 0f)
                    {
                        if (h.Count < h.Cap && Stack.CountOf(Task.supply) > 0)
                        {
                            var it = Stack.Take(Task.supply, out var from);
                            h.Receive(it.tr, from);
                            h.Incoming = Mathf.Max(0, h.Incoming - 1); Task.amount--;
                            loadT = Turbo ? 0.06f : 0.12f;
                        }
                        else
                        {
                            h.Incoming = Mathf.Max(0, h.Incoming - Mathf.Max(0, Task.amount));
                            Task = null; State = "idle";
                        }
                    }
                    break;
                }
            }
        }

        public void DropTask()
        {
            if (Task == null) return;
            Task.hopper.Incoming = Mathf.Max(0, Task.hopper.Incoming - Mathf.Max(0, Task.amount));
            Task = null;
        }

        public void Dismiss()
        {
            DropTask();
            Fx.Smoke(new Vector3(Pos.x, 0.8f, Pos.z), 8, 0.6f); Sfx.Play("poof");
            Destroy();
            Game.RemoveWorker(this);
        }

        public void Destroy()
        {
            dead = true;
            Stack.Clear();
            if (Rig.Root) Object.Destroy(Rig.Root.gameObject);
            if (shadow) Object.Destroy(shadow.gameObject);
            tag.Remove(); zzz.Remove();
        }
    }
}
