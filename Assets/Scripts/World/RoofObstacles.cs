using UnityEngine;
using RoofingSimulator.Gameplay;
using RoofingSimulator.Player;

namespace RoofingSimulator.World
{
    /// <summary>
    /// Structural extras a contract can bring (Fase D, Etapa D3): a brick chimney and a
    /// glass skylight that OCCUPY roof cells (the grid marks them obstructed and you
    /// shingle around them), and a yard tree crowding the ladder. The chimney and tree
    /// are world-upright roots of their own; the skylight lies flush on the face.
    /// </summary>
    public static class RoofObstacles
    {
        /// <summary>
        /// A brick chimney through the roof: takes its cell out of play, covers the
        /// footprint with a metal flashing skirt and raises an upright brick stack.
        /// </summary>
        public static void SpawnChimney(RoofGrid grid, int row, int col)
        {
            RoofCell cell = grid.CellAt(row, col);
            if (cell == null) return;

            Transform face = cell.transform.parent;
            Vector3 world = cell.transform.position;
            Vector3 local = cell.transform.localPosition;
            float cw = grid.CellUp, cd = grid.CellSide;
            grid.ObstructCell(row, col);

            // Flashing skirt on the face so the freed footprint reads sealed, not bare.
            var skirt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            skirt.name = "ChimneyFlashing";
            skirt.transform.SetParent(face, false);
            skirt.transform.localPosition = local + Vector3.up * 0.03f;
            skirt.transform.localScale = new Vector3(cw * 0.98f, 0.05f, cd * 0.98f);
            StripCol(skirt);
            PaintTex(skirt, new Color(0.45f, 0.46f, 0.50f), RoofTextureLibrary.Surface.Metal, 2f, 0.4f);

            // Upright stack — its own scene root (the D2 teardown snapshot catches it).
            var root = new GameObject("Chimney");
            root.transform.position = world;
            root.transform.rotation = Quaternion.Euler(0f, face.eulerAngles.y, 0f);

            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cube); // keeps its collider — it's solid
            shaft.name = "Shaft";
            shaft.transform.SetParent(root.transform, false);
            shaft.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            shaft.transform.localScale = new Vector3(0.62f, 1.9f, 0.62f);
            PaintTex(shaft, new Color(0.66f, 0.33f, 0.24f), RoofTextureLibrary.Surface.Brick, 2f, 0.1f);

            var crown = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crown.name = "Crown";
            crown.transform.SetParent(root.transform, false);
            crown.transform.localPosition = new Vector3(0f, 1.53f, 0f);
            crown.transform.localScale = new Vector3(0.80f, 0.10f, 0.80f);
            StripCol(crown);
            PaintTex(crown, new Color(0.62f, 0.62f, 0.60f), RoofTextureLibrary.Surface.Metal, 2f, 0.15f);

            var flue = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            flue.name = "Flue";
            flue.transform.SetParent(root.transform, false);
            flue.transform.localPosition = new Vector3(0f, 1.66f, 0f);
            flue.transform.localScale = new Vector3(0.22f, 0.10f, 0.22f);
            StripCol(flue);
            Paint(flue, new Color(0.14f, 0.14f, 0.15f));
        }

        /// <summary>
        /// A glass skylight flush with the roof, spanning two cells along a course. Its
        /// cells leave play — and standing on the glass CRACKS it (see SkylightHazard).
        /// </summary>
        public static void SpawnSkylight(RoofGrid grid, int row, int col,
            PlayerLocomotion loco, PlayerStamina stamina)
        {
            RoofCell a = grid.CellAt(row, col);
            RoofCell b = grid.CellAt(row + 1, col);
            if (a == null || b == null) return;

            Transform face = a.transform.parent;
            Vector3 local = (a.transform.localPosition + b.transform.localPosition) * 0.5f;
            float w = grid.CellUp * 0.94f;   // up-slope span (one course wide)
            float d = grid.CellSide * 1.90f; // across span (two cells long)
            grid.ObstructCell(row, col);
            grid.ObstructCell(row + 1, col);

            var root = new GameObject("Skylight");
            root.transform.SetParent(face, false);
            root.transform.localPosition = local + Vector3.up * 0.02f;

            // Aluminium frame.
            Color alu = new Color(0.30f, 0.31f, 0.34f);
            FrameBar(root.transform, new Vector3(-w * 0.5f, 0.05f, 0f), new Vector3(0.07f, 0.10f, d + 0.07f), alu);
            FrameBar(root.transform, new Vector3(w * 0.5f, 0.05f, 0f), new Vector3(0.07f, 0.10f, d + 0.07f), alu);
            FrameBar(root.transform, new Vector3(0f, 0.05f, -d * 0.5f), new Vector3(w + 0.07f, 0.10f, 0.07f), alu);
            FrameBar(root.transform, new Vector3(0f, 0.05f, d * 0.5f), new Vector3(w + 0.07f, 0.10f, 0.07f), alu);

            // The pane: pale sky glass, glossy.
            var glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glass.name = "Glass";
            glass.transform.SetParent(root.transform, false);
            glass.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            glass.transform.localScale = new Vector3(w, 0.05f, d);
            StripCol(glass);
            var gr = glass.GetComponent<Renderer>();
            if (gr != null)
            {
                var m = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                { color = new Color(0.60f, 0.76f, 0.88f) };
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.92f);
                gr.sharedMaterial = m;
            }

