using UnityEngine;
using RoofingSimulator.Player;

namespace RoofingSimulator.World
{
    /// <summary>
    /// The skip/dumpster in the equipment area. Look at it with debris in your hands and
    /// press E to throw it all in — that's what counts toward the job's cleanliness bonus.
    /// </summary>
    public class DumpsterBin : MonoBehaviour, IInteractable
    {
        public string Prompt => "Tirar escombros";

        public bool CanInteract(PlayerInteractor interactor)
            => interactor.Materials != null && interactor.Materials.CarryingDebris;

        public void Interact(PlayerInteractor interactor)
        {
            interactor.Materials.DumpDebris();
        }
    }
}
