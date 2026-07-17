using UnityEngine;

namespace RoofingSimulator.Gameplay
{
    /// <summary>The tools the player swaps between, each driving one roofing step.</summary>
    public enum RoofingTool
    {
        TearOffShovel,      // strip the old shingles
        PryBar,             // pull the nails and lift off the damaged plywood (exposes the hollow)
        DeckHammer,         // nail fresh plywood down over the hollow (a 2×3 block at a time)
        UnderlaymentRoller, // lay the felt/underlayment
        ShingleHand,        // place a new shingle in position (by hand)
        NailGun,            // nail the placed shingle down

        // Extra tools (inventory + real functions in ToolFunctions).
        Drill,        // taladro — unscrew the 4 corner bolts of the antenna base
        Saw,          // cortadora — cut the deck along the tile divisions (full cut-to-size in Etapa 6)
        TapeMeasure,  // metro — measure the distance between two clicked points
        UtilityKnife, // cuchilla — trim laid felt neat
        ChalkLine     // tiza — snap a guide line between two clicked points (like the tape measure)
    }

    /// <summary>The ordered states a single roof cell passes through to be "done".</summary>
    public enum CellStage
    {
        OldShingles = 0,
        TornOff = 1,
        DeckRemoved = 2,    // damaged plywood pried off — hollow (rafters + insulation) showing
        DeckPlaced = 3,     // fresh plank set over the hollow by hand, not nailed yet
        DeckRepaired = 4,   // plank nailed down flush
        Underlayment = 5,
        ShinglePlaced = 6,  // shingle laid but not yet nailed
        NewShingles = 7     // nailed = complete
    }

    /// <summary>
    /// One rectangular shingle area, shown as real stacked layers (old shingles → wood deck
    /// → felt → placed shingle → nailed shingle). Shingles have a small side gap so each one
    /// reads individually, while the deck stays continuous. Some cells' decks are damaged and
    /// must be replaced; finishing (nailing) records a precision quality score.
    /// </summary>
    public class RoofCell : MonoBehaviour
    {
        public CellStage Stage { get; private set; } = CellStage.OldShingles;
        public bool IsComplete => Stage == CellStage.NewShingles;
        public Vector2 Size { get; private set; }
        public float Quality { get; private set; } = 1f;
        public int Row { get; private set; }
        public int Col { get; private set; }
        public bool DeckDamaged { get; private set; }
        /// <summary>Whether this particular cell shows the rot stain (only part of a damaged block does).</summary>
        public bool Stained { get; private set; }

        private GameObject deck, oldTiles, felt, newTiles, hollow, starter, highlight;
        private Renderer deckRenderer;
        private Renderer[] newTileRenderers;
        private float[] newTileShades; // per-piece tint so the courses don't look like one flat slab
        private bool deckRepaired;
        private Transform feltDecor;
        private GameObject snowCap;
        private float snowDepth; // 0 bare .. 1 buried under snowfall (Fase C, Etapa C2)

        private const float SideGap = 0.008f; // hairline left/right gap between shingles

        private static readonly Color WeatheredWood = new Color(0.55f, 0.42f, 0.28f);
        private static readonly Color DamagedWood = new Color(0.40f, 0.24f, 0.20f);
        private static readonly Color FreshWood = new Color(0.80f, 0.62f, 0.40f);
        private static readonly Color OldShingleColor = new Color(0.33f, 0.31f, 0.30f);
        private static readonly Color FeltColor = new Color(0.13f, 0.13f, 0.16f);
        private static readonly Color PlacedShingleColor = new Color(0.45f, 0.60f, 0.72f); // laid, not nailed
        private static readonly Color NewShingleColor = new Color(0.12f, 0.26f, 0.38f);     // nailed (darker)

        /// <summary>The tool that advances this cell from its current stage.</summary>
        public RoofingTool RequiredTool => Stage switch
        {
            CellStage.OldShingles => RoofingTool.TearOffShovel,
            CellStage.TornOff => DeckDamaged ? RoofingTool.PryBar : RoofingTool.UnderlaymentRoller,
            CellStage.DeckRemoved => RoofingTool.ShingleHand, // set the fresh plank in place by hand
            CellStage.DeckPlaced => RoofingTool.DeckHammer,   // then nail it down flush
            CellStage.DeckRepaired => RoofingTool.UnderlaymentRoller,
            CellStage.Underlayment => RoofingTool.ShingleHand,
            _ => RoofingTool.NailGun
        };

