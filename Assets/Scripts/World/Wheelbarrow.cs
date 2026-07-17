using UnityEngine;
using RoofingSimulator.Gameplay;
using RoofingSimulator.Player;

namespace RoofingSimulator.World
{
    /// <summary>
    /// The physical debris wheelbarrow (Etapa 5). Debris that falls into the tray is caught
    /// automatically (up to 30 units) — park it under the eave and let the tear-off rain in.
    /// Press E while carrying debris to tip your hands into it. Press E with free hands to
    /// GRAB it: it rolls along in front of you as a real rigidbody (the wheel spins, it
    /// collides, and tipping it far over spills part of the load). Push it next to the
    /// dumpster and press E to empty it 6 debris at a time (counts toward cleanliness);
    /// E away from the dumpster lets go.
    /// </summary>
    public class Wheelbarrow : MonoBehaviour, IInteractable
    {
        public const int Capacity = 30;
        private const int UnloadBatch = 6;
        private const float FollowDistance = 1.5f;
        private const float MaxHoldDistance = 4.5f;
        private const float DumpsterRange = 2.8f;
        private const float WheelRadius = 0.18f;

        private Rigidbody rb;
        private Transform dumpster;
        private Transform wheel;
        private readonly System.Collections.Generic.List<GameObject> pile =
            new System.Collections.Generic.List<GameObject>();
        private int load;

        private bool grabbed;
        private PlayerInteractor holder;
        private int grabFrame;         // so the E press that grabbed doesn't instantly act again
        private int releaseFrame = -1; // so the E press that released doesn't instantly re-grab
        private float nextSpillTime;
        private float nextCatchTime;   // brief immunity after a spill so it doesn't re-eat it

        // ----- Spawning -----

        public static Wheelbarrow Spawn(Vector3 position, Transform dumpsterRoot)
        {
            var root = new GameObject("Wheelbarrow");
            root.transform.position = position + Vector3.up * 0.05f;

            var barrow = root.AddComponent<Wheelbarrow>();
            barrow.dumpster = dumpsterRoot;
            barrow.BuildVisual();

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.42f, 0.02f);
            box.size = new Vector3(0.72f, 0.55f, 1.15f);

            barrow.rb = root.AddComponent<Rigidbody>();
            barrow.rb.mass = 14f;
            barrow.rb.linearDamping = 0.8f;
            barrow.rb.angularDamping = 2.5f;
            barrow.rb.centerOfMass = new Vector3(0f, 0.12f, 0f); // low, so it doesn't tip on a whim

            // Open tray volume that swallows debris chunks falling in.
            var catcher = new GameObject("Catcher");
            catcher.transform.SetParent(root.transform, false);
            catcher.transform.localPosition = new Vector3(0f, 0.66f, 0.05f);
            var trig = catcher.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.size = new Vector3(0.55f, 0.35f, 0.8f);
            catcher.AddComponent<WheelbarrowCatcher>().Init(barrow);

            return barrow;
        }

