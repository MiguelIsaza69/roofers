using UnityEngine;
using RoofingSimulator.Gameplay;

namespace RoofingSimulator.World
{
    /// <summary>
    /// A bolted-down roof antenna that must be removed with the <b>drill</b>: unscrew the 4
    /// corner bolts of its base (aim at each bolt and hold the drill — they back out and
    /// disappear), and once all four are out the whole antenna comes loose and tumbles off as an
    /// antenna-shaped piece of debris to toss. Vents and satellite dishes are fixed decoration
    /// that stay on the roof — you shingle around them.
    /// </summary>
    public class RoofFixture : MonoBehaviour
    {
        private int screwsLeft;
        private bool removed;

        /// <summary>Called by a bolt when it's fully out; returns true if that was the last one (antenna detaches).</summary>
        public bool ScrewRemoved()
        {
            if (removed) return false;
            screwsLeft--;
            if (screwsLeft > 0) return false;
            Detach();
            return true;
        }

        private void Detach()
        {
            removed = true;
            // Don't swap for a block — let the antenna itself fall, turned into tossable debris.
            transform.SetParent(null, true); // keep world pose
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.85f, 0f);
            box.size = new Vector3(0.6f, 1.7f, 0.6f);
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 2.5f;
            rb.AddForce(transform.up * 1.0f + Random.insideUnitSphere * 0.5f, ForceMode.VelocityChange);
            gameObject.AddComponent<Debris>(); // pick it up (E) and toss it like any debris
            Destroy(this);                      // it's no longer a fixture
        }

        // ----- Spawning -----

        /// <summary>A big bolted antenna the drill removes by its 4 corner screws.</summary>
        public static RoofFixture SpawnAntenna(Transform faceAnchor, Vector3 localPos)
        {
            var root = new GameObject("Fixture_Antenna");
            root.transform.SetParent(faceAnchor, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = Quaternion.identity;

            var fix = root.AddComponent<RoofFixture>();

            // Big base + tall mast + cross arms (no colliders here — only the bolts are targets).
            Part(root.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.6f, 0.06f, 0.6f), new Color(0.20f, 0.20f, 0.22f));
            Part(root.transform, new Vector3(0f, 0.85f, 0f), new Vector3(0.05f, 1.6f, 0.05f), new Color(0.62f, 0.62f, 0.64f));
            for (int i = 0; i < 4; i++)
            {
                float y = 1.05f + i * 0.18f;
                float w = 0.9f - i * 0.18f;
                Part(root.transform, new Vector3(0f, y, 0f), new Vector3(w, 0.02f, 0.02f), new Color(0.62f, 0.62f, 0.64f));
            }

            // Four corner bolts — each a separate target the drill unscrews.
            const float c = 0.24f;
            fix.screwsLeft = 4;
            BuildBolt(root.transform, fix, new Vector3(-c, 0.08f, -c));
            BuildBolt(root.transform, fix, new Vector3( c, 0.08f, -c));
            BuildBolt(root.transform, fix, new Vector3(-c, 0.08f,  c));
            BuildBolt(root.transform, fix, new Vector3( c, 0.08f,  c));
            return fix;
        }

        private static void BuildBolt(Transform parent, RoofFixture owner, Vector3 localPos)
        {
            var go = new GameObject("Bolt");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            // Grey steel, and a full screw: a LOW pan head sitting on the plate plus a shaft
            // buried in the base — the whole thing becomes visible as the drill backs it out.
            Color steel = new Color(0.58f, 0.59f, 0.62f);
            var head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0f, -0.005f, 0f);
            head.transform.localScale = new Vector3(0.10f, 0.015f, 0.10f); // low, like a real screw head
            StripCol(head);
            PaintMetal(head, steel);

            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.transform.SetParent(go.transform, false);
            shaft.transform.localPosition = new Vector3(0f, -0.065f, 0f);
            shaft.transform.localScale = new Vector3(0.035f, 0.055f, 0.035f); // hidden in the base until unscrewed
            StripCol(shaft);
            PaintMetal(shaft, steel * 0.85f);

            // Phillips slot (a dark cross on top) so it reads as a screw.
            Color slot = new Color(0.12f, 0.12f, 0.13f);
            var s1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s1.transform.SetParent(go.transform, false);
            s1.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            s1.transform.localScale = new Vector3(0.09f, 0.008f, 0.02f);
            StripCol(s1); Paint(s1, slot);
            var s2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s2.transform.SetParent(go.transform, false);
            s2.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            s2.transform.localScale = new Vector3(0.02f, 0.008f, 0.09f);
            StripCol(s2); Paint(s2, slot);

