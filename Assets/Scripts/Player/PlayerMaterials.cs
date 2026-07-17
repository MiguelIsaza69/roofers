using UnityEngine;

namespace RoofingSimulator.Player
{
    /// <summary>The three roofing materials, each restocked at its own station.</summary>
    public enum MaterialKind { Wood, Felt, Shingles }

    /// <summary>What the player is currently holding in their two hands.</summary>
    public enum CarryKind { None, Shingles, Plank, FeltRoll, Debris }

    /// <summary>
    /// Two-handed carry: the player holds ONE thing at a time — a pack of shingles, a wood
    /// plank, a felt roll, or debris. The matching tool spends from what's in hand (nail gun
    /// needs shingles, hammer needs a plank, roller needs the roll). Run out and you go back
    /// to that material's station. Drop the current load with G. Carrying anything slows you
    /// down; carrying debris also blocks tool use until you dump it.
    /// </summary>
    public class PlayerMaterials : MonoBehaviour
    {
        [Header("Capacities")]
        [Tooltip("Shingles per pack you pick up.")]
        [SerializeField] private int shinglesPerPack = 6;
        [Tooltip("Planks you can carry at once (one plank repairs one 2×3 block).")]
        [SerializeField] private int plankCells = 1;
        [Tooltip("Cells one felt roll can cover (the roller lays 2 at a time).")]
        [SerializeField] private int feltRollCells = 20;
        [SerializeField] private int maxDebris = 6;
        [SerializeField] private KeyCode dropKey = KeyCode.G;

        public CarryKind Carrying { get; private set; } = CarryKind.None;
        /// <summary>Units left of whatever is in hand (shingles / deck cells / felt cells / debris).</summary>
        public int Amount { get; private set; }

        /// <summary>A full plywood sheet as it comes off the stack (real 4×8 ≈ 2.4 m long).</summary>
        public const float FullSheetLength = 2.4f;
        /// <summary>Length of the plank in hand — holes need a matching cut (Etapa 6).</summary>
        public float PlankLength { get; private set; } = FullSheetLength;

        public bool CarryingDebris => Carrying == CarryKind.Debris && Amount > 0;
        // Anything else in hand is set down first (swap) — only a full debris load refuses more.
        public bool CanPickDebris =>
            Carrying != CarryKind.Debris || Amount < maxDebris;

        /// <summary>0..1 weight of what's in hand — using material up makes you lighter and faster.</summary>
        public float CarryLoad => Mathf.Clamp01(CarryWeight / 10f);

        private float CarryWeight => Carrying switch
        {
            CarryKind.Shingles => Amount * 1.0f,
            CarryKind.Plank => Amount * 6.0f,
            CarryKind.FeltRoll => Amount * 0.5f,
            CarryKind.Debris => Amount * 1.0f,
            _ => 0f
        };

        private int debrisSpawned, debrisDumped;
        /// <summary>0..1 — how much torn-off debris actually reached the dumpster.</summary>
        public float Cleanliness => debrisSpawned == 0 ? 1f : Mathf.Clamp01((float)debrisDumped / debrisSpawned);

        // Real tile size, so dropped shingles/debris match the roof (set by the scene).
        private Vector2 tileSize = new Vector2(0.6f, 1.0f);
        public void SetTileSize(float up, float side) => tileSize = new Vector2(up, side);

        private PlayerLocomotion locomotion;

        private void Awake() => locomotion = GetComponent<PlayerLocomotion>();

        private void Update()
        {
            if (locomotion != null) locomotion.SetCarryLoad(CarryLoad);
            if (UnityEngine.Input.GetKeyDown(dropKey)) Drop();
        }

        // ----- Loading at stations -----

        // Material budget (Fase E3): units actually taken from the supply stations. Picking
        // dropped loads or reusing offcuts is free — only the pallet restock counts.
        private readonly int[] restocked = new int[3]; // indexed by MaterialKind
        public int Restocked(MaterialKind kind) => restocked[(int)kind];

        /// <summary>
        /// Pick up a material at its station. If the hands hold something different, that
        /// load is set down as a world pickup first (a swap — nothing is ever deleted).
        /// </summary>
        public bool TryLoad(MaterialKind kind)
        {
            CarryKind ck = Map(kind);
            if (Carrying != CarryKind.None && Carrying != ck) Drop();
            int before = Carrying == ck ? Amount : 0;
            Carrying = ck;
            Amount = Capacity(kind);
            if (Amount > before) restocked[(int)kind] += Amount - before; // E3: only the top-up counts
            if (ck == CarryKind.Plank) PlankLength = FullSheetLength; // fresh full sheet
            return true;
        }