        public void Setup(int row, int col, float width, float depth, bool deckDamaged, bool stained,
            bool eaveCourse = false)
        {
            Row = row;
            Col = col;
            Size = new Vector2(width, depth);
            DeckDamaged = deckDamaged;
            Stained = stained;

            deck = CreateSlab("Deck", 0.00f, 0.04f, WeatheredWood, RoofTextureLibrary.Surface.WoodDeck);
            deckRenderer = deck.GetComponent<Renderer>();
            oldTiles = CreateSteppedTile("OldShingles", 0.05f, OldShingleColor, RoofTextureLibrary.Surface.OldShingle);
            felt = CreateSlab("Felt", 0.05f, 0.02f, FeltColor, RoofTextureLibrary.Surface.Felt);
            newTiles = CreateShingleCourses("NewShingles", 0.065f);
            hollow = DeckDamaged ? BuildHollow() : null; // only damaged decks ever expose the cavity
            if (eaveCourse) BuildStarterStrip();

            UpdatePieces();
        }

        /// <summary>
        /// Advance one stage if <paramref name="tool"/> matches. Undamaged decks skip the
        /// repaired stage; the final nailing records quality. Returns true if it changed.
        /// </summary>
        public bool TryAdvance(RoofingTool tool, float quality)
        {
            if (IsComplete || tool != RequiredTool) return false;

            switch (Stage)
            {
                case CellStage.OldShingles:
                    Stage = CellStage.TornOff;
                    break;
                case CellStage.TornOff:
                    // Undamaged decks go straight to felt; damaged ones are pried + re-decked by
                    // the grid's block logic (RemoveDeck/RepairDeck), never through here.
                    Stage = CellStage.Underlayment;
                    break;
                case CellStage.DeckRepaired:
                    Stage = CellStage.Underlayment;
                    break;
                case CellStage.Underlayment:
                    Stage = CellStage.ShinglePlaced;
                    break;
                case CellStage.ShinglePlaced:
                    Stage = CellStage.NewShingles;
                    Quality = Mathf.Clamp01(quality);
                    break;
            }

            UpdatePieces();

            // A little life: the placed shingle drops in and settles; nailing presses it flat.
            if (Stage == CellStage.ShinglePlaced) PlayShingleAnim(SettleShingle());
            else if (Stage == CellStage.NewShingles) PlayShingleAnim(NailPress());
            return true;
        }

        // ----- Shingle animation (they shouldn't just snap rigidly into place) -----

        private Coroutine shingleAnim;

        private void PlayShingleAnim(System.Collections.IEnumerator anim)
        {
            if (newTiles == null || !isActiveAndEnabled) return;
            if (shingleAnim != null) StopCoroutine(shingleAnim);
            ResetShingleTransform();
            shingleAnim = StartCoroutine(anim);
        }

        private void ResetShingleTransform()
        {
            newTiles.transform.localPosition = Vector3.zero;
            newTiles.transform.localRotation = Quaternion.identity;
            newTiles.transform.localScale = Vector3.one;
        }

        /// <summary>The shingle falls the last stretch, slightly tilted, and settles with a tiny bounce.</summary>
        private System.Collections.IEnumerator SettleShingle()
        {
            Transform t = newTiles.transform;
            Quaternion tilt = Quaternion.Euler(
                Random.Range(-7f, -3f), Random.Range(-5f, 5f), Random.Range(-4f, 4f));
            const float drop = 0.22f, fall = 0.18f, bounce = 0.12f;

            for (float e = 0f; e < fall; e += Time.deltaTime)
            {
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(e / fall), 2f); // ease-out fall
                t.localPosition = Vector3.up * (drop * (1f - k));
                t.localRotation = Quaternion.Slerp(tilt, Quaternion.identity, k);
                yield return null;
            }
            for (float e = 0f; e < bounce; e += Time.deltaTime)
            {
                float k = Mathf.Sin(Mathf.Clamp01(e / bounce) * Mathf.PI);
                t.localPosition = Vector3.up * (0.02f * k); // soft settle
                yield return null;
            }
            ResetShingleTransform();
            shingleAnim = null;
        }

        /// <summary>Nailing squashes the shingle down for an instant so the hit reads as a press.</summary>
        private System.Collections.IEnumerator NailPress()
        {
            Transform t = newTiles.transform;
            const float dur = 0.12f;
            for (float e = 0f; e < dur; e += Time.deltaTime)
            {
                float k = Mathf.Sin(Mathf.Clamp01(e / dur) * Mathf.PI);
                t.localScale = new Vector3(1f, 1f - 0.22f * k, 1f);
                yield return null;
            }
            ResetShingleTransform();
            shingleAnim = null;
        }