        private void BuildVisual()
        {
            Color tub = new Color(0.36f, 0.40f, 0.44f);
            Color dark = new Color(0.10f, 0.10f, 0.11f);
            Color wood = new Color(0.55f, 0.42f, 0.28f);

            // Tray: floor plus four outward-tilted panels.
            Piece(new Vector3(0f, 0.36f, 0.02f), Vector3.zero, new Vector3(0.5f, 0.05f, 0.8f), tub, true);
            Piece(new Vector3(0f, 0.50f, 0.46f), new Vector3(-24f, 0f, 0f), new Vector3(0.52f, 0.30f, 0.04f), tub, true);  // front
            Piece(new Vector3(0f, 0.50f, -0.42f), new Vector3(24f, 0f, 0f), new Vector3(0.52f, 0.30f, 0.04f), tub, true);  // back
            Piece(new Vector3(0.30f, 0.50f, 0.02f), new Vector3(0f, 0f, 24f), new Vector3(0.04f, 0.30f, 0.86f), tub, true);  // right
            Piece(new Vector3(-0.30f, 0.50f, 0.02f), new Vector3(0f, 0f, -24f), new Vector3(0.04f, 0.30f, 0.86f), tub, true); // left

            // Front wheel (axis across X; spun by movement), legs and wooden handles.
            var w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            w.name = "Wheel";
            var wc = w.GetComponent<Collider>();
            if (wc != null) Destroy(wc);
            w.transform.SetParent(transform, false);
            w.transform.localPosition = new Vector3(0f, WheelRadius, 0.52f);
            w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            w.transform.localScale = new Vector3(WheelRadius * 2f, 0.05f, WheelRadius * 2f);
            PaintPlain(w, dark);
            wheel = w.transform;

            Piece(new Vector3(0.20f, 0.14f, -0.32f), Vector3.zero, new Vector3(0.05f, 0.28f, 0.05f), dark, false);
            Piece(new Vector3(-0.20f, 0.14f, -0.32f), Vector3.zero, new Vector3(0.05f, 0.28f, 0.05f), dark, false);
            Piece(new Vector3(0.24f, 0.48f, -0.66f), new Vector3(12f, 0f, 0f), new Vector3(0.045f, 0.045f, 0.55f), wood, false);
            Piece(new Vector3(-0.24f, 0.48f, -0.66f), new Vector3(12f, 0f, 0f), new Vector3(0.045f, 0.045f, 0.55f), wood, false);

            // The load: up to 5 visible layers (one per 6 debris), toggled as it fills.
            for (int i = 0; i < 5; i++)
            {
                var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slab.name = "PileLayer";
                var sc = slab.GetComponent<Collider>();
                if (sc != null) Destroy(sc);
                slab.transform.SetParent(transform, false);
                slab.transform.localPosition = new Vector3(0f, 0.42f + i * 0.065f, 0.02f);
                slab.transform.localRotation = Quaternion.Euler(0f, Random.Range(-9f, 9f), 0f);
                slab.transform.localScale = new Vector3(0.44f, 0.06f, 0.72f);
                var r = slab.GetComponent<Renderer>();
                if (r != null)
                {
                    var m = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                    { color = new Color(0.30f, 0.28f, 0.27f) * Random.Range(0.9f, 1.1f) };
                    m.mainTexture = RoofTextureLibrary.Get(RoofTextureLibrary.Surface.OldShingle);
                    m.mainTextureScale = new Vector2(2f, 2f);
                    r.sharedMaterial = m;
                }
                slab.SetActive(false);
                pile.Add(slab);
            }
        }