        /// <summary>The saw trims the carried sheet down to the hole's measure (Etapa 6).</summary>
        public void CutPlank(float newLength)
        {
            if (Carrying != CarryKind.Plank) return;
            PlankLength = Mathf.Max(0.3f, newLength);
        }

        /// <summary>Spend <paramref name="count"/> units of the required carry kind for a tool step.</summary>
        public bool TryUse(CarryKind needed, int count = 1)
        {
            if (Carrying != needed || Amount < count) return false;
            Amount -= count;
            if (Amount <= 0) { Amount = 0; Carrying = CarryKind.None; }
            return true;
        }

        // ----- Debris -----

        public bool TryLoadDebris()
        {
            if (!CanPickDebris) return false;
            if (Carrying != CarryKind.None && Carrying != CarryKind.Debris) Drop(); // swap, never delete
            Carrying = CarryKind.Debris;
            Amount += 1;
            return true;
        }

        public int DumpDebris()
        {
            if (Carrying != CarryKind.Debris) return 0;
            int n = Amount;
            debrisDumped += n;
            Amount = 0;
            Carrying = CarryKind.None;
            return n;
        }

        /// <summary>Move carried debris into a container (the wheelbarrow). No cleanliness credit
        /// yet — that waits until the container is emptied at the dumpster.</summary>
        public int TransferDebris(int maxCount)
        {
            if (Carrying != CarryKind.Debris || maxCount <= 0) return 0;
            int n = Mathf.Min(Amount, maxCount);
            Amount -= n;
            if (Amount <= 0) { Amount = 0; Carrying = CarryKind.None; }
            return n;
        }

        /// <summary>Debris reached the dumpster via a container (wheelbarrow) — count it clean.</summary>
        public void CreditDumpedDebris(int n) => debrisDumped += Mathf.Max(0, n);

        public void NotifyDebrisSpawned() => debrisSpawned++;

        /// <summary>
        /// A scene system can claim the drop before the generic pickup spawns — e.g. the roof
        /// grid deploys a carried felt roll onto the aimed strip (Etapa 3). Return true to
        /// consume the load.
        /// </summary>
        public System.Func<CarryKind, int, bool> DropOverride { get; set; }

        /// <summary>Set the current load down in the world as a pickup (G) — it isn't lost.</summary>
        public void Drop()
        {
            if (Carrying == CarryKind.None || Amount <= 0) return;
            if (DropOverride != null && DropOverride(Carrying, Amount))
            {
                Carrying = CarryKind.None;
                Amount = 0;
                return;
            }
            World.DroppedMaterial.Spawn(
                transform.position + transform.forward * 1.1f + Vector3.up * 0.5f,
                Carrying, Amount, tileSize.x, tileSize.y,
                Carrying == CarryKind.Plank ? PlankLength : 0f);
            Carrying = CarryKind.None;
            Amount = 0;
        }

        /// <summary>Whether a dropped load of this kind can be picked up right now (always: a
        /// different load in hand is swapped down, never deleted).</summary>
        public bool CanTake(CarryKind kind) => true;

        /// <summary>Pick a dropped load back into the hands (same kind merges; different kind swaps;
        /// planks never merge — each keeps its own cut length).</summary>
        public bool TryTake(CarryKind kind, int amount, float plankLength = 0f)
        {
            if (Carrying != CarryKind.None && (Carrying != kind || kind == CarryKind.Plank)) Drop();
            Carrying = kind;
            Amount += amount;
            if (kind == CarryKind.Plank && plankLength > 0f) PlankLength = plankLength;
            return true;
        }

        public string CarryLabel => Carrying switch
        {
            CarryKind.Shingles => $"Tejas ×{Amount}",
            CarryKind.Plank => $"Plancha de madera ({PlankLength:0.0} m)",
            CarryKind.FeltRoll => $"Rollo de fieltro ({Amount} restantes)",
            CarryKind.Debris => $"Escombros ×{Amount}",
            _ => "Manos libres"
        };

        private static CarryKind Map(MaterialKind kind) => kind switch
        {
            MaterialKind.Wood => CarryKind.Plank,
            MaterialKind.Felt => CarryKind.FeltRoll,
            _ => CarryKind.Shingles
        };

        private int Capacity(MaterialKind kind) => kind switch
        {
            MaterialKind.Wood => plankCells,
            MaterialKind.Felt => feltRollCells,
            _ => shinglesPerPack
        };
    }
}
