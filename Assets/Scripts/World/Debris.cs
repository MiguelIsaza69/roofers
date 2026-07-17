using UnityEngine;
using RoofingSimulator.Player;

namespace RoofingSimulator.World
{
    /// <summary>
    /// A torn-off chunk of old roofing. It's a physical rigidbody so it tumbles down the
    /// roof and onto the ground (satisfying mess). Look at it and press E to pick it up
    /// into your hands; carry it to a <see cref="DumpsterBin"/> to throw it away.
    /// </summary>
    public class Debris : MonoBehaviour, IInteractable
    {
        public string Prompt => "Recoger escombro";

        public bool CanInteract(PlayerInteractor interactor)
            => interactor.Materials != null && interactor.Materials.CanPickDebris;

        public void Interact(PlayerInteractor interactor)
        {
            if (interactor.Materials.TryLoadDebris())
            {
                Destroy(gameObject);
            }
        }

        /// <summary>Spawn a tumbling debris chunk at a position.</summary>
        public static Debris Spawn(Vector3 position, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Debris";
            go.transform.position = position;
            go.transform.rotation = Random.rotation;
            go.transform.localScale = size;

            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                { color = color };
            }

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.8f;
            return go.AddComponent<Debris>();
        }
    }
}
