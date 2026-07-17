using System.Collections.Generic;
using UnityEngine;
using RoofingSimulator.Player;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// The player's tool inventory: a 4-slot hotbar. You only carry four tools at a time and
    /// swap the rest at the ground tool rack (see <c>World.ToolRack</c>). Slots are selected
    /// with the 1-4 keys or the scroll wheel. Also draws the hotbar, a crosshair and the
    /// roof/material readout so the prototype stays playable before the real HUD exists.
    /// </summary>
    public class RoofingToolbelt : MonoBehaviour
    {
        public const int SlotCount = 4;

        // The four tools currently in reach. Starts with the tear-off / deck / felt set; swap in
        // the hand, nailer and specialty tools at the rack.
        private readonly RoofingTool[] slots =
        {
            RoofingTool.TearOffShovel,
            RoofingTool.PryBar,
            RoofingTool.DeckHammer,
            RoofingTool.UnderlaymentRoller
        };
        private int activeSlot;
        private bool usingHands; // the hand (ShingleHand) is always available via key 5

        private RoofGrid grid;
        private PlayerMaterials materials;

        public RoofingTool Current => usingHands ? RoofingTool.ShingleHand : slots[activeSlot];
        public int ActiveSlot => activeSlot;
        public IReadOnlyList<RoofingTool> Slots => slots;

        public bool Has(RoofingTool tool)
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i] == tool) return true;
            return false;
        }

        /// <summary>Put a tool into the active slot; returns the tool it displaced (goes back to the rack).</summary>
        public RoofingTool EquipToActiveSlot(RoofingTool incoming)
        {
            RoofingTool displaced = slots[activeSlot];
            slots[activeSlot] = incoming;
            return displaced;
        }

        public void SetGrid(RoofGrid roofGrid) => grid = roofGrid;

        // All faces of the job (Etapa D4): the HUD progress line aggregates them.
        private RoofGrid[] grids;
        public void SetGrids(RoofGrid[] roofGrids)
        {
            grids = roofGrids;
            if (grids != null && grids.Length > 0) grid = grids[0];
        }

        /// <summary>Totals across every face (falls back to the single grid).</summary>
        private void Aggregate(out int done, out int total, out float quality)
        {
            done = 0; total = 0; quality = 1f;
            if (grids == null || grids.Length == 0)
            {
                if (grid == null) return;
                done = grid.Completed; total = grid.Total; quality = grid.AverageQuality;
                return;
            }
            float qSum = 0f;
            foreach (RoofGrid g in grids)
            {
                if (g == null) continue;
                done += g.Completed;
                total += g.Total;
                qSum += g.AverageQuality * g.Completed;
            }
            quality = done > 0 ? qSum / done : 1f;
        }

        /// <summary>Worst snow across the faces (drives the shovel hint).</summary>
        private float MaxSnow()
        {
            if (grids == null || grids.Length == 0) return grid != null ? grid.AverageSnow : 0f;
            float s = 0f;
            foreach (RoofGrid g in grids)
                if (g != null) s = Mathf.Max(s, g.AverageSnow);
            return s;
        }

        public void SetMaterials(PlayerMaterials playerMaterials) => materials = playerMaterials;

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1)) { activeSlot = 0; usingHands = false; }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2)) { activeSlot = 1; usingHands = false; }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3)) { activeSlot = 2; usingHands = false; }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4)) { activeSlot = 3; usingHands = false; }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha5)) usingHands = true; // bare hands (always available)

            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                usingHands = false;
                activeSlot = (activeSlot + (scroll > 0f ? 1 : SlotCount - 1)) % SlotCount;
            }
        }

        private void OnGUI()
        {
            // Crosshair.
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(cx - 2, cy - 2, 4, 4), Texture2D.whiteTexture);

            // 4-slot hotbar (active slot highlighted).
            var slotStyle = new GUIStyle(GUI.skin.box) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            const float sw = 168f, sh = 28f, x0 = 16f, y0 = 16f;
            Color hi = new Color(1f, 0.9f, 0.4f), dim = new Color(1f, 1f, 1f, 0.7f);
            for (int i = 0; i < slots.Length; i++)
            {
                Color prev = GUI.color;
                GUI.color = (!usingHands && i == activeSlot) ? hi : dim;
                GUI.Box(new Rect(x0, y0 + i * (sh + 3f), sw, sh), $" {i + 1}  {Name(slots[i])}", slotStyle);
                GUI.color = prev;
            }
            // The hand is always available (key 5), shown as a fifth, fixed slot.
            {
                Color prev = GUI.color;
                GUI.color = usingHands ? hi : dim;
                GUI.Box(new Rect(x0, y0 + slots.Length * (sh + 3f), sw, sh), " 5  Manos (poner teja)", slotStyle);
                GUI.color = prev;
            }

            var style = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            float infoY = y0 + (slots.Length + 1) * (sh + 3f) + 8f;
            if (grid != null)
            {
                Aggregate(out int done, out int total, out float qual);
                float pct = total > 0 ? 100f * done / total : 0f;
                GUI.Label(new Rect(16, infoY, 580, 22),
                    $"Techo: {done}/{total} celdas ({pct:0}%) · calidad {qual:P0}", style);
            }
            if (materials != null)
            {
                GUI.Label(new Rect(16, infoY + 22, 720, 22), $"En manos: {materials.CarryLabel}   ([G] soltar)", style);
                if (materials.CarryingDebris)
                {
                    var warn = new GUIStyle(style) { normal = { textColor = new Color(1f, 0.8f, 0.3f) } };
                    GUI.Label(new Rect(16, infoY + 44, 720, 22),
                        "Manos ocupadas con escombros — ¡tíralos en el contenedor!", warn);
                }
            }
            // Transient one-line notices from any system ("mide el hueco primero", etc.).
            string notice = HudNotice.Current;
            if (!string.IsNullOrEmpty(notice))
            {
                var ns = new GUIStyle(GUI.skin.box) { fontSize = 15, fontStyle = FontStyle.Bold };
                GUI.Box(new Rect(Screen.width * 0.5f - 280f, Screen.height * 0.50f, 560f, 30f), notice, ns);
            }

            // With the hand out, teach the stair-step order that the green frames are showing.
            if (Current == RoofingTool.ShingleHand)
            {
                var hint = new GUIStyle(GUI.skin.box) { fontSize = 13, fontStyle = FontStyle.Bold };
                GUI.Box(new Rect(Screen.width * 0.5f - 250f, Screen.height * 0.72f, 500f, 28f),
                    "Tejas: sigue el marco VERDE — esquina del alero y en diagonal · fuera de orden = -25% calidad", hint);
            }
            // With the nail gun out, teach the precision zoom.
            else if (Current == RoofingTool.NailGun)
            {
                var hint = new GUIStyle(GUI.skin.box) { fontSize = 13, fontStyle = FontStyle.Bold };
                GUI.Box(new Rect(Screen.width * 0.5f - 250f, Screen.height * 0.72f, 500f, 28f),
                    "Clavadora: mantén CLICK DERECHO para acercar y clavar con precisión (centro = mejor pago)", hint);
            }
            // With the shovel out on a snowed roof, teach the snow clearing (Etapa C2).
            else if (Current == RoofingTool.TearOffShovel && MaxSnow() > 0.35f)
            {
                var hint = new GUIStyle(GUI.skin.box) { fontSize = 13, fontStyle = FontStyle.Bold };
                GUI.Box(new Rect(Screen.width * 0.5f - 250f, Screen.height * 0.72f, 500f, 28f),
                    "Pala: quita la NIEVE de las celdas tapadas (1-2 paladas) antes de trabajarlas", hint);
            }

            GUI.Label(new Rect(16, Screen.height - 26, 1280, 22),
                "WASD mover · Shift correr · Espacio saltar · Ctrl agachar · F arnés · E recoger/tomar herramienta · G soltar (rollo mirando al techo = desplegar) · 1-4 ranura · 5 manos · Click usar", style);
        }

        /// <summary>Short display name (no slot number) — shared with the ground tool rack.</summary>
        public static string Name(RoofingTool t) => t switch
        {
            RoofingTool.TearOffShovel => "Pala",
            RoofingTool.PryBar => "Palanca",
            RoofingTool.DeckHammer => "Martillo",
            RoofingTool.UnderlaymentRoller => "Rodillo",
            RoofingTool.ShingleHand => "Mano (teja)",
            RoofingTool.NailGun => "Clavadora",
            RoofingTool.Drill => "Taladro",
            RoofingTool.Saw => "Cortadora",
            RoofingTool.TapeMeasure => "Metro",
            RoofingTool.UtilityKnife => "Cuchilla",
            RoofingTool.ChalkLine => "Tiza",
            _ => t.ToString()
        };
    }
}