        public void ResetCell()
        {
            Stage = CellStage.OldShingles;
            Quality = 1f;
            deckRepaired = false;
            UpdatePieces();
        }

        /// <summary>Pry the damaged board off this cell (the grid calls it for every cell in a 2×3 block).</summary>
        public void RemoveDeck()
        {
            if (Stage != CellStage.TornOff) return;
            Stage = CellStage.DeckRemoved;
            UpdatePieces();
        }

        /// <summary>Set the fresh plank over the hollow, resting loose (the grid calls it per 2×3 block).</summary>
        public void PlaceDeck()
        {
            if (Stage != CellStage.DeckRemoved) return;
            Stage = CellStage.DeckPlaced;
            UpdatePieces();
        }

        /// <summary>Nail the resting plank down flush (the grid calls it for every cell in a 2×3 block).</summary>
        public void NailDeck()
        {
            if (Stage != CellStage.DeckPlaced) return;
            Stage = CellStage.DeckRepaired;
            deckRepaired = true;
            UpdatePieces();
        }

        private void UpdatePieces()
        {
            if (deckRenderer != null)
            {
                Color c = Stage == CellStage.DeckPlaced ? FreshWood * 1.12f // pale: resting, not nailed
                    : deckRepaired ? FreshWood
                    : (Stained && Stage == CellStage.TornOff ? DamagedWood : WeatheredWood);
                deckRenderer.material.color = c;
            }

            if (deck != null)
            {
                deck.SetActive(Stage != CellStage.DeckRemoved); // board is off while the hollow shows
                // The fresh plank sits a touch proud of the rafters until it's nailed flush.
                Vector3 dp = deck.transform.localPosition;
                dp.y = Stage == CellStage.DeckPlaced ? 0.035f : 0f;
                deck.transform.localPosition = dp;
            }
            if (hollow != null) hollow.SetActive(Stage == CellStage.DeckRemoved);
            if (oldTiles != null) oldTiles.SetActive(Stage == CellStage.OldShingles);
            if (felt != null) felt.SetActive(Stage >= CellStage.Underlayment);
            // Cap nails / trim marks live on the felt — a shingle on top covers them.
            if (feltDecor != null) feltDecor.gameObject.SetActive(
                Stage >= CellStage.Underlayment && Stage < CellStage.ShinglePlaced);

            // The starter strip peeks out under the eave course once the felt is on.
            if (starter != null) starter.SetActive(Stage >= CellStage.Underlayment);

            bool showNew = Stage >= CellStage.ShinglePlaced;
            if (newTiles != null) newTiles.SetActive(showNew);
            if (showNew && newTileRenderers != null)
            {
                // Pale while just placed, full colour once nailed — each piece keeps its own shade.
                Color tileColor = Stage == CellStage.ShinglePlaced ? PlacedShingleColor : NewShingleColor;
                for (int i = 0; i < newTileRenderers.Length; i++)
                {
                    if (newTileRenderers[i] == null) continue;
                    float s = newTileShades != null && i < newTileShades.Length ? newTileShades[i] : 1f;
                    newTileRenderers[i].material.color = tileColor * s;
                }
            }
        }

        /// <summary>The grid pulses this on cells that are the valid next stair-step placement.</summary>
        public void SetHighlight(bool on)
        {
            if (highlight == null)
            {
                if (!on) return;
                highlight = BuildHighlight();
            }
            if (highlight.activeSelf != on) highlight.SetActive(on);
        }

        // ----- Snow (Fase C, Etapa C2) -----

        public float SnowDepth => snowDepth;

        /// <summary>Buried deep enough that no tool can work the cell until it's shoveled clear.</summary>
        public bool SnowBlocked => snowDepth > 0.45f;

        /// <summary>Accumulate (positive) or melt (negative) snow; the white cap grows/shrinks.</summary>
        public void AddSnow(float delta)
        {
            float d = Mathf.Clamp01(snowDepth + delta);
            if (Mathf.Approximately(d, snowDepth)) return;
            snowDepth = d;
            UpdateSnowCap();
        }

        /// <summary>One shovel stroke of clearing. Returns true once the cell is workable again
        /// (a fully buried cell takes two strokes).</summary>
        public bool ShovelSnow()
        {
            AddSnow(-0.65f);
            return !SnowBlocked;
        }

