using UnityEngine;
using RoofingSimulator.Gameplay;

namespace RoofingSimulator.World
{
    /// <summary>
    /// Builds the small "on the ground" models and floating name labels shown on the tool rack.
    /// These are deliberately simple silhouettes (the detailed model is the in-hand one); the
    /// label removes any doubt about which tool is which.
    /// </summary>
    internal static class ToolVisuals
    {
        private static readonly Color Wood = new Color(0.55f, 0.38f, 0.22f);
        private static readonly Color Steel = new Color(0.75f, 0.77f, 0.80f);
        private static readonly Color Dark = new Color(0.22f, 0.23f, 0.26f);
        private static readonly Color Red = new Color(0.80f, 0.24f, 0.18f);
        private static readonly Color Yellow = new Color(0.92f, 0.78f, 0.18f);
        private static readonly Color Blue = new Color(0.24f, 0.45f, 0.85f);
        private static readonly Color Skin = new Color(0.86f, 0.66f, 0.52f);
        private static readonly Color Nap = new Color(0.30f, 0.30f, 0.34f);

        public static void BuildLabel(Transform parent, string text)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = 0.045f;
            tm.fontSize = 72;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;

            // Unity 6 dropped the default Arial; assign a built-in font (and its material) so the
            // label actually renders.
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
            {
                tm.font = font;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = font.material;
            }

            go.AddComponent<Billboard>();
        }

        public static void BuildIcon(Transform parent, RoofingTool tool)
        {
            var holder = new GameObject("Icon");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.Euler(18f, 25f, 0f); // tilt so it reads in 3D

            switch (tool)
            {
                case RoofingTool.TearOffShovel:
                    Prim(holder, new Vector3(0f, 0.04f, -0.05f), new Vector3(0.03f, 0.03f, 0.34f), Wood);
                    Prim(holder, new Vector3(0f, 0.0f, 0.16f), new Vector3(0.22f, 0.02f, 0.14f), Steel, Quaternion.Euler(20f, 0f, 0f));
                    break;
                case RoofingTool.PryBar:
                    Prim(holder, new Vector3(0f, 0.05f, 0f), new Vector3(0.03f, 0.03f, 0.40f), Dark);
                    Prim(holder, new Vector3(0f, 0.09f, 0.20f), new Vector3(0.05f, 0.02f, 0.06f), Steel, Quaternion.Euler(-60f, 0f, 0f));
                    break;
                case RoofingTool.DeckHammer:
                    Prim(holder, new Vector3(0f, 0.0f, 0f), new Vector3(0.03f, 0.34f, 0.03f), Wood);
                    Prim(holder, new Vector3(0f, 0.18f, 0.01f), new Vector3(0.05f, 0.05f, 0.12f), Steel);
                    break;
                case RoofingTool.UnderlaymentRoller:
                    Prim(holder, new Vector3(0f, 0.02f, -0.06f), new Vector3(0.03f, 0.03f, 0.26f), Wood);
                    Prim(holder, new Vector3(0f, 0.06f, 0.14f), new Vector3(0.20f, 0.10f, 0.10f), Nap, Quaternion.Euler(0f, 0f, 90f), PrimitiveType.Cylinder);
                    break;
                case RoofingTool.ShingleHand:
                    Prim(holder, new Vector3(0f, 0.02f, 0.06f), new Vector3(0.12f, 0.05f, 0.14f), Skin);
                    for (int f = -1; f <= 1; f++)
                        Prim(holder, new Vector3(f * 0.04f, 0.04f, 0.14f), new Vector3(0.03f, 0.05f, 0.07f), Skin);
                    break;
                case RoofingTool.NailGun:
                    Prim(holder, new Vector3(0f, 0.08f, 0.05f), new Vector3(0.08f, 0.10f, 0.20f), Red);
                    Prim(holder, new Vector3(0f, 0.0f, 0.06f), new Vector3(0.06f, 0.12f, 0.07f), Dark, Quaternion.Euler(-12f, 0f, 0f));
                    Prim(holder, new Vector3(0f, 0.04f, 0.06f), new Vector3(0.11f, 0.05f, 0.11f), Dark, Quaternion.Euler(0f, 0f, 90f), PrimitiveType.Cylinder);
                    break;
                case RoofingTool.Drill:
                    Prim(holder, new Vector3(0f, 0.10f, 0.02f), new Vector3(0.07f, 0.09f, 0.16f), Blue);
                    Prim(holder, new Vector3(0f, 0.04f, 0.04f), new Vector3(0.05f, 0.12f, 0.06f), Dark, Quaternion.Euler(-10f, 0f, 0f));
                    Prim(holder, new Vector3(0f, 0.10f, 0.16f), new Vector3(0.03f, 0.03f, 0.12f), Steel);
                    Prim(holder, new Vector3(0f, -0.02f, 0.04f), new Vector3(0.07f, 0.03f, 0.08f), Dark);
                    break;
                case RoofingTool.Saw:
                    Prim(holder, new Vector3(0f, 0.08f, 0f), new Vector3(0.10f, 0.10f, 0.14f), Red);
                    Prim(holder, new Vector3(0.06f, 0.0f, 0.10f), new Vector3(0.22f, 0.015f, 0.22f), Steel, Quaternion.Euler(0f, 0f, 90f), PrimitiveType.Cylinder);
                    Prim(holder, new Vector3(0.02f, -0.08f, 0.04f), new Vector3(0.16f, 0.015f, 0.20f), Steel);
                    break;
                case RoofingTool.TapeMeasure:
                    Prim(holder, new Vector3(0f, 0.06f, 0f), new Vector3(0.10f, 0.11f, 0.06f), Yellow);
                    Prim(holder, new Vector3(0f, 0.0f, 0.09f), new Vector3(0.05f, 0.01f, 0.10f), Steel);
                    break;
                case RoofingTool.UtilityKnife:
                    Prim(holder, new Vector3(0f, 0.04f, -0.02f), new Vector3(0.04f, 0.03f, 0.16f), Yellow);
                    Prim(holder, new Vector3(0f, 0.05f, 0.10f), new Vector3(0.006f, 0.04f, 0.06f), Steel);
                    break;
                case RoofingTool.ChalkLine:
                    Prim(holder, new Vector3(0f, 0.06f, 0f), new Vector3(0.10f, 0.12f, 0.05f), Blue);
                    Prim(holder, new Vector3(0.05f, 0.08f, 0f), new Vector3(0.02f, 0.02f, 0.05f), Steel);
                    Prim(holder, new Vector3(0f, -0.06f, 0.08f), new Vector3(0.03f, 0.02f, 0.02f), Steel);
                    break;
            }
        }

        private static void Prim(GameObject parent, Vector3 pos, Vector3 scale, Color color,
            Quaternion? rot = null, PrimitiveType type = PrimitiveType.Cube)
        {
            var p = GameObject.CreatePrimitive(type);
            p.transform.SetParent(parent.transform, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = rot ?? Quaternion.identity;
            p.transform.localScale = scale;
            var c = p.GetComponent<Collider>();
            if (c != null) Object.Destroy(c); // the peg root box handles the pickup ray
            var r = p.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default")) { color = color };
        }
    }
}
