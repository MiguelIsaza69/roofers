using System;
using System.Collections.Generic;
using UnityEngine;
using RoofingSimulator.Gameplay;

namespace RoofingSimulator.World
{
    /// <summary>
    /// The ground tool rack near the supply area (like the tarp of tools in the site photos):
    /// every tool the player isn't currently carrying sits here. Look at one and press E to
    /// swap it into your active hotbar slot — the tool you were holding drops back onto the
    /// rack. Built by <c>Core.JobSceneController</c>. Each tool has a fixed peg that is shown
    /// only while that tool is off the belt.
    /// </summary>
    public class ToolRack : MonoBehaviour
    {
        private RoofingToolbelt belt;
        private readonly Dictionary<RoofingTool, GameObject> pegs = new Dictionary<RoofingTool, GameObject>();

        public static ToolRack Build(Vector3 center, RoofingToolbelt belt)
        {
            var root = new GameObject("ToolRack");
            root.transform.position = center;
            var rack = root.AddComponent<ToolRack>();
            rack.belt = belt;
            rack.BuildPegs();
            return rack;
        }

        private void BuildPegs()
        {
            // Tarp under the tools (decor — no collider so it never blocks the pickup ray).
            var tarp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tarp.name = "Tarp";
            tarp.transform.SetParent(transform, false);
            tarp.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            tarp.transform.localScale = new Vector3(2.9f, 0.04f, 2.3f);
            var tc = tarp.GetComponent<Collider>();
            if (tc != null) Destroy(tc);
            var tr = tarp.GetComponent<Renderer>();
            if (tr != null)
                tr.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                { color = new Color(0.18f, 0.22f, 0.30f) };

            var all = (RoofingTool[])Enum.GetValues(typeof(RoofingTool));
            const int perRow = 4;
            const float sx = 0.66f, sz = 0.68f;
            int rowsCount = Mathf.CeilToInt(all.Length / (float)perRow);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == RoofingTool.ShingleHand) continue; // the hand is always available (key 5), never on the rack
                int col = i % perRow, row = i / perRow;
                float x = (col - (perRow - 1) * 0.5f) * sx;
                float z = (row - (rowsCount - 1) * 0.5f) * sz;
                GameObject peg = ToolPickup.Build(transform, new Vector3(x, 0.06f, z), all[i], this);
                pegs[all[i]] = peg;
                peg.SetActive(!belt.Has(all[i])); // hide tools already in the belt
            }
        }

        /// <summary>Swap <paramref name="tool"/> into the belt's active slot; show the displaced one here.</summary>
        public void Take(RoofingTool tool)
        {
            RoofingTool displaced = belt.EquipToActiveSlot(tool);
            if (pegs.TryGetValue(tool, out var taken) && taken != null) taken.SetActive(false);
            if (pegs.TryGetValue(displaced, out var back) && back != null) back.SetActive(true);
        }
    }
}
