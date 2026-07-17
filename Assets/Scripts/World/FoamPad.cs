using UnityEngine;
using RoofingSimulator.Player;

namespace RoofingSimulator.World
{
    /// <summary>
    /// Anti-slip foam pad (Etapa 7). Pick it up with E (it hovers in your hands, not the
    /// material inventory), aim where you want it — the sloped roof, ideally — and press E to
    /// set it down flush with the surface. While you stand on or right next to a placed pad,
    /// you get GRIP: no slide and no steep-slope speed penalty (kneeling comfort comes later).
    /// A small stack of pads waits in the equipment area.
    /// </summary>
    public class FoamPad : MonoBehaviour, IInteractable
    {
        private const float GripRadius = 0.85f;
        private const float PlaceRange = 4.5f;

        private Collider solid;
        private Collider gripZone;
        private bool held;
        private PlayerInteractor holder;
        private int grabFrame;

        // ----- Spawning -----

        public static FoamPad Spawn(Vector3 position)
        {
            var root = new GameObject("FoamPad");
            root.transform.position = position;
            var pad = root.AddComponent<FoamPad>();
            pad.BuildVisual();
            return pad;
        }

        private void BuildVisual()
        {
            Color foam = new Color(0.95f, 0.45f, 0.12f); // safety orange
            Color top = new Color(1f, 0.62f, 0.28f);

            Slab(new Vector3(0f, 0.035f, 0f), new Vector3(0.52f, 0.07f, 0.38f), foam);
            Slab(new Vector3(0f, 0.075f, 0f), new Vector3(0.46f, 0.015f, 0.32f), top); // kneel face

            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.045f, 0f);
            box.size = new Vector3(0.52f, 0.1f, 0.38f);
            solid = box;

            // Grip aura: while the player is inside, the locomotion stops sliding.
            var zone = new GameObject("GripZone");
            zone.transform.SetParent(transform, false);
            zone.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            var sphere = zone.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = GripRadius;
            zone.AddComponent<FoamPadGrip>();
            gripZone = sphere;
        }

        private void Slab(Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Foam";
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                { color = color };
        }

        // ----- Interaction -----

        public string Prompt => "Llevar espuma antideslizante";

        public bool CanInteract(PlayerInteractor interactor) => !held;

        public void Interact(PlayerInteractor interactor)
        {
            if (held) return;
            held = true;
            holder = interactor;
            grabFrame = Time.frameCount;
            if (solid != null) solid.enabled = false;       // let raycasts pass while carried
            if (gripZone != null) gripZone.enabled = false; // no grip from a pad in your hands
        }

        private void Update()
        {
            if (!held) return;
            PlayerInteractor.ClaimInteraction(); // we own E while carried

            if (holder == null)
            {
                PlaceAt(transform.position, Vector3.up);
                return;
            }

            Camera cam = holder.Cam;
            // Hover low in front of the player while carried.
            if (cam != null)
            {
                transform.position = cam.transform.position + cam.transform.forward * 0.9f
                    + Vector3.down * 0.35f;
                transform.rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
            }

            if (Time.frameCount != grabFrame && UnityEngine.Input.GetKeyDown(holder.Key))
            {
                // Set it down flush with whatever the crosshair points at (the roof, ideally).
                if (cam != null && Physics.Raycast(
                        cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)),
                        out RaycastHit hit, PlaceRange, ~0, QueryTriggerInteraction.Ignore))
                {
                    PlaceAt(hit.point + hit.normal * 0.02f, hit.normal);
                }
                else
                {
                    PlaceAt(holder.transform.position + holder.transform.forward * 0.8f, Vector3.up);
                }
            }
        }

        private void PlaceAt(Vector3 position, Vector3 normal)
        {
            held = false;
            holder = null;
            transform.SetPositionAndRotation(position,
                Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            if (solid != null) solid.enabled = true;
            if (gripZone != null) gripZone.enabled = true;
        }

        private void OnGUI()
        {
            if (!held) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 14, fontStyle = FontStyle.Bold };
            GUI.Box(new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.66f, 440f, 30f),
                $"[{(holder != null ? holder.Key : KeyCode.E)}] colocar la espuma — pisa cerca y no resbalas", style);
        }
    }

    /// <summary>The pad's grip aura: refreshes the player's slide immunity while inside.</summary>
    public class FoamPadGrip : MonoBehaviour
    {
        private void OnTriggerStay(Collider other)
        {
            var loco = other.GetComponent<PlayerLocomotion>();
            if (loco != null) loco.GripAssist();
        }
    }
}
