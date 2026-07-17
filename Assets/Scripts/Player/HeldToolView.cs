using System.Collections.Generic;
using UnityEngine;
using RoofingSimulator.Gameplay;

namespace RoofingSimulator.Player
{
    /// <summary>
    /// Builds simple first-person "in hand" models for each roofing tool out of primitives and
    /// shows the one matching the active toolbelt slot. The models sit on a dedicated view-model
    /// layer that a second, depth-only camera draws on top (set up in
    /// <see cref="PlayerRigBuilder"/>), the standard FPS trick so the tool never clips through
    /// walls and can have its own field of view. None of the parts carry colliders, so they
    /// never block the aim ray.
    /// </summary>
    public class HeldToolView : MonoBehaviour
    {
        /// <summary>User layer used only for held-tool models (no name needed; culling masks are by index).</summary>
        public const int ViewModelLayer = 8;

        private RoofingToolbelt toolbelt;
        private readonly Dictionary<RoofingTool, GameObject> models = new Dictionary<RoofingTool, GameObject>();
        private readonly Dictionary<RoofingTool, Quaternion> baseRots = new Dictionary<RoofingTool, Quaternion>();
        private RoofingTool shown = (RoofingTool)(-1);

        // Use animation: a short there-and-back swing on each apply, repeating while held.
        private Transform shownModel;
        private Quaternion baseRot = Quaternion.identity;
        private float swingElapsed = -1f;        // <0 = idle
        private const float SwingDuration = 0.20f;

        private static readonly Color Wood = new Color(0.55f, 0.38f, 0.22f);
        private static readonly Color Steel = new Color(0.75f, 0.77f, 0.80f);
        private static readonly Color DarkMetal = new Color(0.28f, 0.30f, 0.34f);
        private static readonly Color Rubber = new Color(0.12f, 0.12f, 0.14f);
        private static readonly Color Nap = new Color(0.30f, 0.30f, 0.34f);
        private static readonly Color ToolRed = new Color(0.80f, 0.24f, 0.18f);
        private static readonly Color Skin = new Color(0.86f, 0.66f, 0.52f);

        private void Awake()
        {
            toolbelt = GetComponentInParent<RoofingToolbelt>();
            BuildAll();
        }

        private void Update()
        {
            if (toolbelt != null && toolbelt.Current != shown) Show(toolbelt.Current);

            bool applying = ApplyHeld();
            if (applying && swingElapsed < 0f) swingElapsed = 0f; // begin a use swing
            AnimateSwing(applying);
        }

        /// <summary>Raw apply (left mouse / Fire1) so the tool animates on any click, target or not.</summary>
        private static bool ApplyHeld()
        {
            if (UnityEngine.Input.GetMouseButton(0)) return true;
            try { return UnityEngine.Input.GetButton("Fire1"); }
            catch (System.ArgumentException) { return false; }
        }

        /// <summary>Plays the short use motion on the active tool, looping while the button is held.</summary>
        private void AnimateSwing(bool applying)
        {
            if (shownModel == null || swingElapsed < 0f) return;

            swingElapsed += Time.deltaTime;
            float t = swingElapsed / SwingDuration;
            if (t >= 1f)
            {
                if (applying) { swingElapsed = 0f; t = 0f; }    // keep swinging while held
                else { swingElapsed = -1f; shownModel.localRotation = baseRot; shownModel.localPosition = Vector3.zero; return; }
            }

            float curve = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI); // 0 → 1 → 0
            (Vector3 pos, Vector3 rot) = Motion(shown);
            shownModel.localRotation = Quaternion.Euler(rot * curve) * baseRot;
            shownModel.localPosition = pos * curve;
        }

