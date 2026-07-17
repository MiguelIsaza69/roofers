using UnityEngine;
using RoofingSimulator.Player;

namespace RoofingSimulator.World
{
    /// <summary>
    /// A material station in the equipment area. Each one stocks a single material (wood,
    /// felt or shingles) so the supplies are physically separated. Look at it and press E
    /// to restock that material.
    /// </summary>
    public class SupplyStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private MaterialKind kind = MaterialKind.Shingles;

        public void SetKind(MaterialKind k) => kind = k;

        public string Prompt => kind switch
        {
            MaterialKind.Wood => "Recoger plancha entera (2.4 m)",
            MaterialKind.Felt => "Recoger rollo de fieltro",
            _ => "Recoger tejas"
        };

        public bool CanInteract(PlayerInteractor interactor) => interactor.Materials != null;

        public void Interact(PlayerInteractor interactor) => interactor.Materials.TryLoad(kind);
    }
}