            // Generous box collider so the bolt is easy to aim at with the drill.
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.02f, 0f);
            box.size = new Vector3(0.18f, 0.16f, 0.18f);

            go.AddComponent<FixtureScrew>().Init(owner);
        }

        /// <summary>A fixed vent that stays on the roof (solid decoration to shingle around).</summary>
        public static void SpawnVent(Transform faceAnchor, Vector3 localPos)
        {
            var root = new GameObject("Vent");
            root.transform.SetParent(faceAnchor, false);
            root.transform.localPosition = localPos;
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.22f, 0f);
            box.size = new Vector3(0.32f, 0.45f, 0.32f);
            Part(root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.30f, 0.04f, 0.30f), new Color(0.25f, 0.18f, 0.14f));
            Part(root.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.14f, 0.42f, 0.14f), new Color(0.32f, 0.22f, 0.16f), PrimitiveType.Cylinder);
            Part(root.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.18f, 0.05f, 0.18f), new Color(0.20f, 0.20f, 0.22f), PrimitiveType.Cylinder);
        }

        /// <summary>A fixed satellite dish that stays on the roof (solid decoration).</summary>
        public static void SpawnDish(Transform faceAnchor, Vector3 localPos)
        {
            var root = new GameObject("Dish");
            root.transform.SetParent(faceAnchor, false);
            root.transform.localPosition = localPos;
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.26f, 0.05f);
            box.size = new Vector3(0.55f, 0.55f, 0.5f);
            Part(root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.14f, 0.04f, 0.14f), new Color(0.20f, 0.20f, 0.22f));
            Part(root.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.03f, 0.42f, 0.03f), new Color(0.50f, 0.50f, 0.52f));
            Transform dish = Part(root.transform, new Vector3(0f, 0.36f, 0.12f), new Vector3(0.5f, 0.06f, 0.5f), new Color(0.85f, 0.85f, 0.88f), PrimitiveType.Cylinder);
            dish.localRotation = Quaternion.Euler(55f, 0f, 0f);
        }

        // ----- Build helpers -----

        private static Transform Part(Transform parent, Vector3 pos, Vector3 scale, Color color, PrimitiveType type = PrimitiveType.Cube)
        {
            var p = GameObject.CreatePrimitive(type);
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localScale = scale;
            StripCol(p); // only the root box / bolts collide
            Paint(p, color);
            return p.transform;
        }

        private static void StripCol(GameObject g)
        {
            var c = g.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        private static void Paint(GameObject g, Color color)
        {
            var r = g.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default")) { color = color };
        }

        private static void PaintMetal(GameObject g, Color color)
        {
            var r = g.GetComponent<Renderer>();
            if (r == null) return;
            var m = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default")) { color = color };
            m.mainTexture = RoofTextureLibrary.Get(RoofTextureLibrary.Surface.Metal);
            m.mainTextureScale = new Vector2(2f, 2f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.5f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.5f);
            r.sharedMaterial = m;
        }
    }

    /// <summary>One corner bolt of a <see cref="RoofFixture"/>; the drill backs it out (it rises and
    /// spins as it unscrews), then it pops off and disappears.</summary>
    public class FixtureScrew : MonoBehaviour
    {
        private RoofFixture owner;
        private Vector3 basePos;
        private float work;
        private const float Needed = 0.9f; // seconds of drilling per bolt
        private bool done;

        public void Init(RoofFixture o)
        {
            owner = o;
            basePos = transform.localPosition;
        }

        /// <summary>Add drill progress (animates the bolt backing out); returns true on the frame the whole fixture comes loose.</summary>
        public bool AddWork(float dt)
        {
            if (done) return false;
            work += dt;
            float p = Mathf.Clamp01(work / Needed);
            transform.localPosition = basePos + new Vector3(0f, p * 0.13f, 0f); // back out along the roof normal
            transform.localRotation = Quaternion.Euler(0f, work * 1200f, 0f);   // spin while unscrewing
            if (work < Needed) return false;
            done = true;
            bool detached = owner != null && owner.ScrewRemoved();

            // The complete screw (head + shaft) pops free and tumbles off the roof.
            transform.SetParent(null, true);
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.AddForce(transform.up * 1.6f + Random.insideUnitSphere * 0.6f, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 6f, ForceMode.VelocityChange);
            Destroy(gameObject, 2.5f); // small hardware, not worth chasing — fades from play
            Destroy(this);             // no more drilling on it
            return detached;
        }
    }
}