        /// <summary>Per-tool use motion (translation in camera space + extra rotation in camera space).</summary>
        private static (Vector3 pos, Vector3 rot) Motion(RoofingTool tool) => tool switch
        {
            RoofingTool.TearOffShovel      => (new Vector3(0f, -0.02f, 0.14f), new Vector3(18f, 0f, 0f)),   // pry/thrust
            RoofingTool.PryBar             => (new Vector3(0f, 0.04f, 0.06f),  new Vector3(-26f, 0f, 0f)),  // lever up
            RoofingTool.DeckHammer         => (new Vector3(0f, -0.05f, 0.05f), new Vector3(58f, 0f, 0f)),   // swing down
            RoofingTool.UnderlaymentRoller => (new Vector3(0f, 0f, 0.12f),     new Vector3(-8f, 0f, 0f)),   // push forward
            RoofingTool.ShingleHand        => (new Vector3(0f, -0.03f, 0.10f), new Vector3(16f, 0f, 0f)),   // place down
            RoofingTool.NailGun            => (new Vector3(0f, 0.01f, -0.06f), new Vector3(-14f, 0f, 0f)),  // recoil back
            RoofingTool.Drill              => (new Vector3(0f, 0f, 0.05f),     new Vector3(0f, 0f, -34f)),  // bore/twist
            RoofingTool.Saw                => (new Vector3(0f, -0.01f, 0.12f), new Vector3(0f, 0f, 0f)),    // push cut
            RoofingTool.TapeMeasure        => (new Vector3(0f, 0f, 0.06f),     new Vector3(0f, 0f, 0f)),    // pull out
            RoofingTool.UtilityKnife       => (new Vector3(0f, -0.02f, 0.09f), new Vector3(0f, 0f, -16f)),  // slice
            RoofingTool.ChalkLine          => (new Vector3(0f, -0.02f, 0.04f), new Vector3(10f, 0f, 0f)),   // snap
            _ => (Vector3.zero, Vector3.zero)
        };

        private void Show(RoofingTool tool)
        {
            shown = tool;
            foreach (var kv in models)
                if (kv.Value != null) kv.Value.SetActive(kv.Key == tool);

            if (models.TryGetValue(tool, out var go) && go != null)
            {
                shownModel = go.transform;
                baseRot = baseRots.TryGetValue(tool, out var q) ? q : shownModel.localRotation;
                shownModel.localRotation = baseRot;     // reset to the resting pose
                shownModel.localPosition = Vector3.zero;
            }
            swingElapsed = -1f;
        }

        private void BuildAll()
        {
            models[RoofingTool.TearOffShovel] = BuildShovel();
            models[RoofingTool.PryBar] = BuildPryBar();
            models[RoofingTool.DeckHammer] = BuildHammer();
            models[RoofingTool.UnderlaymentRoller] = BuildRoller();
            models[RoofingTool.ShingleHand] = BuildShingleHand();
            models[RoofingTool.NailGun] = BuildNailGun();
            models[RoofingTool.Drill] = BuildDrill();
            models[RoofingTool.Saw] = BuildSaw();
            models[RoofingTool.TapeMeasure] = BuildTapeMeasure();
            models[RoofingTool.UtilityKnife] = BuildUtilityKnife();
            models[RoofingTool.ChalkLine] = BuildChalkLine();
            foreach (var kv in models)
                if (kv.Value != null) baseRots[kv.Key] = kv.Value.transform.localRotation;
            Show(toolbelt != null ? toolbelt.Current : RoofingTool.TearOffShovel);
        }

        // ----- Tool models (local space: +Z forward, +X right, +Y up) -----

        private GameObject BuildShovel()
        {
            var root = NewRoot("Tool_Shovel");
            // Shaft.
            Part(root.transform, new Vector3(0f, 0f, 0.00f), new Vector3(0.035f, 0.035f, 0.42f), Wood, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.WoodDeck, 3f);
            // D-handle at the back (bars overlap the shaft end, cross bar joins them).
            Part(root.transform, new Vector3(-0.055f, 0f, -0.20f), new Vector3(0.022f, 0.022f, 0.12f), Wood, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.WoodDeck, 2f);
            Part(root.transform, new Vector3( 0.055f, 0f, -0.20f), new Vector3(0.022f, 0.022f, 0.12f), Wood, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.WoodDeck, 2f);
            Part(root.transform, new Vector3(0f, 0f, -0.24f), new Vector3(0.15f, 0.028f, 0.028f), Wood, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.WoodDeck, 2f);
            // Steel ferrule joining shaft to blade, then the wide angled blade.
            Part(root.transform, new Vector3(0f, -0.01f, 0.20f), new Vector3(0.06f, 0.05f, 0.10f), Steel, Quaternion.Euler(14f, 0f, 0f), PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f);
            Part(root.transform, new Vector3(0f, -0.05f, 0.31f), new Vector3(0.24f, 0.02f, 0.20f), Steel, Quaternion.Euler(22f, 0f, 0f), PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f);
            for (int t = -2; t <= 2; t++)
                Part(root.transform, new Vector3(t * 0.045f, -0.085f, 0.40f), new Vector3(0.03f, 0.018f, 0.05f), Steel, Quaternion.Euler(22f, 0f, 0f), PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f);
            Pose(root, -14f, -26f, 0f);
            return root;
        }

