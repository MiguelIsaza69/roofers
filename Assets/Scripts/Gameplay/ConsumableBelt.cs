using System;
using System.Collections.Generic;
using UnityEngine;
using RoofingSimulator.Input;
using RoofingSimulator.Player;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// The in-level consumable belt (Fase E, Etapa E2): [X] cycles the selected consumable,
    /// [Q] spends one charge — de-icing salt (melts snow around the aimed point), coffee
    /// (stamina top-up + regen boost) and the emergency rope (absorbs one hard fall).
    /// Charges live on the career: this component never reads it directly — the scene
    /// controller hands in count/use callbacks, keeping the save logic in Core.
    /// </summary>
    public class ConsumableBelt : MonoBehaviour
    {
        private const float SaltRadius = 2.4f; // metres around the aimed point

        // (id, label) in cycle order — ids must match Core.Shop constants.
        private static readonly (string id, string label)[] Kinds =
        {
            ("salt", "Sal de deshielo"),
            ("coffee", "Termo de café"),
            ("rope", "Cuerda de emergencia"),
        };

        private PlayerInput input;
        private PlayerLocomotion loco;
        private PlayerStamina stamina;
        private LedgeGrabAndFall ledge;
        private List<RoofGrid> grids;
        private Func<string, bool> useCharge;
        private Func<string, int> chargeCount;
        private int selected;

        public void Bind(PlayerInput playerInput, PlayerLocomotion locomotion,
            List<RoofGrid> roofGrids, Func<string, bool> use, Func<string, int> count)
        {
            input = playerInput;
            loco = locomotion;
            grids = roofGrids;
            useCharge = use;
            chargeCount = count;
            if (locomotion != null)
            {
                stamina = locomotion.GetComponent<PlayerStamina>();
                ledge = locomotion.GetComponent<LedgeGrabAndFall>();
            }
        }

        private int Count(string id) => chargeCount != null ? chargeCount(id) : 0;

        private int TotalCharges()
        {
            int t = 0;
            foreach (var k in Kinds) t += Count(k.id);
            return t;
        }

        private void Update()
        {
            if (useCharge == null || TotalCharges() == 0) return;

            // Make sure the selection points at something we actually carry.
            if (Count(Kinds[selected].id) == 0) CycleToOwned();

            if (UnityEngine.Input.GetKeyDown(KeyCode.X)) CycleToOwned();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Q)) UseSelected();
        }

        private void CycleToOwned()
        {
            for (int step = 1; step <= Kinds.Length; step++)
            {
                int i = (selected + step) % Kinds.Length;
                if (Count(Kinds[i].id) > 0)
                {
                    selected = i;
                    return;
                }
            }
        }

        private void UseSelected()
        {
            string id = Kinds[selected].id;
            if (Count(id) <= 0) return;

            switch (id)
            {
                case "salt": UseSalt(); break;
                case "coffee": UseCoffee(); break;
                case "rope": UseRope(); break;
            }
        }

        /// <summary>Salt: melt the snow off every cell near the aimed point (or your feet).
        /// Doesn't consume a charge if there was nothing to melt.</summary>
        private void UseSalt()
        {
            Vector3 at = input != null && input.HasValidTarget
                ? input.CurrentHit.point
                : loco != null ? loco.transform.position : transform.position;

            int cleared = 0;
            if (grids != null)
            {
                foreach (RoofGrid g in grids)
                {
                    if (g == null) continue;
                    for (int r = 0; r < g.Rows; r++)
                    {
                        for (int c = 0; c < g.Cols; c++)
                        {
                            RoofCell cell = g.CellAt(r, c);
                            if (cell == null || cell.SnowDepth <= 0.05f) continue;
                            if ((cell.transform.position - at).sqrMagnitude > SaltRadius * SaltRadius) continue;
                            cell.AddSnow(-1f);
                            cleared++;
                        }
                    }
                }
            }

            if (cleared == 0)
            {
                HudNotice.Show("No hay nieve que salar aquí — apunta a las celdas tapadas");
                return;
            }
            if (useCharge("salt"))
                HudNotice.Show($"Sal esparcida — {cleared} celda{(cleared > 1 ? "s" : "")} despejada{(cleared > 1 ? "s" : "")}", 3f);
        }

        private void UseCoffee()
        {
            if (stamina == null) return;
            if (!useCharge("coffee")) return;
            stamina.DrinkCoffee(75f);
            HudNotice.Show("☕ Café: estamina arriba y regeneración ×1.5 (75 s)", 3f);
        }

        /// <summary>Rope: arm it once — it absorbs the next hard fall. No double-arming.</summary>
        private void UseRope()
        {
            if (ledge == null) return;
            if (ledge.RopeArmed)
            {
                HudNotice.Show("La cuerda de emergencia ya está armada");
                return;
            }
            if (!useCharge("rope")) return;
            ledge.ArmEmergencyRope();
            HudNotice.Show("Cuerda de emergencia ARMADA — absorbe tu próxima caída dura", 3.5f);
        }

        private void OnGUI()
        {
            int total = TotalCharges();
            if (total == 0) return;

            var chip = new GUIStyle(GUI.skin.box) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            string label = Kinds[selected].label;
            int n = Count(Kinds[selected].id);
            string extra = Kinds[selected].id == "rope" && ledge != null && ledge.RopeArmed ? " (ARMADA)" : string.Empty;
            GUI.Box(new Rect(16f, Screen.height - 42f, 340f, 28f),
                $" {label} ×{n}{extra} · [Q] usar · [X] cambiar", chip);
        }
    }
}