        private void Piece(Vector3 localPos, Vector3 euler, Vector3 scale, Color color, bool metal)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Part";
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c); // the root box collider handles physics
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            if (metal)
            {
                var r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    var m = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default")) { color = color };
                    m.mainTexture = RoofTextureLibrary.Get(RoofTextureLibrary.Surface.Metal);
                    m.mainTextureScale = new Vector2(2f, 2f);
                    if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.4f);
                    r.sharedMaterial = m;
                }
            }
            else PaintPlain(go, color);
        }

        private static void PaintPlain(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                { color = color };
        }

        private void UpdatePile()
        {
            int layers = Mathf.CeilToInt(load / (float)UnloadBatch);
            for (int i = 0; i < pile.Count; i++)
                if (pile[i] != null && pile[i].activeSelf != (i < layers)) pile[i].SetActive(i < layers);
            if (rb != null) rb.mass = 14f + load * 0.4f; // a full barrow pushes heavier
        }

        // ----- Catching debris that falls in -----

        /// <summary>Called by the tray trigger: swallow debris chunks that land inside.</summary>
        public void TryCatch(Collider other)
        {
            if (load >= Capacity || Time.time < nextCatchTime) return;
            Debris d = other != null ? other.GetComponentInParent<Debris>() : null;
            if (d == null) return;
            load++;
            UpdatePile();
            Destroy(d.gameObject);
        }

        // ----- Interaction (E) -----

        private bool NearDumpster =>
            dumpster != null && Vector3.Distance(transform.position, dumpster.position) <= DumpsterRange;

        public string Prompt =>
            load > 0 && NearDumpster
                ? $"Vaciar carretilla al contenedor ({load}/{Capacity}, de a {UnloadBatch})"
                : $"Carretilla ({load}/{Capacity}) — empujar / echar escombros";

        public bool CanInteract(PlayerInteractor interactor)
            => !grabbed && Time.frameCount != releaseFrame;

        public void Interact(PlayerInteractor interactor)
        {
            if (grabbed) return;
            PlayerMaterials mats = interactor.Materials;

            // Hands full of debris? Tip them into the tray.
            if (mats != null && mats.CarryingDebris && load < Capacity)
            {
                load += mats.TransferDebris(Capacity - load);
                UpdatePile();
                return;
            }

            // Parked at the dumpster? Empty a batch.
            if (load > 0 && NearDumpster)
            {
                Unload(mats);
                return;
            }

            grabbed = true;
            holder = interactor;
            grabFrame = Time.frameCount;
        }

        private void Unload(PlayerMaterials mats)
        {
            int n = Mathf.Min(UnloadBatch, load);
            load -= n;
            UpdatePile();
            mats?.CreditDumpedDebris(n);
        }

        // ----- Pushing (grabbed) -----

        private void Update()
        {
            // Wheel spins with ground speed so the push reads as rolling.
            if (wheel != null && rb != null)
            {
                Vector3 v = rb.linearVelocity;
                v.y = 0f;
                float dir = Vector3.Dot(v, transform.forward) >= 0f ? 1f : -1f;
                wheel.Rotate(0f, dir * v.magnitude * Time.deltaTime / WheelRadius * Mathf.Rad2Deg, 0f, Space.Self);
            }

            // Tipped right over with a load? Part of it spills out (physical comedy tax).
            if (load > 0 && Time.time >= nextSpillTime && Vector3.Angle(transform.up, Vector3.up) > 75f)
                Spill();

            if (!grabbed) return;
            PlayerInteractor.ClaimInteraction(); // we own E while pushing

            if (holder == null
                || Vector3.Distance(holder.transform.position, transform.position) > MaxHoldDistance)
            {
                Release();
                return;
            }
            if (Time.frameCount != grabFrame && UnityEngine.Input.GetKeyDown(holder.Key))
            {
                // At the dumpster the press empties a batch and you keep pushing; anywhere else it lets go.
                if (load > 0 && NearDumpster) Unload(holder.Materials);
                else Release();
            }
        }

        private void Release()
        {
            grabbed = false;
            releaseFrame = Time.frameCount;
        }

        private void FixedUpdate()
        {
            if (!grabbed || holder == null || rb == null) return;

            Vector3 fwd = holder.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) return;
            fwd.Normalize();

            // Chase a point in front of the player, keeping gravity's vertical velocity.
            Vector3 target = holder.transform.position + fwd * FollowDistance;
            Vector3 to = target - rb.position;
            to.y = 0f;
            Vector3 vel = Vector3.ClampMagnitude(to * 5f, 3.2f);
            rb.linearVelocity = new Vector3(vel.x, rb.linearVelocity.y, vel.z);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(fwd), 8f * Time.fixedDeltaTime));
        }

        private void Spill()
        {
            nextSpillTime = Time.time + 1.2f;
            nextCatchTime = Time.time + 0.8f; // don't instantly re-eat what just fell out
            int n = Mathf.Min(UnloadBatch, load);
            load -= n;
            UpdatePile();

            // Toss the chunks out the side it's leaning toward.
            Vector3 lean = transform.up;
            lean.y = 0f;
            if (lean.sqrMagnitude < 0.01f) lean = Random.insideUnitSphere;
            lean.Normalize();
            for (int i = 0; i < n; i++)
                Debris.Spawn(transform.position + lean * 0.8f + Vector3.up * 0.5f
                        + Random.insideUnitSphere * 0.25f,
                    new Vector3(0.5f, 0.06f, 0.4f), new Color(0.30f, 0.28f, 0.27f));
        }

        private void OnGUI()
        {
            if (!grabbed) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 14, fontStyle = FontStyle.Bold };
            string tip = load > 0 && NearDumpster
                ? $"[{(holder != null ? holder.Key : KeyCode.E)}] vaciar {UnloadBatch} al contenedor"
                : $"[{(holder != null ? holder.Key : KeyCode.E)}] soltar";
            GUI.Box(new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.66f, 440f, 30f),
                $"Empujando carretilla ({load}/{Capacity}) · {tip}", style);
        }
    }

    /// <summary>Forwards the tray trigger's hits to its <see cref="Wheelbarrow"/>.</summary>
    public class WheelbarrowCatcher : MonoBehaviour
    {
        private Wheelbarrow owner;
        public void Init(Wheelbarrow o) => owner = o;
        private void OnTriggerEnter(Collider other) => owner?.TryCatch(other);
    }
}