        private GameObject BuildPryBar()
        {
            var root = NewRoot("Tool_PryBar");
            // Painted steel shaft.
            Part(root.transform, new Vector3(0f, 0f, 0f), new Vector3(0.028f, 0.028f, 0.5f), DarkMetal, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f);
            // Front: a curved claw ending in a flat chisel (two angled segments), with a nail slot.
            Part(root.transform, new Vector3(0f, 0.02f, 0.26f), new Vector3(0.03f, 0.03f, 0.10f), Steel, Quaternion.Euler(-40f, 0f, 0f), PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f);
            Part(root.transform, new Vector3(0f, 0.07f, 0.30f), new Vector3(0.05f, 0.02f, 0.06f), Steel, Quaternion.Euler(-70f, 0f, 0f), PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f);
            Part(root.transform, new Vector3(0f, 0.085f, 0.32f), new Vector3(0.012f, 0.022f, 0.03f), Rubber, Quaternion.Euler(-70f, 0f, 0f)); // nail-puller V slot
            // Back: a slight bend + flat pry tip.
            Part(root.transform, new Vector3(0f, -0.01f, -0.26f), new Vector3(0.035f, 0.018f, 0.08f), Steel, Quaternion.Euler(18f, 0f, 0f), PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f);
            Pose(root, -10f, -24f, 0f);
            return root;
        }

        private GameObject BuildHammer()
        {
            var root = NewRoot("Tool_Hammer");
            // Vertical handle, rubber grip at the bottom (near the hand).
            Part(root.transform, new Vector3(0f, 0f, 0f), new Vector3(0.03f, 0.40f, 0.03f), Wood, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.WoodDeck, 3f);
            Part(root.transform, new Vector3(0f, -0.15f, 0f), new Vector3(0.042f, 0.16f, 0.042f), Rubber, Quaternion.identity);
            // Head across the top (along Z): striking face forward, claw at the back.
            Part(root.transform, new Vector3(0f, 0.21f, 0.01f), new Vector3(0.06f, 0.06f, 0.12f), Steel, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f);
            Part(root.transform, new Vector3(0f, 0.21f, 0.10f), new Vector3(0.065f, 0.065f, 0.05f), Steel, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f); // striking face
            Part(root.transform, new Vector3(0f, 0.235f, -0.06f), new Vector3(0.022f, 0.05f, 0.026f), Steel, Quaternion.Euler(22f, 0f, 0f), PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f); // claw
            Part(root.transform, new Vector3(0f, 0.195f, -0.07f), new Vector3(0.022f, 0.05f, 0.026f), Steel, Quaternion.Euler(-22f, 0f, 0f), PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f); // claw
            Pose(root, -6f, -18f, 6f);
            return root;
        }

        private GameObject BuildRoller()
        {
            var root = NewRoot("Tool_Roller");
            // Handle + rubber grip.
            Part(root.transform, new Vector3(0f, -0.02f, -0.02f), new Vector3(0.03f, 0.03f, 0.30f), Wood, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.WoodDeck, 3f);
            Part(root.transform, new Vector3(0f, -0.02f, -0.15f), new Vector3(0.04f, 0.04f, 0.12f), Rubber, Quaternion.identity);
            // Bent steel arm: up from the handle, forward to the axle (segments overlap).
            Part(root.transform, new Vector3(0f, 0.02f, 0.12f), new Vector3(0.022f, 0.12f, 0.022f), Steel, Quaternion.Euler(48f, 0f, 0f), PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f);
            Part(root.transform, new Vector3(0f, 0.09f, 0.20f), new Vector3(0.022f, 0.022f, 0.12f), Steel, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f);
            Part(root.transform, new Vector3(0f, 0.10f, 0.26f), new Vector3(0.20f, 0.018f, 0.018f), Steel, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f);
            // Nap drum (cylinder axis along X) on the axle.
            Part(root.transform, new Vector3(0f, 0.10f, 0.26f), new Vector3(0.10f, 0.14f, 0.10f), Nap, Quaternion.Euler(0f, 0f, 90f), PrimitiveType.Cylinder, RoofTextureLibrary.Surface.Felt, 3f);
            Pose(root, -12f, -28f, 0f);
            return root;
        }

        private GameObject BuildShingleHand()
        {
            var root = NewRoot("Tool_ShingleHand");
            // Just the gloved hand (no held tile): palm + three fingers + thumb.
            Part(root.transform, new Vector3(0f, -0.04f, 0.12f), new Vector3(0.12f, 0.06f, 0.15f), Skin, Quaternion.identity);
            for (int f = -1; f <= 1; f++)
                Part(root.transform, new Vector3(f * 0.038f, -0.035f, 0.21f), new Vector3(0.032f, 0.05f, 0.08f), Skin, Quaternion.Euler(20f, 0f, 0f));
            Part(root.transform, new Vector3(0.075f, -0.025f, 0.13f), new Vector3(0.034f, 0.05f, 0.06f), Skin, Quaternion.Euler(0f, 0f, -30f));
            Pose(root, -8f, -18f, 0f);
            return root;
        }

