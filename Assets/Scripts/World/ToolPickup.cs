using UnityEngine;
using RoofingSimulator.Gameplay;
using RoofingSimulator.Player;

namespace RoofingSimulator.World
{
    /// <summary>
    /// One tool sitting on the <see cref="ToolRack"/>: a small model + a floating name label,
    /// with a box collider so the player can look at it and press E to take it into their
    /// active hotbar slot.
    /// </summary>
    public class ToolPickup : MonoBehaviour, IInteractable
    {
        private ToolRack rack;
        private RoofingTool tool;

        public string Prompt => $"Tomar {RoofingToolbelt.Name(tool)}";
        public bool CanInteract(PlayerInteractor interactor) => true;
        public void Interact(PlayerInteractor interactor) => rack.Take(tool);

        public static GameObject Build(Transform parent, Vector3 localPos, RoofingTool tool, ToolRack rack)
        {
            var go = new GameObject($"ToolPeg_{tool}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.5f, 0.45f, 0.5f);
            box.center = new Vector3(0f, 0.12f, 0f);

            var pick = go.AddComponent<ToolPickup>();
            pick.rack = rack;
            pick.tool = tool;

            ToolVisuals.BuildIcon(go.transform, tool);
            return go;
        }
    }
}
