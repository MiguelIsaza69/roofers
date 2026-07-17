using UnityEngine;
using RoofingSimulator.Input;
using RoofingSimulator.Player;
using RoofingSimulator.World;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// Drives the "extra" tools off the same aim+click events as the roof grid:
    ///   • <b>Drill</b> — hold on each of the antenna's 4 corner <see cref="FixtureScrew"/> bolts to
    ///     unscrew them; with all four out the antenna drops as debris to toss.
    ///   • <b>Saw</b> — click the deck to score a cut along the tile division (full cut-to-size: Etapa 6).
    ///   • <b>Hammer (clavo de tapa)</b> — with the cap nail chosen (R toggles), click laid felt to pin it.
    ///   • <b>Knife</b> — click felt to trim the edge neat (small extra quality).
    ///   • <b>Chalk line</b> — click two points to snap a guide between them (chalked rows score higher).
    ///   • <b>Tape measure</b> — click two points to read the distance between them.
    /// The grid ignores all of these (they never match a cell's required tool), so there's no conflict.
    /// </summary>
    public class ToolFunctions : MonoBehaviour
    {
        private PlayerInput input;
        private RoofingToolbelt belt;
        private PlayerMaterials materials;
        private RoofGrid grid;

        private enum NailKind { Roofing, Cap }
        private NailKind nail = NailKind.Roofing; // hammer's selected nail (R toggles)

        // Two-point tools.
        private bool haveMeasureA, haveChalkA;
        private Vector3 measureA, chalkA;
        private float lastMeasure = -1f;

        // Cut-to-size decking (Etapa 6): the measure taken off a hollow, for the saw to cut to.
        private float knownCut = -1f;

        public void Bind(PlayerInput playerInput, RoofingToolbelt toolbelt, PlayerMaterials mats, RoofGrid roofGrid)
        {
            Unbind();
            input = playerInput;
            belt = toolbelt;
            materials = mats;
            grid = roofGrid;
            if (input != null)
            {
                input.OnApplyStarted += OnStart;
                input.OnApplyHeld += OnHeld;
            }
        }

        private void Unbind()
        {
            if (input != null)
            {
                input.OnApplyStarted -= OnStart;
                input.OnApplyHeld -= OnHeld;
            }
        }

        private void OnDisable() => Unbind();

        private void Update()
        {
            // Hammer's nail menu: R toggles between a roofing nail and a cap nail (for felt).
            if (belt != null && belt.Current == RoofingTool.DeckHammer && UnityEngine.Input.GetKeyDown(KeyCode.R))
                nail = nail == NailKind.Roofing ? NailKind.Cap : NailKind.Roofing;
        }

        // Continuous (hold) tools: the drill grinds out a bolt.
        private void OnHeld(RoofingRaycastHit hit)
        {
            if (belt == null || belt.Current != RoofingTool.Drill) return;
            FixtureScrew screw = hit.collider != null ? hit.collider.GetComponentInParent<FixtureScrew>() : null;
            if (screw != null && screw.AddWork(Time.deltaTime))
                materials?.NotifyDebrisSpawned(); // the antenna just came loose -> counts toward cleanliness
        }

        // One-shot (per click) tools.
        private void OnStart(RoofingRaycastHit hit)
        {
            if (belt == null) return;
            switch (belt.Current)
            {
                case RoofingTool.Saw:
                {
                    RoofCell cell = Cell(hit);
                    if (cell != null && cell.Stage == CellStage.DeckRemoved) TryCutToHole(cell, hit.point);
                    else if (cell != null) GridFor(cell)?.SawCut(cell); // cosmetic kerf on ordinary deck
                    break;
                }
                case RoofingTool.DeckHammer:
                {
                    if (nail == NailKind.Cap) // cap nail pins the laid felt
                    {
                        RoofCell cell = Cell(hit);
                        if (cell != null) GridFor(cell)?.SecureFelt(cell);
                    }
                    break;
                }
                case RoofingTool.UtilityKnife:
                {
                    RoofCell cell = Cell(hit);
                    if (cell != null) GridFor(cell)?.TrimFelt(cell);
                    break;
                }
                case RoofingTool.ChalkLine:
                    ChalkPoint(hit.point);
                    break;
                case RoofingTool.TapeMeasure:
                {
                    // On an open hollow, one click reads the hole's plank measure (Etapa 6);
                    // anywhere else it's the usual two-point measure.
                    RoofCell cell = Cell(hit);
                    RoofGrid owner = cell != null ? GridFor(cell) : null;
                    if (cell != null && cell.Stage == CellStage.DeckRemoved && owner != null)
                    {
                        knownCut = owner.HoleMeasure(cell);
                        haveMeasureA = false;
                        lastMeasure = -1f;
                        HudNotice.Show($"Hueco medido: plancha de {knownCut:0.0} m — corta con la sierra", 3.2f);
                    }
                    else Measure(hit.point);
                    break;
                }
            }
        }

        private static RoofCell Cell(RoofingRaycastHit hit)
            => hit.collider != null ? hit.collider.GetComponentInParent<RoofCell>() : null;

        /// <summary>The grid that owns the cell (multi-face jobs, Etapa D4) — bound grid as fallback.</summary>
        private RoofGrid GridFor(RoofCell cell) => RoofGrid.GridOf(cell) ?? grid;

        /// <summary>
        /// Saw on an open hollow: cut the carried sheet down to the hole's measure — but only
        /// after that measure was taken with the tape (mide → corta → coloca). The offcut drops
        /// next to you: still a usable plank if it's ≥1.2 m, otherwise scrap to toss.
        /// </summary>
        private void TryCutToHole(RoofCell cell, Vector3 at)
        {
            RoofGrid owner = GridFor(cell);
            if (owner == null || materials == null) return;
            float need = owner.HoleMeasure(cell);
            if (need <= 0f) return;

            if (materials.Carrying != CarryKind.Plank)
            {
                HudNotice.Show("Trae una plancha en las manos para cortarla");
                return;
            }
            float have = materials.PlankLength;
            if (Mathf.Abs(have - need) <= 0.06f)
            {
                HudNotice.Show($"La plancha ya está a medida ({need:0.0} m) — colócala con la mano (5)");
                return;
            }
            if (have < need)
            {
                HudNotice.Show($"La plancha ({have:0.0} m) no alcanza — el hueco pide {need:0.0} m");
                return;
            }
            if (Mathf.Abs(knownCut - need) > 0.06f)
            {
                HudNotice.Show("Mide el hueco con el metro primero");
                return;
            }

            materials.CutPlank(need);
            float off = have - need;
            if (off >= 1.15f)
            {
                DroppedMaterial.Spawn(at + Vector3.up * 0.35f, CarryKind.Plank, 1, 0f, 0f, off);
                HudNotice.Show($"Cortada a {need:0.0} m — retazo útil de {off:0.0} m al lado", 3.2f);
            }
            else
            {
                Debris.Spawn(at + Vector3.up * 0.35f,
                    new Vector3(0.5f, 0.05f, Mathf.Max(0.3f, off)), new Color(0.72f, 0.56f, 0.34f));
                materials.NotifyDebrisSpawned();
                HudNotice.Show($"Cortada a {need:0.0} m — el retazo sobrante es escombro", 3.2f);
            }
        }

        private void ChalkPoint(Vector3 p)
        {
            if (!haveChalkA) { chalkA = p; haveChalkA = true; }
            else { haveChalkA = false; (RoofGrid.GridNear(p) ?? grid)?.ChalkWorldLine(chalkA, p); }
        }

        private void Measure(Vector3 p)
        {
            if (!haveMeasureA)
            {
                measureA = p;
                haveMeasureA = true;
                lastMeasure = 0f;
            }
            else
            {
                lastMeasure = Vector3.Distance(measureA, p);
                haveMeasureA = false;
                DrawLine(measureA, p, new Color(1f, 0.85f, 0.1f), 5f);
            }
        }

        private static void DrawLine(Vector3 a, Vector3 b, Color color, float life)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "ToolLine";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.position = (a + b) * 0.5f;
            Vector3 dir = b - a;
            if (dir.sqrMagnitude > 0.0001f) go.transform.up = dir.normalized;
            go.transform.localScale = new Vector3(0.02f, Mathf.Max(0.01f, dir.magnitude * 0.5f), 0.02f);
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default")) { color = color };
            if (life > 0f) Destroy(go, life);
        }

        private void OnGUI()
        {
            if (belt == null) return;

            if (belt.Current == RoofingTool.TapeMeasure && lastMeasure >= 0f)
            {
                var style = new GUIStyle(GUI.skin.box) { fontSize = 16, fontStyle = FontStyle.Bold };
                string txt = haveMeasureA ? "Metro: marca el segundo punto…" : $"Metro: {lastMeasure:0.00} m";
                GUI.Box(new Rect(Screen.width * 0.5f - 130f, Screen.height * 0.55f, 260f, 30f), txt, style);
            }
            else if (belt.Current == RoofingTool.ChalkLine && haveChalkA)
            {
                var style = new GUIStyle(GUI.skin.box) { fontSize = 16, fontStyle = FontStyle.Bold };
                GUI.Box(new Rect(Screen.width * 0.5f - 130f, Screen.height * 0.55f, 260f, 30f), "Tiza: marca el segundo punto…", style);
            }
            else if (belt.Current == RoofingTool.DeckHammer)
            {
                var style = new GUIStyle(GUI.skin.box) { fontSize = 14, fontStyle = FontStyle.Bold };
                string n = nail == NailKind.Roofing ? "Clavo de techo (madera)" : "Clavo de tapa (fija fieltro)";
                GUI.Box(new Rect(Screen.width * 0.5f - 160f, Screen.height * 0.62f, 320f, 28f), $"Clavo: {n}    [R cambia]", style);
            }
        }
    }
}