        private GameObject BuildNailGun()
        {
            var root = NewRoot("Tool_NailGun");
            // Body + air-motor cap (overlapping).
            Part(root.transform, new Vector3(0f, 0f, 0.12f), new Vector3(0.085f, 0.12f, 0.22f), ToolRed, Quaternion.identity);
            Part(root.transform, new Vector3(0f, 0.09f, 0.08f), new Vector3(0.075f, 0.06f, 0.075f), DarkMetal, Quaternion.identity, PrimitiveType.Cylinder);
            // Round coil canister (axis along X) sunk into the body, with a red lid.
            Part(root.transform, new Vector3(0f, -0.04f, 0.14f), new Vector3(0.12f, 0.05f, 0.12f), DarkMetal, Quaternion.Euler(0f, 0f, 90f), PrimitiveType.Cylinder, RoofTextureLibrary.Surface.Metal, 1f);
            Part(root.transform, new Vector3(0f, -0.04f, 0.14f), new Vector3(0.105f, 0.055f, 0.105f), ToolRed, Quaternion.Euler(0f, 0f, 90f), PrimitiveType.Cylinder);
            // Nose tip (overlaps the body front).
            Part(root.transform, new Vector3(0f, -0.03f, 0.26f), new Vector3(0.04f, 0.05f, 0.10f), Steel, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f);
            // Grip + trigger + hose connector.
            Part(root.transform, new Vector3(0f, -0.12f, 0.06f), new Vector3(0.05f, 0.16f, 0.07f), Rubber, Quaternion.Euler(-12f, 0f, 0f));
            Part(root.transform, new Vector3(0f, -0.055f, 0.13f), new Vector3(0.02f, 0.04f, 0.03f), DarkMetal, Quaternion.identity);
            Part(root.transform, new Vector3(0f, -0.19f, 0.01f), new Vector3(0.03f, 0.03f, 0.06f), DarkMetal, Quaternion.Euler(40f, 0f, 0f));
            Pose(root, -6f, -30f, 0f);
            return root;
        }

        private GameObject BuildDrill()
        {
            var root = NewRoot("Tool_Drill");
            Color blue = new Color(0.24f, 0.45f, 0.85f);
            // Body.
            Part(root.transform, new Vector3(0f, 0f, 0.08f), new Vector3(0.08f, 0.10f, 0.20f), blue, Quaternion.identity);
            // Chuck + bit out the front.
            Part(root.transform, new Vector3(0f, 0f, 0.20f), new Vector3(0.05f, 0.05f, 0.06f), Steel, Quaternion.Euler(90f, 0f, 0f), PrimitiveType.Cylinder, RoofTextureLibrary.Surface.Metal, 1f);
            Part(root.transform, new Vector3(0f, 0f, 0.30f), new Vector3(0.016f, 0.016f, 0.12f), DarkMetal, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f);
            // Grip + trigger + battery.
            Part(root.transform, new Vector3(0f, -0.13f, 0.04f), new Vector3(0.06f, 0.18f, 0.08f), DarkMetal, Quaternion.Euler(-10f, 0f, 0f));
            Part(root.transform, new Vector3(0f, -0.04f, 0.13f), new Vector3(0.02f, 0.045f, 0.03f), Steel, Quaternion.identity);
            Part(root.transform, new Vector3(0f, -0.22f, 0.05f), new Vector3(0.10f, 0.05f, 0.11f), blue, Quaternion.identity);
            Pose(root, -6f, -28f, 0f);
            return root;
        }