            // One walkable collider for the whole unit, plus the standing hazard.
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.05f, 0f);
            box.size = new Vector3(w, 0.12f, d);
            root.AddComponent<SkylightHazard>().Init(loco, stamina,
                gr, new Vector2(w * 0.5f, d * 0.5f));
        }

        /// <summary>
        /// A yard tree crowding the ladder: solid trunk on the approach, leafy canopy
        /// (no collider) leaning over the climb so the eave corner is a squeeze.
        /// </summary>
        public static void SpawnTree(Vector3 groundPos, Vector3 leanDir)
        {
            var root = new GameObject("YardTree");
            root.transform.position = groundPos;

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // keeps its collider
            trunk.name = "Trunk";
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            trunk.transform.localScale = new Vector3(0.30f, 1.35f, 0.30f);
            PaintTex(trunk, new Color(0.38f, 0.27f, 0.16f), RoofTextureLibrary.Surface.WoodDeck, 2f, 0.05f);

            Vector3 lean = leanDir.sqrMagnitude > 0.001f ? leanDir.normalized : Vector3.zero;
            Blob(root.transform, new Vector3(0f, 2.9f, 0f) + lean * 0.5f, 2.6f, new Color(0.19f, 0.42f, 0.20f));
            Blob(root.transform, new Vector3(0.6f, 3.3f, 0.3f) + lean * 1.0f, 2.1f, new Color(0.23f, 0.48f, 0.22f));
            Blob(root.transform, new Vector3(-0.5f, 3.5f, -0.3f) + lean * 0.8f, 1.9f, new Color(0.16f, 0.38f, 0.18f));
            Blob(root.transform, new Vector3(0.2f, 2.5f, -0.5f) + lean * 1.3f, 1.7f, new Color(0.21f, 0.44f, 0.20f));
        }

        // ----- Build helpers -----

        private static void FrameBar(Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Frame";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            StripCol(go);
            PaintTex(go, color, RoofTextureLibrary.Surface.Metal, 2f, 0.4f);
        }

        private static void Blob(Transform parent, Vector3 pos, float diameter, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Canopy";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * diameter;
            StripCol(go); // leaves don't block — they crowd and hide
            Paint(go, color);
        }

        private static void StripCol(GameObject g)
        {
            var c = g.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }

        private static void Paint(GameObject g, Color color)
        {
            var r = g.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default")) { color = color };
        }

        private static void PaintTex(GameObject g, Color color, RoofTextureLibrary.Surface surf, float tile, float gloss)
        {
            var r = g.GetComponent<Renderer>();
            if (r == null) return;
            var m = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default")) { color = color };
            m.mainTexture = RoofTextureLibrary.Get(surf);
            m.mainTextureScale = new Vector2(tile, tile);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            r.sharedMaterial = m;
        }
    }

    /// <summary>
    /// Standing on a skylight is a rookie mistake: the glass creaks a warning, and if you
    /// stay it CRACKS — an injury hit plus a shove down the slope. One crack per pane;
    /// the glass stays walkable after (visibly ruined) so nobody gets soft-locked.
    /// </summary>
    public class SkylightHazard : MonoBehaviour
    {
        private PlayerLocomotion loco;
        private PlayerStamina stamina;
        private Renderer glass;
        private Vector2 half;
        private float standing;
        private bool warned;
        private bool cracked;

        public void Init(PlayerLocomotion locomotion, PlayerStamina stam, Renderer glassRenderer, Vector2 halfExtents)
        {
            loco = locomotion;
            stamina = stam;
            glass = glassRenderer;
            half = halfExtents;
        }

        private void Update()
        {
            if (cracked || loco == null) return;

            Vector3 local = transform.InverseTransformPoint(loco.transform.position);
            bool onGlass = Mathf.Abs(local.x) < half.x + 0.12f
                && Mathf.Abs(local.z) < half.y + 0.12f
                && local.y > -0.3f && local.y < 1.7f;

            if (!onGlass)
            {
                standing = Mathf.Max(0f, standing - Time.deltaTime * 2f);
                if (standing <= 0f) warned = false;
                return;
            }

            standing += Time.deltaTime;
            if (!warned && standing > 0.15f)
            {
                warned = true;
                HudNotice.Show("La claraboya CRUJE bajo tu peso — ¡sal de ahí!", 2.5f);
            }
            if (standing < 1.0f) return;

            cracked = true;
            HudNotice.Show("¡CRAC! El vidrio se rajó — por poco te cuelas", 3.5f);
            stamina?.AddInjury(10f);
            // +X of the face runs ridge→eave, so this shoves the player down the slope.
            loco.AddImpulse(transform.right * 3.5f + Vector3.up * 1.2f);

            if (glass != null) glass.sharedMaterial.color = new Color(0.72f, 0.77f, 0.79f);
            for (int i = 0; i < 3; i++)
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = "Crack";
                var c = line.GetComponent<Collider>();
                if (c != null) Destroy(c);
                line.transform.SetParent(transform, false);
                line.transform.localPosition = new Vector3(
                    Random.Range(-half.x * 0.4f, half.x * 0.4f), 0.075f,
                    Random.Range(-half.y * 0.4f, half.y * 0.4f));
                line.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 180f), 0f);
                line.transform.localScale = new Vector3(half.y * Random.Range(0.8f, 1.5f), 0.008f, 0.02f);
                var r = line.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                { color = new Color(0.92f, 0.95f, 0.97f) };
            }
        }
    }
}