        private void UpdateSnowCap()
        {
            bool show = snowDepth > 0.03f;
            if (snowCap == null)
            {
                if (!show) return;
                snowCap = NewCube("SnowCap", transform);
                var col = snowCap.GetComponent<Collider>();
                if (col != null) Destroy(col); // logical block, not a physical one — clicks reach the cell
                var r = snowCap.GetComponent<Renderer>();
                if (r != null)
                {
                    var m = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                    { color = new Color(0.93f, 0.95f, 0.99f) };
                    if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.08f); // matte snow
                    r.sharedMaterial = m;
                }
            }
            if (snowCap.activeSelf != show) snowCap.SetActive(show);
            float th = 0.02f + 0.075f * snowDepth;
            snowCap.transform.localScale = new Vector3(Size.x * 0.99f, th, Size.y * 0.99f);
            snowCap.transform.localPosition = new Vector3(0f, 0.105f + th * 0.5f, 0f);
        }

        /// <summary>Parent for marks that sit ON the felt (cap nails, trimmed edge): they hide
        /// automatically once a shingle covers that felt.</summary>
        public Transform FeltDecor
        {
            get
            {
                if (feltDecor == null)
                {
                    feltDecor = new GameObject("FeltDecor").transform;
                    feltDecor.SetParent(transform, false);
                    feltDecor.gameObject.SetActive(
                        Stage >= CellStage.Underlayment && Stage < CellStage.ShinglePlaced);
                }
                return feltDecor;
            }
        }

        // ----- Geometry helpers -----

        /// <summary>Continuous layer (deck / felt) covering the whole cell, flush with neighbours.</summary>
        private GameObject CreateSlab(string slabName, float localY, float thickness, Color color, RoofTextureLibrary.Surface surface)
        {
            var go = NewCube(slabName, transform);
            go.transform.localPosition = new Vector3(0f, localY, 0f);
            go.transform.localScale = new Vector3(Size.x, thickness, Size.y);
            Paint(go, color, surface);
            return go;
        }

        /// <summary>
        /// The NEW shingles at real scale (Etapa 4): the cell is one shingle wide, split into
        /// mini-courses of ~14 cm real exposure up the slope. Each course gets its own slight
        /// shade, a darker butt edge, and alternating courses show a mid-tile bond joint — the
        /// classic offset (desfase) look.
        /// </summary>
        private GameObject CreateShingleCourses(string tileName, float localY)
        {
            var root = new GameObject(tileName);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;

            var rends = new System.Collections.Generic.List<Renderer>();
            var shades = new System.Collections.Generic.List<float>();
            float sideZ = Mathf.Max(0.05f, Size.y - SideGap);
            int n = Mathf.Max(1, Mathf.RoundToInt(Size.x / 0.15f)); // ≈14 cm exposure per course
            float exp = Size.x / n;

            for (int i = 0; i < n; i++)
            {
                float x = -Size.x * 0.5f + (i + 0.5f) * exp;

                var slab = NewCube("Course", root.transform);
                slab.transform.localPosition = new Vector3(x, localY, 0f);
                slab.transform.localScale = new Vector3(exp, 0.04f, sideZ);
                Paint(slab, NewShingleColor, RoofTextureLibrary.Surface.AsphaltShingle);
                rends.Add(slab.GetComponent<Renderer>());
                shades.Add(Random.Range(0.93f, 1.07f));

                // Raised, darker butt edge on the down-slope side of each course (the step).
                var lip = NewCube("Butt", root.transform);
                lip.transform.localPosition = new Vector3(x + exp * 0.42f, localY + 0.014f, 0f);
                lip.transform.localScale = new Vector3(exp * 0.16f, 0.05f, sideZ);
                Paint(lip, NewShingleColor, RoofTextureLibrary.Surface.AsphaltShingle);
                rends.Add(lip.GetComponent<Renderer>());
                shades.Add(0.78f);

                // Bond joint at mid-tile on alternating courses (reads as the half-shingle offset).
                if (i % 2 == 1)
                {
                    var seam = NewCube("Seam", root.transform);
                    seam.transform.localPosition = new Vector3(x, localY + 0.022f, 0f);
                    seam.transform.localScale = new Vector3(exp * 0.9f, 0.012f, 0.014f);
                    Paint(seam, NewShingleColor, RoofTextureLibrary.Surface.AsphaltShingle);
                    rends.Add(seam.GetComponent<Renderer>());
                    shades.Add(0.45f);
                }
            }

            newTileRenderers = rends.ToArray();
            newTileShades = shades.ToArray();
            return root;
        }

        /// <summary>Starter strip on the eave course: a dark band overhanging the lower edge, as on site.</summary>
        private void BuildStarterStrip()
        {
            starter = NewCube("StarterStrip", transform);
            starter.transform.localPosition = new Vector3(Size.x * 0.5f + 0.05f, 0.05f, 0f);
            starter.transform.localScale = new Vector3(0.14f, 0.03f, Size.y);
            Paint(starter, NewShingleColor * 0.5f, RoofTextureLibrary.Surface.AsphaltShingle);
            starter.SetActive(false);
        }

        /// <summary>A bright green frame floating just over the cell (built lazily, toggled by the grid).</summary>
        private GameObject BuildHighlight()
        {
            var root = new GameObject("NextHighlight");
            root.transform.SetParent(transform, false);
            Color glow = new Color(0.40f, 1f, 0.45f);
            float hx = Size.x * 0.46f, hz = Size.y * 0.46f;
            const float y = 0.11f, t = 0.035f, h = 0.02f;
            HighlightBar(root.transform, new Vector3(-hx, y, 0f), new Vector3(t, h, Size.y * 0.92f), glow);
            HighlightBar(root.transform, new Vector3(hx, y, 0f), new Vector3(t, h, Size.y * 0.92f), glow);
            HighlightBar(root.transform, new Vector3(0f, y, -hz), new Vector3(Size.x * 0.92f, h, t), glow);
            HighlightBar(root.transform, new Vector3(0f, y, hz), new Vector3(Size.x * 0.92f, h, t), glow);
            return root;
        }

        private static void HighlightBar(Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = NewCube("Bar", parent);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                { color = color };
        }

        /// <summary>
        /// A shingle course: flush up the slope (with a raised butt edge for the step), but
        /// with a small gap on the sides so each shingle is distinguishable.
        /// </summary>
        private GameObject CreateSteppedTile(string tileName, float localY, Color color, RoofTextureLibrary.Surface surface)
        {
            float sideZ = Mathf.Max(0.05f, Size.y - SideGap);

            var root = new GameObject(tileName);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;

            var slab = NewCube("Course", root.transform);
            slab.transform.localPosition = new Vector3(0f, localY, 0f);
            slab.transform.localScale = new Vector3(Size.x, 0.04f, sideZ);
            Paint(slab, color, surface);

            var lip = NewCube("Butt", root.transform);
            lip.transform.localPosition = new Vector3(Size.x * 0.47f, localY + 0.03f, 0f);
            lip.transform.localScale = new Vector3(Size.x * 0.16f, 0.06f, sideZ);
            Paint(lip, color * 0.82f, surface);

            return root;
        }

        /// <summary>
        /// The exposed cavity shown once the damaged plywood is pried off: fibreglass insulation
        /// filling the bay with a few rafters running up the slope, proud of it. Built inactive.
        /// </summary>
        private GameObject BuildHollow()
        {
            var root = new GameObject("Hollow");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;

            // Fibreglass insulation sitting where the board was.
            var insulation = NewCube("Insulation", root.transform);
            insulation.transform.localPosition = new Vector3(0f, 0f, 0f);
            insulation.transform.localScale = new Vector3(Size.x * 0.98f, 0.03f, Size.y * 0.9f);
            Paint(insulation, new Color(0.86f, 0.78f, 0.42f), RoofTextureLibrary.Surface.Felt);

            // Rafters along the slope (local X), proud of the insulation, ~0.4 m apart across (local Z).
            int n = Mathf.Max(2, Mathf.RoundToInt(Size.y / 0.4f));
            for (int i = 0; i <= n; i++)
            {
                float z = -Size.y * 0.5f + (i / (float)n) * Size.y;
                var rafter = NewCube("Rafter", root.transform);
                rafter.transform.localPosition = new Vector3(0f, 0.02f, z);
                rafter.transform.localScale = new Vector3(Size.x, 0.045f, 0.05f);
                Paint(rafter, WeatheredWood, RoofTextureLibrary.Surface.WoodDeck);
            }

            root.SetActive(false);
            return root;
        }

        private static GameObject NewCube(string cubeName, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = cubeName;
            go.transform.SetParent(parent, false);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // the cell root collider handles raycasts
            return go;
        }

        private static void Paint(GameObject go, Color color, RoofTextureLibrary.Surface surface)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;

            var m = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default")) { color = color };
            m.mainTexture = RoofTextureLibrary.Get(surface);
            // Tile the detail so the grain reads at a believable size on each piece.
            Vector3 sc = go.transform.localScale;
            m.mainTextureScale = new Vector2(
                Mathf.Max(0.5f, Mathf.Abs(sc.x) * 2.5f),
                Mathf.Max(0.5f, Mathf.Abs(sc.z) * 2.5f));
            r.sharedMaterial = m;
        }
    }
}
