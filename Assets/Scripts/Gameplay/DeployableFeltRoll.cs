using UnityEngine;
using RoofingSimulator.Player;
using RoofingSimulator.World;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// The physical felt roll on the roof (Etapa 3, full version). Carry a felt roll to the
    /// roof and press G while aiming at a course to deploy it there; then look at it and press
    /// E to grab it, and drag it HORIZONTALLY along the course by looking where you want it to
    /// go — it unrolls sideways (its axis points up the slope, so gravity can't roll it away
    /// downhill), laying real felt on every prepared cell it passes, spending its own units,
    /// not your hands. It can't roll over cells that still have old shingles or an open deck.
    /// Press E again to let go; aim at a roll that can't unroll (blocked or course done) and E
    /// picks it back up into your hands. When the last unit unrolls, the empty cardboard core
    /// drops as debris to toss in the dumpster.
    /// </summary>
    public class DeployableFeltRoll : MonoBehaviour, IInteractable
    {
        private const float DragSpeed = 2.2f;      // m/s the roll follows your aim, so it feels dragged
        private const float MaxHoldDistance = 5f;  // walk away and the roll slips out of your hands
        private const float RadiusFull = 0.16f, RadiusEmpty = 0.06f;
        private const float RestHeight = 0.14f;    // roll centre above the deck

        private RoofGrid grid;
        private Transform anchor;      // the roof face's unit-scaled anchor (X = up-slope, Z = along the course)
        private PlayerMaterials materials;
        private int col;               // the course (fixed column) this roll covers
        private float cellUp, cellSide; // cell size up-slope (course height) / across (travel step)
        private float minReached, maxReached; // travelled span in local Z — felt only exists inside it
        private int unitsLeft, fullUnits;

        private Transform roller;      // the spinning cylinder visual
        private bool grabbed;
        private PlayerInteractor holder;
        private int grabFrame;         // so the E press that grabbed doesn't also release
        private int releaseFrame = -1; // so the E press that released doesn't instantly re-grab

        // ----- Deploy -----

        /// <summary>Set a carried roll down on <paramref name="cell"/>'s course; it flops open on that cell.</summary>
        public static DeployableFeltRoll Deploy(RoofGrid grid, Transform anchor, RoofCell cell,
            int units, PlayerMaterials materials)
        {
            var go = new GameObject("DeployedFeltRoll");
            go.transform.SetParent(anchor, false);
            Vector3 cp = cell.transform.localPosition;
            go.transform.localPosition = new Vector3(cp.x, RestHeight, cp.z);

            var roll = go.AddComponent<DeployableFeltRoll>();
            roll.grid = grid;
            roll.anchor = anchor;
            roll.materials = materials;
            roll.col = cell.Col;
            roll.cellUp = cell.Size.x;
            roll.cellSide = cell.Size.y;
            roll.minReached = roll.maxReached = cp.z;
            roll.unitsLeft = units;
            roll.fullUnits = Mathf.Max(1, units);
            roll.BuildVisual();

            if (grid.LayFeltFromRoll(cell)) roll.unitsLeft--; // opening the roll covers its own cell
            roll.RefreshVisual();
            if (roll.unitsLeft <= 0) roll.FinishRoll();
            return roll;
        }

        private void BuildVisual()
        {
            // Interaction/physics stand-in; the raycasts of PlayerInteractor land on this.
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(cellUp, RadiusFull * 2.2f, RadiusFull * 2.5f);

            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "Roller";
            var col2 = cyl.GetComponent<Collider>();
            if (col2 != null) Destroy(col2);
            cyl.transform.SetParent(transform, false);
            cyl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // axis up the slope
            roller = cyl.transform;

            var r = cyl.GetComponent<Renderer>();
            if (r != null)
            {
                var m = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                { color = new Color(0.16f, 0.16f, 0.20f) };
                m.mainTexture = RoofTextureLibrary.Get(RoofTextureLibrary.Surface.Felt);
                m.mainTextureScale = new Vector2(2f, 2f);
                r.sharedMaterial = m;
            }
        }

        /// <summary>The roll gets visibly thinner as its felt is used up.</summary>
        private void RefreshVisual()
        {
            if (roller == null) return;
            float d = Mathf.Lerp(RadiusEmpty, RadiusFull, fullUnits <= 0 ? 0f : (float)unitsLeft / fullUnits) * 2f;
            roller.localScale = new Vector3(d, cellUp * 0.46f, d);
        }

        // ----- Interaction (E) -----

        public string Prompt => CanUnrollNow()
            ? $"Arrastrar rollo de fieltro ({unitsLeft})"
            : $"Recoger rollo de fieltro ({unitsLeft})";

        public bool CanInteract(PlayerInteractor interactor)
            => !grabbed && Time.frameCount != releaseFrame && (CanUnrollNow()
                || (unitsLeft > 0 && interactor.Materials != null && interactor.Materials.CanTake(CarryKind.FeltRoll)));

        public void Interact(PlayerInteractor interactor)
        {
            if (grabbed) return;
            if (CanUnrollNow())
            {
                grabbed = true;
                holder = interactor;
                grabFrame = Time.frameCount;
            }
            else if (unitsLeft > 0 && interactor.Materials != null
                && interactor.Materials.TryTake(CarryKind.FeltRoll, unitsLeft))
            {
                Destroy(gameObject); // blocked or course done — back into the hands
            }
        }

        /// <summary>Whether dragging from here could still lay felt: walks the course both ways past
        /// already-felted cells until a cell that needs felt (true) or an unprepared one (stop).</summary>
        private bool CanUnrollNow()
        {
            if (unitsLeft <= 0 || grid == null) return false;
            return CanUnrollToward(+1) || CanUnrollToward(-1);
        }

        private bool CanUnrollToward(int dir)
        {
            for (int r = RowOf(LocalZ()); r >= 0 && r < grid.Rows; r += dir)
            {
                RoofCell cell = grid.CellAt(r, col);
                if (cell == null) return false;
                if (!cell.IsComplete && cell.RequiredTool == RoofingTool.UnderlaymentRoller) return true;
                if (cell.Stage < CellStage.Underlayment) return false; // old shingles / open deck block the roll
            }
            return false;
        }

        // ----- Drag -----

        private void Update()
        {
            if (!grabbed) return;
            PlayerInteractor.ClaimInteraction(); // we own E while dragging

            if (holder == null
                || Vector3.Distance(holder.transform.position, transform.position) > MaxHoldDistance)
            {
                grabbed = false;
                return;
            }
            if (Time.frameCount != grabFrame && UnityEngine.Input.GetKeyDown(holder.Key))
            {
                grabbed = false; // let go where it lies
                releaseFrame = Time.frameCount;
                return;
            }

            Camera cam = holder.Cam;
            if (cam == null) return;

            // Project the crosshair onto the roof plane and chase that point along the course.
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var plane = new Plane(anchor.up, transform.position);
            if (!plane.Raycast(ray, out float enter)) return;
            float targetZ = anchor.InverseTransformPoint(ray.GetPoint(enter)).z;

            float z = LocalZ();
            float desired = Mathf.MoveTowards(z, targetZ, DragSpeed * Time.deltaTime);
            // Clamp to the reachable span, but never let the clamp push it the other way.
            if (desired > z) desired = Mathf.Max(z, Mathf.Min(desired, ReachLimit(+1)));
            else if (desired < z) desired = Mathf.Min(z, Mathf.Max(desired, ReachLimit(-1)));
            if (Mathf.Approximately(desired, z)) return;

            SetLocalZ(desired);
            minReached = Mathf.Min(minReached, desired);
            maxReached = Mathf.Max(maxReached, desired);
            if (roller != null) // spin by arc length (signed) so the surface appears to roll, not slide
                roller.Rotate(0f, (desired - z) / Mathf.Max(0.03f, roller.localScale.x * 0.5f) * Mathf.Rad2Deg, 0f, Space.Self);
            LayUnder();
        }

        /// <summary>The furthest local Z the roll may reach in <paramref name="dir"/> right now: it
        /// stops at the near edge of the first unprepared cell, or where its felt would run out.</summary>
        private float ReachLimit(int dir)
        {
            int budget = unitsLeft;
            float limit = LocalZ();
            for (int r = RowOf(LocalZ()); r >= 0 && r < grid.Rows; r += dir)
            {
                RoofCell cell = grid.CellAt(r, col);
                bool needsFelt = cell != null && !cell.IsComplete
                    && cell.RequiredTool == RoofingTool.UnderlaymentRoller;
                bool passable = cell != null
                    && (cell.Stage >= CellStage.Underlayment || (needsFelt && budget > 0));
                if (!passable)
                    return cell != null
                        ? cell.transform.localPosition.z - dir * (cellSide * 0.5f + RadiusFull)
                        : limit;
                if (needsFelt) budget--;
                limit = cell.transform.localPosition.z; // may reach this cell's centre
            }
            return limit;
        }

        /// <summary>Lay felt on every cell of the course whose centre the roll has rolled over.</summary>
        private void LayUnder()
        {
            for (int r = 0; r < grid.Rows && unitsLeft > 0; r++)
            {
                RoofCell cell = grid.CellAt(r, col);
                if (cell == null) continue;
                float cz = cell.transform.localPosition.z;
                if (cz < minReached - 0.02f || cz > maxReached + 0.02f) continue; // outside the travelled span
                if (grid.LayFeltFromRoll(cell))
                {
                    unitsLeft--;
                    RefreshVisual();
                }
            }
            if (unitsLeft <= 0) FinishRoll();
        }

        /// <summary>The felt ran out: the empty cardboard core drops as debris to go toss.</summary>
        private void FinishRoll()
        {
            Debris.Spawn(transform.position + anchor.up * 0.15f,
                new Vector3(cellUp * 0.8f, 0.11f, 0.11f), new Color(0.62f, 0.48f, 0.30f));
            materials?.NotifyDebrisSpawned();
            Destroy(gameObject);
        }

        // ----- Local-space helpers (anchor local: X = up-slope, Z = along the course) -----

        private float LocalZ() => transform.localPosition.z;

        private void SetLocalZ(float z)
        {
            Vector3 p = transform.localPosition;
            p.z = z;
            transform.localPosition = p;
        }

        /// <summary>Row whose centre is nearest the roll (rows run along the course; small counts, so scan).</summary>
        private int RowOf(float z)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int r = 0; r < grid.Rows; r++)
            {
                RoofCell cell = grid.CellAt(r, col);
                if (cell == null) continue;
                float d = Mathf.Abs(cell.transform.localPosition.z - z);
                if (d < bestDist) { bestDist = d; best = r; }
            }
            return best;
        }

        private void OnGUI()
        {
            if (!grabbed) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 14, fontStyle = FontStyle.Bold };
            GUI.Box(new Rect(Screen.width * 0.5f - 230f, Screen.height * 0.66f, 460f, 30f),
                $"Arrastra el rollo a lo largo de la hilada · fieltro: {unitsLeft} · [{(holder != null ? holder.Key : KeyCode.E)}] soltar", style);
        }
    }
}
