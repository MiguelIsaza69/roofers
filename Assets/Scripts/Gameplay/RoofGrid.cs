using System;
using System.Collections.Generic;
using UnityEngine;
using RoofingSimulator.Input;
using RoofingSimulator.Player;
using RoofingSimulator.World;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// Tiles a roof face into rectangular shingle cells and drives the per-cell loop. On
    /// the player's apply input it finds the cell under the crosshair, checks the right
    /// tool and that the matching material is available, advances it, and:
    ///   • tear-off spawns a tile-sized debris chunk,
    ///   • the felt roller also covers the next cell ALONG the course (felt runs horizontally),
    ///   • nailing scores precision.
    /// Some cells have undamaged decks (no hammer needed). Raises <see cref="OnRoofComplete"/>
    /// when every cell is shingled.
    /// </summary>
    public class RoofGrid : MonoBehaviour
    {
        [Tooltip("Fraction of 2×3 deck blocks that are damaged and must be replaced.")]
        [SerializeField] private float deckDamageChance = 0.35f;

        /// <summary>Contract knob (Fase D): how rotten this roof is. Call before <see cref="Build"/>.</summary>
        public void SetDeckDamageChance(float chance) => deckDamageChance = Mathf.Clamp01(chance);

        [Tooltip("Seconds between repeats while the apply button is held — lets you sweep a tool (the felt roller especially) without re-clicking.")]
        [SerializeField] private float holdRepeatInterval = 0.10f;
        private float nextHeldApplyTime;

        private const int DeckBlockCols = 2; // tiles up the slope per plank
        private const int DeckBlockRows = 3; // tiles across the roof per plank

        private readonly List<RoofCell> cells = new List<RoofCell>();
        private RoofCell[,] grid;
        private int rows, cols;
        private float[,] blockPlankLength; // plank measure each damaged block needs (Etapa 6)
        private bool[,] obstructedMask;    // cells a chimney/skylight occupies (Etapa D3)

        // Finishing aids from the extra tools (see Gameplay.ToolFunctions): chalked courses,
        // felt pinned by the cap nailer, and felt trimmed by the knife — each adds a quality
        // bonus; shingles placed out of stair-step order lose some.
        private Transform anchor;
        private Vector2 faceSize;
        private float cellDepth;
        private float cellWidth;
        private bool[] chalkedCourse;
        private readonly HashSet<RoofCell> secured = new HashSet<RoofCell>();
        private readonly HashSet<RoofCell> trimmed = new HashSet<RoofCell>();
        private readonly HashSet<RoofCell> outOfOrder = new HashSet<RoofCell>();

        private PlayerInput playerInput;
        private RoofingToolbelt toolbelt;
        private PlayerMaterials materials;
        private int completed;
        private float qualitySum;

        public int Total => cells.Count;
        public int Completed => completed;
        /// <summary>Columns run up the slope (col 0 at the ridge); each column is one felt course.</summary>
        public int Cols => cols;
        /// <summary>Rows run across the roof; the deployable felt roll travels them along a course.</summary>
        public int Rows => rows;
        public float CompletionPercent => cells.Count == 0 ? 0f : 100f * completed / cells.Count;
        public float AverageQuality => completed == 0 ? 1f : qualitySum / completed;

        /// <summary>Real tile size the last <see cref="Build"/> produced (up the slope / across) — used to size dropped materials.</summary>
        public float CellUp { get; private set; }
        public float CellSide { get; private set; }

        /// <summary>Average snow depth across the roof, 0..1 (Fase C, Etapa C2).</summary>
        public float AverageSnow { get; private set; }

        /// <summary>Weather hook: accumulate on every cell while it snows, melt once it stops.
        /// Both amounts are this frame's delta (either may be zero).</summary>
        public void TickSnow(float accumulate, float melt)
        {
            if (cells.Count == 0) return;
            float sum = 0f;
            for (int i = 0; i < cells.Count; i++)
            {
                RoofCell cell = cells[i];
                if (cell == null) continue;
                if (accumulate > 0f) cell.AddSnow(accumulate);
                if (melt > 0f) cell.AddSnow(-melt);
                sum += cell.SnowDepth;
            }
            AverageSnow = sum / cells.Count;
        }

        /// <summary>Raised once when the final cell is finished.</summary>
        public event Action OnRoofComplete;

        /// <summary>
        /// Tile the face into a FIXED grid of <paramref name="tilesUpSlope"/> ×
        /// <paramref name="tilesAcross"/> cells. Each tile is sized to fill the face exactly,
        /// so a bigger roof just means bigger tiles — never more of them.
        /// <paramref name="keepCell"/> (Etapa D5, hip roofs): cells where it returns false
        /// never get built — they count as obstructed, like the D3 chimney footprint.
        /// </summary>
        public void Build(Transform faceAnchor, Vector2 size, int tilesUpSlope, int tilesAcross,
            Func<int, int, bool> keepCell = null)
        {
            foreach (var c in cells) if (c != null) Destroy(c.gameObject);
            cells.Clear();
            completed = 0;
            qualitySum = 0f;

            cols = Mathf.Max(1, tilesUpSlope);  // up the slope (vertical)
            rows = Mathf.Max(1, tilesAcross);   // across the roof (horizontal)
            grid = new RoofCell[rows, cols];
            obstructedMask = new bool[rows, cols];
            float cellW = size.x / cols;
            float cellD = size.y / rows;
            CellUp = cellW;
            CellSide = cellD;

            anchor = faceAnchor;
            faceSize = size;
            cellDepth = cellD;
            cellWidth = cellW;
            chalkedCourse = new bool[cols];
            secured.Clear();
            trimmed.Clear();
            outOfOrder.Clear();
            feltRolls.Clear();

            // Deck damage is decided per 2×3 block (2 up the slope, 3 across). Each damaged
            // block wants a plank of a MIXED size (Etapa 6): some take the full 4×8 sheet,
            // others need it measured and cut down first.
            int bRows = Mathf.CeilToInt(rows / (float)DeckBlockRows);
            int bCols = Mathf.CeilToInt(cols / (float)DeckBlockCols);
            var blockDamaged = new bool[bRows, bCols];
            blockPlankLength = new float[bRows, bCols];
            for (int br = 0; br < bRows; br++)
                for (int bc = 0; bc < bCols; bc++)
                {
                    blockDamaged[br, bc] = UnityEngine.Random.value < deckDamageChance;
                    if (!blockDamaged[br, bc]) continue;
                    float roll = UnityEngine.Random.value;
                    blockPlankLength[br, bc] = roll < 0.4f ? PlayerMaterials.FullSheetLength
                        : roll < 0.7f ? 1.8f : 1.2f;
                }

            // A damaged block only shows the rot on part of itself (a stain), but the whole
            // block still has to be replaced — the coloured border shows how far it reaches.
            var stained = ComputeStains(blockDamaged, bRows, bCols, rows, cols);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // Hip cut (Etapa D5): the cell is outside the face polygon — it never
                    // exists. Marked obstructed so the stair-step order flows around it.
                    if (keepCell != null && !keepCell(r, c))
                    {
                        obstructedMask[r, c] = true;
                        continue;
                    }

                    float x = -size.x * 0.5f + (c + 0.5f) * cellW;
                    float z = -size.y * 0.5f + (r + 0.5f) * cellD;

                    var go = new GameObject($"Cell_{r}_{c}");
                    go.transform.SetParent(faceAnchor, false);
                    go.transform.localPosition = new Vector3(x, 0f, z);
                    go.transform.localRotation = Quaternion.identity;

                    var box = go.AddComponent<BoxCollider>();
                    box.size = new Vector3(cellW, 0.18f, cellD);
                    box.center = new Vector3(0f, 0.07f, 0f);

                    var cell = go.AddComponent<RoofCell>();
                    bool damaged = blockDamaged[r / DeckBlockRows, c / DeckBlockCols];
                    cell.Setup(r, c, cellW, cellD, damaged, stained[r, c], c == cols - 1); // last col = eave course
                    cells.Add(cell);
                    grid[r, c] = cell;
                }
            }

            // Damaged blocks that still have cells (hip cuts may empty one) — this is the
            // plank count the material budget covers (Fase E3).
            DamagedBlocks = 0;
            for (int br = 0; br < bRows; br++)
            {
                for (int bc = 0; bc < bCols; bc++)
                {
                    if (!blockDamaged[br, bc]) continue;
                    bool any = false;
                    for (int r = br * DeckBlockRows; r < Mathf.Min((br + 1) * DeckBlockRows, rows) && !any; r++)
                        for (int c = bc * DeckBlockCols; c < Mathf.Min((bc + 1) * DeckBlockCols, cols) && !any; c++)
                            if (grid[r, c] != null) any = true;
                    if (any) DamagedBlocks++;
                }
            }

            BuildBlockBorders(faceAnchor, size, cellW, cellD, bRows, bCols);
        }

        /// <summary>Damaged 2×3 blocks with live cells — each needs one plank (Fase E3).</summary>
        public int DamagedBlocks { get; private set; }

        private static readonly Color[] BlockColors =
        {
            new Color(0.90f, 0.22f, 0.22f), // red
            new Color(0.24f, 0.45f, 0.92f), // blue
            new Color(0.62f, 0.25f, 0.80f), // purple
            new Color(0.26f, 0.72f, 0.34f)  // green
        };

        /// <summary>Picks which cells of each damaged block show the rot stain (at least one).</summary>
        private static bool[,] ComputeStains(bool[,] blockDamaged, int bRows, int bCols, int rows, int cols)
        {
            var stained = new bool[rows, cols];
            for (int br = 0; br < bRows; br++)
            {
                for (int bc = 0; bc < bCols; bc++)
                {
                    if (!blockDamaged[br, bc]) continue;
                    int r0 = br * DeckBlockRows, r1 = Mathf.Min(r0 + DeckBlockRows, rows);
                    int c0 = bc * DeckBlockCols, c1 = Mathf.Min(c0 + DeckBlockCols, cols);

                    stained[UnityEngine.Random.Range(r0, r1), UnityEngine.Random.Range(c0, c1)] = true;
                    for (int r = r0; r < r1; r++)
                        for (int c = c0; c < c1; c++)
                            if (UnityEngine.Random.value < 0.25f) stained[r, c] = true;
                }
            }
            return stained;
        }

        /// <summary>Draws a thin coloured frame around each 2×3 block so its extent is clear.</summary>
        private void BuildBlockBorders(Transform anchor, Vector2 size, float cellW, float cellD, int bRows, int bCols)
        {
            for (int br = 0; br < bRows; br++)
            {
                for (int bc = 0; bc < bCols; bc++)
                {
                    int r0 = br * DeckBlockRows, r1 = Mathf.Min(r0 + DeckBlockRows, rows);
                    int c0 = bc * DeckBlockCols, c1 = Mathf.Min(c0 + DeckBlockCols, cols);

                    // No frame for blocks the hip cut emptied out (Etapa D5).
                    bool anyCell = false;
                    for (int r = r0; r < r1 && !anyCell; r++)
                        for (int c = c0; c < c1 && !anyCell; c++)
                            if (grid[r, c] != null) anyCell = true;
                    if (!anyCell) continue;

                    const float inset = 0.03f; // pull the frame inside the block so neighbours don't overlap
                    float xMin = -size.x * 0.5f + c0 * cellW + inset;
                    float xMax = -size.x * 0.5f + c1 * cellW - inset;
                    float zMin = -size.y * 0.5f + r0 * cellD + inset;
                    float zMax = -size.y * 0.5f + r1 * cellD - inset;
                    float cx = (xMin + xMax) * 0.5f, cz = (zMin + zMax) * 0.5f;
                    float w = xMax - xMin, d = zMax - zMin;
                    Color color = BlockColors[(br + bc) % BlockColors.Length];

                    const float y = 0.03f, t = 0.02f, h = 0.012f; // on the deck — hidden once tiles/felt cover it
                    Bar(anchor, new Vector3(xMin, y, cz), new Vector3(t, h, d), color);
                    Bar(anchor, new Vector3(xMax, y, cz), new Vector3(t, h, d), color);
                    Bar(anchor, new Vector3(cx, y, zMin), new Vector3(w, h, t), color);
                    Bar(anchor, new Vector3(cx, y, zMax), new Vector3(w, h, t), color);
                }
            }
        }

        private static void Bar(Transform anchor, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BlockBorder";
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                { color = color };
            }
        }

        /// <summary>Wire up the input source, toolbelt and material inventory that drive the cells.</summary>
        public void Bind(PlayerInput input, RoofingToolbelt belt, PlayerMaterials mats)
        {
            if (playerInput != null)
            {
                playerInput.OnApplyStarted -= HandleApply;
                playerInput.OnApplyHeld -= HandleHeld;
            }
            ReleaseDropOverride();
            playerInput = input;
            toolbelt = belt;
            materials = mats;
            if (playerInput != null)
            {
                playerInput.OnApplyStarted += HandleApply;
                playerInput.OnApplyHeld += HandleHeld;
            }
            // Dropping a carried felt roll while aiming at the roof deploys it there (Etapa 3).
            if (materials != null) materials.DropOverride = TryDeployFeltRoll;
        }

        private void OnDisable()
        {
            live.Remove(this);
            if (playerInput != null)
            {
                playerInput.OnApplyStarted -= HandleApply;
                playerInput.OnApplyHeld -= HandleHeld;
            }
            ReleaseDropOverride();
        }

        /// <summary>Detach our felt-roll deploy hook from the inventory without clobbering someone else's.</summary>
        private void ReleaseDropOverride()
        {
            if (materials == null) return;
            var mine = (System.Func<CarryKind, int, bool>)TryDeployFeltRoll;
            if (materials.DropOverride == mine) materials.DropOverride = null;
        }

        /// <summary>
        /// While the apply button stays held, keep applying on a cadence so the player can
        /// sweep a tool across cells without clicking each one — mainly for laying felt with
        /// the roller. Each cell still only advances one step per tool, so holding on a single
        /// spot can't over-apply or waste material.
        /// </summary>
        private void HandleHeld(RoofingRaycastHit hit)
        {
            if (Time.time < nextHeldApplyTime) return;
            HandleApply(hit);
        }

        private void HandleApply(RoofingRaycastHit hit)
        {
            // Push the next held-repeat forward so a held click applies on a steady cadence
            // instead of every frame (also gates the very first held frame after the click).
            nextHeldApplyTime = Time.time + Mathf.Max(0.02f, holdRepeatInterval);

            if (hit.collider == null || toolbelt == null) return;

            var cell = hit.collider.GetComponentInParent<RoofCell>();
            if (cell == null) return;
            if (!Owns(cell)) return; // another face's grid handles it (Etapa D4)

            // Snow first (Etapa C2): a buried cell can't be worked with anything until the
            // shovel clears it — finished cells can be cleared too, so the roof reads done.
            if (cell.SnowBlocked)
            {
                if (toolbelt.Current == RoofingTool.TearOffShovel)
                    HudNotice.Show(cell.ShovelSnow()
                        ? "Celda despejada" : "Nieve pesada — otra palada");
                else
                    HudNotice.Show("Celda nevada — límpiala con la pala");
                return;
            }

            if (cell.IsComplete) return;

            RoofingTool tool = toolbelt.Current;
            if (tool != cell.RequiredTool) return;
            if (materials != null && materials.CarryingDebris) return; // hands full of debris

            // Damaged decks are worked a whole 2×3 block at a time, in three real steps: pry the
            // old plywood off (PryBar), SET the fresh plank in place by hand, then NAIL it down
            // flush with the hammer. (tool == RequiredTool was already checked above.)
            if (cell.DeckDamaged && cell.Stage == CellStage.TornOff) { TryPryBlock(cell); return; }
            if (cell.Stage == CellStage.DeckRemoved) { TryPlaceDeckBlock(cell); return; }
            if (cell.Stage == CellStage.DeckPlaced) { TryNailDeckBlock(cell); return; }

            // Stair-step order (Etapa 4): judge the placement BEFORE the stage changes.
            bool orderedPlacement = tool != RoofingTool.ShingleHand
                || cell.Stage != CellStage.Underlayment
                || IsValidShinglePlacement(cell);

            // Spend from what's in hand before advancing (tear-off and nailing cost nothing;
            // the shingle is spent when you place it by hand).
            if (materials != null)
            {
                if (tool == RoofingTool.ShingleHand && !materials.TryUse(CarryKind.Shingles)) return;
                if (tool == RoofingTool.UnderlaymentRoller && !materials.TryUse(CarryKind.FeltRoll)) return;
            }

            float quality = ApplyFinishBonuses(cell, ComputeQuality(cell, hit.point));
            if (!cell.TryAdvance(tool, quality)) return;

            if (!orderedPlacement) outOfOrder.Add(cell); // sloppy order — the nail score will pay for it

            if (tool == RoofingTool.TearOffShovel && cell.Stage == CellStage.TornOff)
            {
                SpawnDebris(cell);
                materials?.NotifyDebrisSpawned();
            }

            if (tool == RoofingTool.UnderlaymentRoller)
            {
                PlaceFeltRoll(cell);
                CoverNeighbourAlong(cell, quality);
            }

            if (cell.IsComplete)
            {
                completed++;
                qualitySum += cell.Quality;
                if (completed >= cells.Count) OnRoofComplete?.Invoke();
            }
        }

        /// <summary>
        /// Felt unrolls HORIZONTALLY along a course (rolling it up the slope makes no sense —
        /// the roll would take off downhill), so the roller also covers the next cell along the
        /// same course, extending the band sideways like a real roll-out.
        /// </summary>
        private void CoverNeighbourAlong(RoofCell cell, float quality)
        {
            RoofCell side = NextAlongCourse(cell);
            if (side == null) return;
            if (materials != null && !materials.TryUse(CarryKind.FeltRoll)) return;
            side.TryAdvance(RoofingTool.UnderlaymentRoller, quality);
            PlaceFeltRoll(side);
        }

        /// <summary>The adjacent cell along the same course (either side) that still needs felt.</summary>
        private RoofCell NextAlongCourse(RoofCell cell)
        {
            RoofCell a = Neighbour(cell.Row + 1, cell.Col);
            if (a != null && !a.IsComplete && a.RequiredTool == RoofingTool.UnderlaymentRoller) return a;
            RoofCell b = Neighbour(cell.Row - 1, cell.Col);
            if (b != null && !b.IsComplete && b.RequiredTool == RoofingTool.UnderlaymentRoller) return b;
            return null;
        }

        /// <summary>Cell lookup for the deployable felt roll (null off-grid).</summary>
        public RoofCell CellAt(int row, int col) => Neighbour(row, col);

        // ----- Multi-face support (Etapa D4) -----
        // A job can now have several grids (the far slope, the L-wing faces) sharing the
        // same player input. Each live grid registers itself so cell-based tools can route
        // to the face that OWNS a cell instead of assuming there is only one.
        private static readonly List<RoofGrid> live = new List<RoofGrid>();

        private void OnEnable()
        {
            if (!live.Contains(this)) live.Add(this);
        }

        /// <summary>Does this grid own that cell? (Cells are parented to the grid's face anchor.)</summary>
        public bool Owns(RoofCell cell) => cell != null && anchor != null && cell.transform.parent == anchor;

        /// <summary>The grid whose face owns this cell, or null.</summary>
        public static RoofGrid GridOf(RoofCell cell)
        {
            if (cell == null) return null;
            for (int i = 0; i < live.Count; i++)
                if (live[i] != null && live[i].Owns(cell)) return live[i];
            return null;
        }

        /// <summary>The grid whose face passes closest under a world point (for the chalk line).</summary>
        public static RoofGrid GridNear(Vector3 world)
        {
            RoofGrid best = null;
            float bestY = 1.5f;
            for (int i = 0; i < live.Count; i++)
            {
                RoofGrid g = live[i];
                if (g == null || g.anchor == null) continue;
                Vector3 l = g.anchor.InverseTransformPoint(world);
                if (Mathf.Abs(l.x) > g.faceSize.x * 0.55f || Mathf.Abs(l.z) > g.faceSize.y * 0.55f) continue;
                float ay = Mathf.Abs(l.y);
                if (ay < bestY) { bestY = ay; best = g; }
            }
            return best;
        }

        /// <summary>
        /// Etapa D3: take a cell out of play — a chimney or skylight occupies it. It stops
        /// counting toward totals/completion, block operations skip it (they already skip
        /// nulls), and the stair-step order treats it as supported so courses flow AROUND
        /// the obstacle instead of dead-ending under it. Returns false off-grid.
        /// </summary>
        public bool ObstructCell(int row, int col)
        {
            RoofCell cell = Neighbour(row, col);
            if (cell == null) return false;
            obstructedMask[row, col] = true;
            grid[row, col] = null;
            cells.Remove(cell);
            Destroy(cell.gameObject);
            return true;
        }

        private bool Obstructed(int r, int c) =>
            obstructedMask != null && r >= 0 && r < rows && c >= 0 && c < cols && obstructedMask[r, c];

        /// <summary>
        /// The nail score the player would get right now at <paramref name="worldPoint"/> —
        /// shown live while zoomed with the nail gun (Etapa 8). -1 when the cell isn't ready
        /// to nail.
        /// </summary>
        public float PreviewNailQuality(RoofCell cell, Vector3 worldPoint)
        {
            if (cell == null || cell.IsComplete || cell.RequiredTool != RoofingTool.NailGun) return -1f;
            return ApplyFinishBonuses(cell, ComputeQuality(cell, worldPoint));
        }

        /// <summary>The plank measure this cell's damaged block needs (0 if it isn't a damaged block).</summary>
        public float HoleMeasure(RoofCell cell)
        {
            if (cell == null || blockPlankLength == null || !cell.DeckDamaged) return 0f;
            int br = cell.Row / DeckBlockRows, bc = cell.Col / DeckBlockCols;
            if (br >= blockPlankLength.GetLength(0) || bc >= blockPlankLength.GetLength(1)) return 0f;
            return blockPlankLength[br, bc];
        }

        /// <summary>
        /// Lay felt on a cell from the physical roll (Etapa 3): the roll spends its own units,
        /// so no carried material is used. Returns true if the cell advanced.
        /// </summary>
        public bool LayFeltFromRoll(RoofCell cell)
        {
            if (cell == null || cell.IsComplete || cell.RequiredTool != RoofingTool.UnderlaymentRoller) return false;
            if (!cell.TryAdvance(RoofingTool.UnderlaymentRoller, 1f)) return false;
            CleanupCourseRoll(cell.Col);
            return true;
        }

        /// <summary>
        /// G with a felt roll in hand while aiming at the roof: instead of the generic ground
        /// drop, deploy the physical roll on that strip (see <see cref="DeployableFeltRoll"/>).
        /// </summary>
        private bool TryDeployFeltRoll(CarryKind kind, int amount)
        {
            if (kind != CarryKind.FeltRoll || amount <= 0) return false;
            if (playerInput == null || !playerInput.HasValidTarget || anchor == null) return false;
            RoofCell cell = playerInput.CurrentHit.collider != null
                ? playerInput.CurrentHit.collider.GetComponentInParent<RoofCell>()
                : null;
            if (cell == null) return false;
            // Only one grid holds the inventory's drop hook (the last one bound), so route
            // the deploy to whichever face actually owns the aimed cell (Etapa D4).
            RoofGrid owner = GridOf(cell) ?? this;
            DeployableFeltRoll.Deploy(owner, owner.anchor, cell, amount, owner.materials);
            return true;
        }

        /// <summary>
        /// Pry a damaged deck off a whole 2×3 block at once: the block must be fully torn off.
        /// Pulls the nails and lifts the old plywood (one big debris object), exposing the
        /// hollow (rafters + insulation). Costs nothing — it's just removal.
        /// </summary>
        private void TryPryBlock(RoofCell origin)
        {
            int c0 = (origin.Col / DeckBlockCols) * DeckBlockCols;
            int r0 = (origin.Row / DeckBlockRows) * DeckBlockRows;

            var block = new List<RoofCell>();
            bool anyToPry = false;
            for (int r = r0; r < r0 + DeckBlockRows && r < rows; r++)
            {
                for (int c = c0; c < c0 + DeckBlockCols && c < cols; c++)
                {
                    RoofCell cell = grid[r, c];
                    if (cell == null) continue;
                    if (cell.Stage == CellStage.OldShingles) return; // strip the whole block first
                    block.Add(cell);
                    if (cell.Stage == CellStage.TornOff) anyToPry = true;
                }
            }
            if (!anyToPry) return;

            SpawnBlockDebris(block);          // the old plywood lifts off as debris
            materials?.NotifyDebrisSpawned();

            foreach (RoofCell cell in block) cell.RemoveDeck();
        }

        /// <summary>
        /// Set a fresh plank over the exposed hollow by hand, a whole 2×3 block at once. The
        /// block must already be pried off (DeckRemoved). Costs one plank; it rests loose
        /// (paler, slightly proud) until it's nailed.
        /// </summary>
        private void TryPlaceDeckBlock(RoofCell origin)
        {
            int c0 = (origin.Col / DeckBlockCols) * DeckBlockCols;
            int r0 = (origin.Row / DeckBlockRows) * DeckBlockRows;

            var block = new List<RoofCell>();
            bool anyToPlace = false;
            for (int r = r0; r < r0 + DeckBlockRows && r < rows; r++)
            {
                for (int c = c0; c < c0 + DeckBlockCols && c < cols; c++)
                {
                    RoofCell cell = grid[r, c];
                    if (cell == null) continue;
                    if (cell.Stage == CellStage.TornOff) return; // pry the whole block off first
                    block.Add(cell);
                    if (cell.Stage == CellStage.DeckRemoved) anyToPlace = true;
                }
            }
            if (!anyToPlace) return;

            // The plank in hand must match the hole's measure (Etapa 6): full sheets fit the
            // full-size holes; the rest must be measured (metro) and cut (sierra) first.
            if (materials != null)
            {
                if (materials.Carrying != CarryKind.Plank)
                {
                    HudNotice.Show("Necesitas una plancha de madera en las manos");
                    return;
                }
                float need = HoleMeasure(origin);
                if (need > 0f && Mathf.Abs(materials.PlankLength - need) > 0.06f)
                {
                    HudNotice.Show(materials.PlankLength < need
                        ? $"La plancha ({materials.PlankLength:0.0} m) es corta — el hueco pide {need:0.0} m"
                        : $"Sobra plancha ({materials.PlankLength:0.0} m) — mide el hueco y corta a {need:0.0} m");
                    return;
                }
                if (!materials.TryUse(CarryKind.Plank)) return;
            }

            foreach (RoofCell cell in block) cell.PlaceDeck();
        }

        /// <summary>Nail the resting plank down flush, the whole 2×3 block at once. Costs nothing —
        /// the plank was spent when it was set in place.</summary>
        private void TryNailDeckBlock(RoofCell origin)
        {
            int c0 = (origin.Col / DeckBlockCols) * DeckBlockCols;
            int r0 = (origin.Row / DeckBlockRows) * DeckBlockRows;

            var block = new List<RoofCell>();
            bool anyToNail = false;
            for (int r = r0; r < r0 + DeckBlockRows && r < rows; r++)
            {
                for (int c = c0; c < c0 + DeckBlockCols && c < cols; c++)
                {
                    RoofCell cell = grid[r, c];
                    if (cell == null) continue;
                    block.Add(cell);
                    if (cell.Stage == CellStage.DeckPlaced) anyToNail = true;
                }
            }
            if (!anyToNail) return;

            foreach (RoofCell cell in block) cell.NailDeck();
        }

        /// <summary>One big debris object for the removed old deck, sized to the block.</summary>
        private static void SpawnBlockDebris(List<RoofCell> block)
        {
            if (block.Count == 0) return;

            Vector3 center = Vector3.zero;
            int minC = int.MaxValue, maxC = int.MinValue, minR = int.MaxValue, maxR = int.MinValue;
            foreach (RoofCell c in block)
            {
                center += c.transform.position;
                minC = Mathf.Min(minC, c.Col); maxC = Mathf.Max(maxC, c.Col);
                minR = Mathf.Min(minR, c.Row); maxR = Mathf.Max(maxR, c.Row);
            }
            center /= block.Count;

            RoofCell sample = block[0];
            Vector3 size = new Vector3(
                sample.Size.x * (maxC - minC + 1) * 0.95f, 0.1f,
                sample.Size.y * (maxR - minR + 1) * 0.95f);
            Debris.Spawn(center + sample.transform.up * 0.35f, size, new Color(0.36f, 0.26f, 0.18f));
        }

        private RoofCell Neighbour(int r, int c)
        {
            if (grid == null || r < 0 || r >= rows || c < 0 || c >= cols) return null;
            return grid[r, c];
        }

        // Fase E gear (calibrated nail gun): softens the off-centre penalty — the sweet
        // spot widens, but a sloppy edge nail still scores worse than a centred one.
        private float precisionAid;
        public void SetPrecisionAid(float a01) => precisionAid = Mathf.Clamp01(a01);

        private float ComputeQuality(RoofCell cell, Vector3 worldPoint)
        {
            Vector3 local = cell.transform.InverseTransformPoint(worldPoint);
            float dx = cell.Size.x > 0f ? Mathf.Abs(local.x) / (cell.Size.x * 0.5f) : 0f;
            float dz = cell.Size.y > 0f ? Mathf.Abs(local.z) / (cell.Size.y * 0.5f) : 0f;
            float off = Mathf.Clamp01(Mathf.Max(dx, dz));
            return 1f - off * 0.6f * (1f - precisionAid * 0.35f); // dead-centre = 1.0, edge ≈ 0.4 (0.61 aided)
        }

        /// <summary>Add the extra-tool finishing bonuses (chalk guide, pinned felt, trimmed felt)
        /// and the stair-step penalty for shingles placed out of order.</summary>
        private float ApplyFinishBonuses(RoofCell cell, float quality)
        {
            if (chalkedCourse != null && cell.Col >= 0 && cell.Col < chalkedCourse.Length && chalkedCourse[cell.Col]) quality += 0.15f;
            if (secured.Contains(cell)) quality += 0.10f;
            if (trimmed.Contains(cell)) quality += 0.05f;
            if (outOfOrder.Contains(cell)) quality -= 0.25f;
            return Mathf.Clamp01(quality);
        }

        // ----- Stair-step shingle order (Etapa 4) -----

        /// <summary>
        /// Whether this cell is a correct next shingle placement: the eave course starts at a
        /// bottom CORNER and grows sideways; every upper course needs the cell below it shingled
        /// (that's what builds the climbing diagonal) and grows from its own course's shingles.
        /// </summary>
        public bool IsValidShinglePlacement(RoofCell cell)
        {
            if (cell == null || cell.Stage != CellStage.Underlayment) return false;
            int r = cell.Row, c = cell.Col;

            if (c == cols - 1) // eave/starter course
                return CourseHasShingles(c)
                    ? Shingled(r - 1, c) || Shingled(r + 1, c)
                    : r == 0 || r == rows - 1;

            if (!Shingled(r, c + 1)) return false; // the course below must support it (diagonal)
            return !CourseHasShingles(c) || Shingled(r - 1, c) || Shingled(r + 1, c);
        }

        private bool Shingled(int r, int c)
        {
            // An obstructed position (chimney/skylight) counts as laid, so the diagonal
            // can climb over it and courses can start against it — you roof AROUND it.
            if (Obstructed(r, c)) return true;
            RoofCell cell = Neighbour(r, c);
            return cell != null && cell.Stage >= CellStage.ShinglePlaced;
        }

        private bool CourseHasShingles(int c)
        {
            for (int r = 0; r < rows; r++)
                if (Shingled(r, c)) return true;
            return false;
        }

        /// <summary>With the hand out, glow the cells that are the valid next stair-step placements.</summary>
        private void Update()
        {
            bool show = toolbelt != null && toolbelt.Current == RoofingTool.ShingleHand
                && (materials == null || !materials.CarryingDebris);
            for (int i = 0; i < cells.Count; i++)
            {
                RoofCell cell = cells[i];
                if (cell != null) cell.SetHighlight(show && IsValidShinglePlacement(cell));
            }
        }

        // ----- Extra-tool functions (called by Gameplay.ToolFunctions) -----

        /// <summary>Chalk line: the player picks two points; snap a guide between them and chalk
        /// every shingle COURSE it spans (like snapping course guides on site) so shingles nailed
        /// there score higher.</summary>
        public void ChalkWorldLine(Vector3 worldA, Vector3 worldB)
        {
            if (anchor == null || chalkedCourse == null) return;
            DrawWorldChalk(worldA, worldB);
            int ca = ColAt(worldA), cb = ColAt(worldB);
            if (ca > cb) { int t = ca; ca = cb; cb = t; }
            for (int c = Mathf.Max(0, ca); c <= Mathf.Min(cols - 1, cb); c++) chalkedCourse[c] = true;
        }

        private int ColAt(Vector3 world)
        {
            Vector3 local = anchor.InverseTransformPoint(world);
            int c = Mathf.FloorToInt((local.x + faceSize.x * 0.5f) / Mathf.Max(0.001f, cellWidth));
            return Mathf.Clamp(c, 0, cols - 1);
        }

        private static void DrawWorldChalk(Vector3 a, Vector3 b)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "ChalkLine";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.position = (a + b) * 0.5f;
            Vector3 dir = b - a;
            if (dir.sqrMagnitude > 0.0001f) go.transform.up = dir.normalized;
            go.transform.localScale = new Vector3(0.015f, Mathf.Max(0.01f, dir.magnitude * 0.5f), 0.015f);
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default")) { color = new Color(0.25f, 0.55f, 0.95f) };
        }

        /// <summary>Saw: score a visible kerf across the cell's tile division (full cut-to-size lands in Etapa 6).</summary>
        public void SawCut(RoofCell cell)
        {
            if (cell == null) return;
            AddMark(cell.transform, new Vector3(0f, 0.06f, 0f), new Vector3(cell.Size.x, 0.012f, 0.02f), new Color(0.08f, 0.08f, 0.08f));
        }

        // ----- Felt roll-out (Etapa 3) -----
        // A visible felt roll that follows the leading edge ALONG each course (fixed column) as
        // you lay felt with the roller, so it reads as unrolling the underlayment horizontally —
        // its axis points up the slope, so gravity can't roll it away downhill.
        private readonly Dictionary<int, GameObject> feltRolls = new Dictionary<int, GameObject>();
        private float feltSpin;

        private void PlaceFeltRoll(RoofCell cell)
        {
            if (cell == null || anchor == null) return;

            if (!feltRolls.TryGetValue(cell.Col, out GameObject roll) || roll == null)
            {
                roll = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                roll.name = "FeltRoll";
                roll.transform.SetParent(anchor, false);
                var col = roll.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var r = roll.GetComponent<Renderer>();
                if (r != null)
                {
                    var m = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default")) { color = new Color(0.16f, 0.16f, 0.20f) };
                    m.mainTexture = RoofTextureLibrary.Get(RoofTextureLibrary.Surface.Felt);
                    m.mainTextureScale = new Vector2(2f, 2f);
                    r.sharedMaterial = m;
                }
                feltRolls[cell.Col] = roll;
            }

            // Sit the roll on this cell with its axis pointing up the slope (it rolls sideways
            // along the course), and spin it a little each time so it reads as rolling out.
            Vector3 cp = cell.transform.localPosition;
            roll.transform.localPosition = new Vector3(cp.x, cp.y + 0.13f, cp.z);
            feltSpin += 110f;
            roll.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // length axis = up-slope
            roll.transform.Rotate(0f, feltSpin, 0f, Space.Self);
            roll.transform.localScale = new Vector3(0.16f, cell.Size.x * 0.46f, 0.16f);

            CleanupCourseRoll(cell.Col);
        }

        /// <summary>Once no cell in the course still needs felt, its ghost roll ran out — remove it.</summary>
        private void CleanupCourseRoll(int col)
        {
            if (grid == null || !feltRolls.TryGetValue(col, out GameObject roll) || roll == null) return;
            for (int r = 0; r < rows; r++)
            {
                RoofCell cell = grid[r, col];
                if (cell != null && cell.Stage < CellStage.Underlayment) return; // still felt to lay here
            }
            Destroy(roll);
            feltRolls.Remove(col);
        }

        /// <summary>Cap nailer: pin laid felt down (visible plastic caps); that cell's shingle scores
        /// higher. The caps live on the cell's felt-decor layer, so the shingle later covers them.</summary>
        public void SecureFelt(RoofCell cell)
        {
            if (cell == null || cell.Stage != CellStage.Underlayment) return;
            if (!secured.Add(cell)) return;
            for (int i = 0; i < 4; i++)
            {
                float lx = (i % 2 == 0 ? -1f : 1f) * cell.Size.x * 0.30f;
                float lz = (i < 2 ? -1f : 1f) * cell.Size.y * 0.30f;
                AddMark(cell.FeltDecor, new Vector3(lx, 0.075f, lz), new Vector3(0.05f, 0.02f, 0.05f), new Color(0.88f, 0.32f, 0.55f));
            }
        }

        /// <summary>Utility knife: trim the felt overhang neat along the cell's down-slope edge; small
        /// extra quality. The cut line hides once a shingle covers the felt.</summary>
        public void TrimFelt(RoofCell cell)
        {
            if (cell == null || cell.Stage < CellStage.Underlayment) return;
            if (!trimmed.Add(cell)) return;
            AddMark(cell.FeltDecor, new Vector3(0f, 0.07f, -cell.Size.y * 0.45f),
                new Vector3(cell.Size.x * 0.9f, 0.01f, 0.02f), new Color(0.95f, 0.95f, 0.90f));
        }

        private static void AddMark(Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ToolMark";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default")) { color = color };
        }

        private static void SpawnDebris(RoofCell cell)
        {
            // Same footprint as the tile that was torn off.
            Vector3 pos = cell.transform.position + cell.transform.up * 0.3f;
            Vector3 size = new Vector3(cell.Size.x * 0.95f, 0.06f, cell.Size.y * 0.95f);
            Debris.Spawn(pos, size, new Color(0.30f, 0.28f, 0.27f));
        }
    }
}