        private GameObject BuildSaw()
        {
            var root = NewRoot("Tool_Saw");
            // Motor body + base plate + handle + circular blade.
            Part(root.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.11f, 0.11f, 0.16f), ToolRed, Quaternion.identity);
            Part(root.transform, new Vector3(0.02f, -0.07f, 0.05f), new Vector3(0.17f, 0.015f, 0.24f), Steel, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f);
            Part(root.transform, new Vector3(0f, 0.13f, -0.05f), new Vector3(0.05f, 0.05f, 0.16f), DarkMetal, Quaternion.Euler(18f, 0f, 0f));
            Part(root.transform, new Vector3(0.07f, -0.01f, 0.10f), new Vector3(0.24f, 0.016f, 0.24f), Steel, Quaternion.Euler(0f, 0f, 90f), PrimitiveType.Cylinder, RoofTextureLibrary.Surface.Metal, 1f);
            Pose(root, -8f, -22f, 0f);
            return root;
        }

        private GameObject BuildTapeMeasure()
        {
            var root = NewRoot("Tool_TapeMeasure");
            Color yellow = new Color(0.92f, 0.78f, 0.18f);
            Part(root.transform, new Vector3(0f, 0f, 0f), new Vector3(0.11f, 0.12f, 0.07f), yellow, Quaternion.identity);
            Part(root.transform, new Vector3(0f, -0.05f, 0f), new Vector3(0.09f, 0.05f, 0.08f), DarkMetal, Quaternion.identity);
            // Tape blade + end hook.
            Part(root.transform, new Vector3(0f, 0.03f, 0.13f), new Vector3(0.04f, 0.012f, 0.18f), Steel, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f);
            Part(root.transform, new Vector3(0f, 0.01f, 0.22f), new Vector3(0.05f, 0.045f, 0.012f), Steel, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f);
            Pose(root, -4f, -20f, 0f);
            return root;
        }

        private GameObject BuildUtilityKnife()
        {
            var root = NewRoot("Tool_UtilityKnife");
            Color yellow = new Color(0.92f, 0.78f, 0.18f);
            Part(root.transform, new Vector3(0f, 0f, 0f), new Vector3(0.045f, 0.035f, 0.22f), yellow, Quaternion.identity);
            Part(root.transform, new Vector3(0f, 0.005f, -0.02f), new Vector3(0.05f, 0.045f, 0.12f), DarkMetal, Quaternion.identity);
            Part(root.transform, new Vector3(0f, 0.015f, 0.18f), new Vector3(0.006f, 0.05f, 0.08f), Steel, Quaternion.identity, PrimitiveType.Cube, RoofTextureLibrary.Surface.Metal, 1f); // blade
            Pose(root, -8f, -22f, 0f);
            return root;
        }

        private GameObject BuildChalkLine()
        {
            var root = NewRoot("Tool_ChalkLine");
            Color blue = new Color(0.24f, 0.45f, 0.85f);
            Part(root.transform, new Vector3(0f, 0f, 0f), new Vector3(0.10f, 0.14f, 0.05f), blue, Quaternion.identity); // teardrop case
            Part(root.transform, new Vector3(0.05f, 0.07f, 0f), new Vector3(0.03f, 0.03f, 0.04f), DarkMetal, Quaternion.identity); // crank knob
            Part(root.transform, new Vector3(0.08f, 0.07f, 0f), new Vector3(0.02f, 0.02f, 0.04f), Steel, Quaternion.identity); // crank handle
            Part(root.transform, new Vector3(0f, -0.10f, 0.06f), new Vector3(0.006f, 0.006f, 0.14f), Steel, Quaternion.identity); // string
            Part(root.transform, new Vector3(0f, -0.10f, 0.15f), new Vector3(0.03f, 0.02f, 0.02f), Steel, Quaternion.identity); // end hook
            Pose(root, -6f, -22f, 0f);
            return root;
        }

        // ----- Helpers -----

        private GameObject NewRoot(string toolName)
        {
            var go = new GameObject(toolName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.layer = ViewModelLayer;
            return go;
        }

        /// <summary>Tilt the whole tool into a natural "held" pose so it shows its side profile.</summary>
        private static void Pose(GameObject root, float pitch, float yaw, float roll)
            => root.transform.localRotation = Quaternion.Euler(pitch, yaw, roll);

        private static void Part(Transform parent, Vector3 localPos, Vector3 localScale, Color color,
            Quaternion localRot, PrimitiveType type = PrimitiveType.Cube,
            RoofTextureLibrary.Surface? surface = null, float tile = 2f)
        {
            var p = GameObject.CreatePrimitive(type);
            p.transform.SetParent(parent, false);
            p.transform.localPosition = localPos;
            p.transform.localRotation = localRot;
            p.transform.localScale = localScale;
            p.layer = ViewModelLayer;

            var col = p.GetComponent<Collider>();
            if (col != null) Destroy(col); // never block the aim ray

            var r = p.GetComponent<Renderer>();
            if (r == null) return;

            var m = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default")) { color = color };
            if (surface.HasValue)
            {
                m.mainTexture = RoofTextureLibrary.Get(surface.Value);
                m.mainTextureScale = new Vector2(tile, tile);
            }
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.2f);
            r.sharedMaterial = m;
        }
    }
}
