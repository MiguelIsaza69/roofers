using UnityEngine;
using RoofingSimulator.Player;
using RoofingSimulator.Gameplay;

namespace RoofingSimulator.World
{
    /// <summary>
    /// A load the player set down in the world (with G). It's built at the real size of the
    /// material — a stack of actual-size shingles, a plywood plank, a felt roll, or tile-size
    /// debris chunks — and you look at it and press E to pick it back up. Dropping never loses
    /// anything.
    /// </summary>
    public class DroppedMaterial : MonoBehaviour, IInteractable
    {
        private CarryKind kind;
        private int amount;
        private float plankLength; // cut planks keep their measure on the ground (Etapa 6)

        public string Prompt => $"Recoger {Label()}";

        public bool CanInteract(PlayerInteractor interactor)
            => interactor.Materials != null && interactor.Materials.CanTake(kind);

        public void Interact(PlayerInteractor interactor)
        {
            if (interactor.Materials.TryTake(kind, amount, plankLength))
            {
                Destroy(gameObject);
            }
        }

        public static DroppedMaterial Spawn(Vector3 position, CarryKind kind, int amount,
            float tileUp, float tileSide, float plankLength = 0f)
        {
            var root = new GameObject($"Dropped_{kind}");
            root.transform.position = position;

            Vector3 colliderSize = BuildVisual(root.transform, kind, amount, tileUp, tileSide, plankLength);

            var box = root.AddComponent<BoxCollider>();
            box.size = colliderSize;
            box.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = Mathf.Clamp(amount * 0.4f, 0.5f, 8f);

            // Dropped on the staging tarp? Snap to a neat slot/stack (Etapa 7).
            if (StagingArea.TrySnap(root.transform, colliderSize))
            {
                rb.isKinematic = true;
            }
            // Loads set down ON the roof rest flush and stay put (staged like real bundles,
            // Etapa 4) — otherwise the slope would tumble them straight off.
            else if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 4f,
                    ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<RoofingSimulator.Gameplay.RoofCell>() != null)
            {
                root.transform.SetPositionAndRotation(
                    hit.point + hit.normal * 0.02f,
                    Quaternion.FromToRotation(Vector3.up, hit.normal));
                rb.isKinematic = true;
            }

            var dropped = root.AddComponent<DroppedMaterial>();
            dropped.kind = kind;
            dropped.amount = amount;
            dropped.plankLength = plankLength;
            return dropped;
        }

        /// <summary>Builds child meshes at real size; returns an approximate collider size.</summary>
        private static Vector3 BuildVisual(Transform parent, CarryKind kind, int amount,
            float tileUp, float tileSide, float plankLength = 0f)
        {
            switch (kind)
            {
                case CarryKind.Shingles:
                {
                    // A small stack of real shingle bundles (≈1.0 × 0.32 m wrapped packs, like on site).
                    const float bw = 1.0f, bd = 0.32f, bh = 0.09f;
                    int n = Mathf.Clamp(Mathf.CeilToInt(amount / 3f), 1, 5);
                    for (int i = 0; i < n; i++)
                    {
                        Piece(parent, new Vector3(Random.Range(-0.03f, 0.03f), bh * 0.5f + i * bh, Random.Range(-0.02f, 0.02f)),
                            new Vector3(bw, bh, bd), new Color(0.22f, 0.42f, 0.58f),
                            RoofTextureLibrary.Surface.AsphaltShingle);
                    }
                    return new Vector3(bw, n * bh + 0.04f, bd);
                }
                case CarryKind.Plank:
                {
                    // A real plywood sheet (1.2 m wide), at its cut length (full sheet = 2.4 m).
                    float len = plankLength > 0f ? plankLength : 2.4f;
                    Piece(parent, new Vector3(0f, 0.05f, 0f),
                        new Vector3(1.2f, 0.06f, len), new Color(0.74f, 0.58f, 0.36f),
                        RoofTextureLibrary.Surface.WoodDeck);
                    return new Vector3(1.2f, 0.12f, len);
                }
                case CarryKind.FeltRoll:
                {
                    // A real roll of felt (~1 m long).
                    var roll = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    roll.transform.SetParent(parent, false);
                    roll.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                    roll.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    roll.transform.localScale = new Vector3(0.6f, 0.55f, 0.6f);
                    StripCollider(roll);
                    Paint(roll, new Color(0.16f, 0.16f, 0.20f), RoofTextureLibrary.Surface.Felt);
                    return new Vector3(1.2f, 0.6f, 0.6f);
                }
                default: // Debris — tile-size chunks
                {
                    int n = Mathf.Clamp(amount, 1, 8);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 off = new Vector3(
                            Random.Range(-0.2f, 0.2f), 0.1f + i * 0.07f, Random.Range(-0.2f, 0.2f));
                        var chunk = Piece(parent, off,
                            new Vector3(tileUp * 0.9f, 0.06f, tileSide * 0.9f), new Color(0.30f, 0.28f, 0.27f),
                            RoofTextureLibrary.Surface.OldShingle);
                        chunk.transform.localRotation = Random.rotation;
                    }
                    return new Vector3(tileUp + 0.3f, 0.5f, tileSide + 0.3f);
                }
            }
        }

        private static GameObject Piece(Transform parent, Vector3 localPos, Vector3 scale, Color color,
            RoofTextureLibrary.Surface surface)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            StripCollider(go);
            Paint(go, color, surface);
            return go;
        }

        private static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // the root BoxCollider handles physics & interaction
        }

        private static void Paint(GameObject go, Color color, RoofTextureLibrary.Surface surface)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;

            var m = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default")) { color = color };
            m.mainTexture = RoofTextureLibrary.Get(surface);
            Vector3 sc = go.transform.localScale;
            m.mainTextureScale = new Vector2(
                Mathf.Max(0.5f, Mathf.Abs(sc.x) * 2.5f),
                Mathf.Max(0.5f, Mathf.Abs(sc.z) * 2.5f));
            r.sharedMaterial = m;
        }

        private string Label() => kind switch
        {
            CarryKind.Shingles => $"tejas ×{amount}",
            CarryKind.Plank => $"plancha de madera ({(plankLength > 0f ? plankLength : 2.4f):0.0} m)",
            CarryKind.FeltRoll => $"rollo de fieltro ({amount})",
            CarryKind.Debris => $"escombros ×{amount}",
            _ => "material"
        };
    }
}
