using UnityEngine;

namespace RoofingSimulator.Player
{
    /// <summary>
    /// Casts from the crosshair each frame for the nearest <see cref="IInteractable"/> and
    /// runs it on the interact key. Shows a small prompt while one is in range. This is the
    /// shared "press E to use" plumbing for debris, supply stations, dumpsters and (later)
    /// the shop.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private float range = 3.2f;
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        private Camera cam;
        private IInteractable current;

        /// <summary>The player's material/inventory state, so interactables can read/modify it.</summary>
        public PlayerMaterials Materials { get; private set; }

        /// <summary>The aim camera, for interactables that keep tracking the crosshair (felt-roll drag).</summary>
        public Camera Cam => cam;

        /// <summary>The interact key, so a grabbed interactable can watch for its release press.</summary>
        public KeyCode Key => interactKey;

        // While something is held (felt roll, wheelbarrow, foam pad) IT owns the E key, so the
        // interactor must not fire other interactables on the same press.
        private static int claimFrame = -10;

        /// <summary>Held objects call this every frame they own the interact key.</summary>
        public static void ClaimInteraction() => claimFrame = Time.frameCount;

        private static bool InteractionClaimed => Time.frameCount <= claimFrame + 1;

        private void Awake()
        {
            cam = GetComponentInChildren<Camera>();
            Materials = GetComponent<PlayerMaterials>();
        }

        private void Update()
        {
            current = null;
            if (cam == null || InteractionClaimed) return;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Collide))
            {
                current = hit.collider.GetComponentInParent<IInteractable>();
            }

            if (current != null && current.CanInteract(this) && UnityEngine.Input.GetKeyDown(interactKey))
            {
                current.Interact(this);
            }
        }

        private void OnGUI()
        {
            if (current == null || !current.CanInteract(this)) return;

            var style = new GUIStyle(GUI.skin.box) { fontSize = 16, fontStyle = FontStyle.Bold };
            GUI.Box(new Rect(Screen.width * 0.5f - 170f, Screen.height * 0.60f, 340f, 32f),
                $"[{interactKey}]  {current.Prompt}", style);
        }
    }
}
