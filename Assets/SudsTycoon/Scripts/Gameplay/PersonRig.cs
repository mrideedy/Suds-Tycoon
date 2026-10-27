using UnityEngine;

namespace SudsTycoon
{
    /// <summary>
    /// Wraps an exported person model (Body, ArmL/ArmR, LegL/LegR, HatHolder) and animates it like the web build.
    /// Rotations about X keep their sign after glTFast's X-mirror; rotations about Y and Z flip.
    /// </summary>
    public class PersonRig
    {
        public readonly Transform Root, Body, ArmL, ArmR, LegL, LegR, HatHolder;
        public float Phase = U.Rand(0f, 6f);
        public string HatId;
        readonly string capVariant;
        float bodyTiltX;

        public PersonRig(string variant, Transform parent, string hat, float scale = 1f)
        {
            Root = new GameObject("Person_" + variant).transform;
            Root.SetParent(parent, false);
            var model = Art.Spawn("People/person_" + variant, Root).transform;
            Body = U.Find(model, "Body"); ArmL = U.Find(model, "ArmL"); ArmR = U.Find(model, "ArmR");
            LegL = U.Find(model, "LegL"); LegR = U.Find(model, "LegR"); HatHolder = U.Find(model, "HatHolder");
            capVariant = variant == "manager" ? "cap_star" : variant == "turbo" ? "cap_gold" : "cap";
            Root.localScale = Vector3.one * scale;
            if (hat != null && hat != "cap") SetHat(hat); else HatId = "cap";
        }

        public void SetHat(string id)
        {
            HatId = id;
            if (!HatHolder) return;
            for (int i = HatHolder.childCount - 1; i >= 0; i--) Object.Destroy(HatHolder.GetChild(i).gameObject);
            var hat = Art.Spawn("People/hat_" + (id == "cap" ? capVariant : id), HatHolder);
            hat.transform.localPosition = Vector3.zero;
            hat.transform.localRotation = Quaternion.identity;
        }

        static Quaternion R(float x, float z) => Quaternion.Euler(x * Mathf.Rad2Deg, 0f, -z * Mathf.Rad2Deg);

        /// <param name="extra">null, "polish", "sleep" or "cheer"</param>
        public void Animate(float dt, float speed01, bool carrying, string extra = null)
        {
            Phase += dt * (5f + speed01 * 9f);
            float sw = Mathf.Sin(Phase) * 0.75f * speed01;
            if (LegL) LegL.localRotation = R(sw, 0);
            if (LegR) LegR.localRotation = R(-sw, 0);
            if (ArmL && ArmR)
            {
                if (extra == "polish")
                {
                    float t = Phase * 0.8f;
                    ArmL.localRotation = R(-1.35f + Mathf.Sin(t) * 0.25f, Mathf.Cos(t) * 0.3f);
                    ArmR.localRotation = R(-1.35f + Mathf.Cos(t) * 0.25f, -Mathf.Sin(t) * 0.3f);
                }
                else if (extra == "sleep") { ArmL.localRotation = R(0.1f, 0.12f); ArmR.localRotation = R(0.1f, -0.12f); }
                else if (extra == "cheer") { ArmL.localRotation = R(-2.8f + Mathf.Sin(Phase * 2) * 0.3f, 0.2f); ArmR.localRotation = R(-2.8f - Mathf.Sin(Phase * 2) * 0.3f, -0.2f); }
                else if (carrying) { ArmL.localRotation = R(-1.2f, 0.1f); ArmR.localRotation = R(-1.2f, -0.1f); }
                else { ArmL.localRotation = R(-sw * 0.8f, 0.06f); ArmR.localRotation = R(sw * 0.8f, -0.06f); }
            }
            if (Body)
            {
                bodyTiltX = extra == "sleep" ? U.Damp(bodyTiltX, 0.22f, 4f, dt) : U.Damp(bodyTiltX, speed01 * 0.08f, 6f, dt);
                Body.localRotation = R(bodyTiltX, 0);
                float bob = Mathf.Abs(Mathf.Sin(Phase)) * 0.07f * speed01 + (speed01 < 0.05f ? Mathf.Sin(Phase * 0.4f) * 0.012f : 0f);
                var p = Body.localPosition; p.y = bob; Body.localPosition = p;
            }
        }
    }
}
