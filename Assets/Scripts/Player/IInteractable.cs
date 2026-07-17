namespace RoofingSimulator.Player
{
    /// <summary>
    /// Something the player can look at and use with the interact key (E): debris to pick
    /// up, a supply pallet to restock, a dumpster to throw debris into, a shop later, etc.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Short verb shown in the prompt, e.g. "Recoger escombro".</summary>
        string Prompt { get; }

        /// <summary>Whether the interaction is currently allowed (e.g. hands not full).</summary>
        bool CanInteract(PlayerInteractor interactor);

        /// <summary>Perform the interaction.</summary>
        void Interact(PlayerInteractor interactor);
    }
}
