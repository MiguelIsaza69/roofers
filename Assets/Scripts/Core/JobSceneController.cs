using UnityEngine;
using RoofingSimulator.Gameplay;
using RoofingSimulator.Player;
using RoofingSimulator.World;

namespace RoofingSimulator.Core
{
    /// <summary>
    /// Builds and drives the first-person roofing playtest scene. On Start it generates a
    /// house (walls + sloped roof + ladder), spawns a full first-person player on the
    /// ground, tiles the roof into shingle cells, and wires the player's tool input to the
    /// grid. The job ends when the roof is finished (press the finish key) or the player
    /// abandons.
    ///
    /// Everything is created procedurally so the scene is playable on its own — open
    /// RoofingJob.unity, press Play, and you can walk, climb and roof immediately. Career/
    /// HUD/coverage hookup from the legacy putty flow is reconnected in a later pass.
    /// </summary>
    public class JobSceneController : MonoBehaviour
    {
        [Header("Roof tiles (shingles)")]
        [Tooltip("Tiles up the slope (vertical). Tiles are sized to fill the roof, so this is a count, not a size.")]
        [SerializeField] private int tilesUpSlope = 6;
        [Tooltip("Tiles across the roof (horizontal).")]
        [SerializeField] private int tilesAcross = 10;

        [Header("Controls")]
        [SerializeField] private KeyCode finishKey = KeyCode.Return;
        // Backspace (not Esc) so it doesn't clash with the Editor's "free the mouse" key.
        [SerializeField] private KeyCode abandonKey = KeyCode.Backspace;

        private RoofGrid roofGrid; // primary face (obstacles, tile size, lightning ridge)
        private readonly System.Collections.Generic.List<RoofGrid> allGrids
            = new System.Collections.Generic.List<RoofGrid>(); // every face of the job (Etapa D4)
        private PlayerRig player;
        private Transform dumpster;
        private bool roofComplete;
        private bool jobEnded;
        private bool paid;
        private int payAmount;
        private float payQuality = 1f;
        private float payCleanliness = 1f;
        private float jobStartTime;

        // Contract board (Fase D): the scene opens on a choice of generated offers and
        // only builds the world once one is taken.
        private ContractOffer[] offers;
        private ContractOffer contract;
        private bool choosing;
        private Camera boardCam;

        // Fines + retry (Etapa D2): abandoning a taken contract costs a cut of the
        // expected pay (debt allowed) and the same house can be retried from scratch.
        private bool confirmingAbandon;
        private bool rebuilding;
        private int attempt = 1;
        private string boardNotice;
        private GameObject[] baselineRoots;

        // Shop (Fase E): browsable from the contract board, purchases persist on the career.
        private bool shopping;
        private string shopNotice;

        // Material budget (Etapa E3): the contract includes an allowance per material;
        // restocking within it is free, only the EXCESS is deducted from the pay.
        private int shingleBudget;
        private int feltBudget;
        private int woodBudget;
        private int materialPenalty;

        private void Start()
        {
            jobStartTime = Time.time;
            EnsureCareerForPlaytest();
            DisableLegacyCameras();
            // Everything BuildWorld creates is a scene root that doesn't exist yet, so a
            // snapshot of the current roots is enough to tear the world down later (D2).
            baselineRoots = gameObject.scene.GetRootGameObjects();
            RollOffers();
        }

        /// <summary>Roll (or re-roll) the contract board. The world isn't built yet.</summary>
        private void RollOffers()
        {
            offers = LevelGenerator.Roll(CareerProgress());
            choosing = true;
            if (boardCam == null)
            {
                // A bare backdrop camera so the board renders before any world exists.
                boardCam = new GameObject("ContractBoardCamera").AddComponent<Camera>();
                boardCam.clearFlags = CameraClearFlags.SolidColor;
                boardCam.backgroundColor = new Color(0.09f, 0.10f, 0.12f);
            }
        }

        /// <summary>Take an offer: tear down the board and build the world it describes.</summary>
        private void StartContract(ContractOffer offer)
        {
            contract = offer;
            choosing = false;
            boardNotice = null;
            if (boardCam != null) Destroy(boardCam.gameObject);
            BuildWorld();
            jobStartTime = Time.time; // the clock starts when the work does
        }

        private void BuildWorld()
        {
            BuiltHouse house = HouseBuilder.Build(contract.spec, Vector3.zero);

            // Spawn the player on the ground facing the house/ladder.
            Vector3 toHouse = house.root.position - house.playerSpawn;
            toHouse.y = 0f;
            Quaternion facing = toHouse.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(toHouse)
                : Quaternion.identity;
            player = PlayerRigBuilder.Build(house.playerSpawn, facing);

            BuildRoofGrid(house);
            BuildEquipmentArea(house);
            SpawnRoofFixtures(house);
            SpawnCrows(house);

            // Fase C: staged dynamic weather — now driven by the contract's forecast
            // (severity skews the phases nastier; climate honours the advertised cold/heat).
            var weather = WeatherSystem.Spawn(player.locomotion, player.root.transform, roofGrid,
                contract.weatherSeverity, contract.climate);
            // Every extra face gets snow/melt too (Etapa D4).
            foreach (var g in allGrids)
                if (g != roofGrid) weather.RegisterGrid(g);

            ApplyOwnedGear(weather);

            // Fase E2: the consumable belt ([Q] use / [X] cycle). Charges live on the
            // career — the belt only sees callbacks, never the save.
            var consumables = new GameObject("ConsumableBelt").AddComponent<ConsumableBelt>();
            consumables.Bind(player.input, player.locomotion, allGrids, TryUseCharge, ChargeCount);

            // Etapa E3: size the material allowance to the REAL roof just built (cells,
            // damaged blocks) plus a working margin. Only exceeding it costs.
            int cells = AllCellsTotal();
            int blocks = 0;
            foreach (var g in allGrids)
                if (g != null) blocks += g.DamagedBlocks;
            shingleBudget = Mathf.CeilToInt(cells * 1.25f);
            feltBudget = Mathf.CeilToInt(cells * 1.35f);
            woodBudget = blocks + Mathf.Max(1, Mathf.CeilToInt(blocks * 0.34f));
            materialPenalty = 0;
            HudNotice.Show($"Material incluido: {shingleBudget} tejas · {feltBudget} fieltro · "
                + $"{woodBudget} plancha{(woodBudget > 1 ? "s" : "")} — el EXTRA se descuenta del pago", 6f);
        }

        /// <summary>What the material taken beyond the allowance costs right now (E3).</summary>
        private int MaterialOverageCost()
        {
            if (player.materials == null) return 0;
            int overShingles = Mathf.Max(0, player.materials.Restocked(MaterialKind.Shingles) - shingleBudget);
            int overFelt = Mathf.Max(0, player.materials.Restocked(MaterialKind.Felt) - feltBudget);
            int overWood = Mathf.Max(0, player.materials.Restocked(MaterialKind.Wood) - woodBudget);
            return overShingles * 4 + overFelt * 3 + overWood * 12;
        }

        /// <summary>Fase E: wire the career's bought gear into the freshly built world.</summary>
        private void ApplyOwnedGear(WeatherSystem weather)
        {
            if (player.locomotion != null)
            {
                if (HasGear(Shop.Boots)) player.locomotion.SetSlipResistance(0.45f);
                if (HasGear(Shop.Harness))
                {
                    var ledge = player.locomotion.GetComponent<LedgeGrabAndFall>();
                    if (ledge != null) ledge.SetFallProtection(0.4f);
                }
            }
            if (weather != null)
                weather.SetProtection(HasGear(Shop.Thermal) ? 0.5f : 0f, HasGear(Shop.Canteen) ? 0.5f : 0f);
            if (HasGear(Shop.NailGun))
                foreach (var g in allGrids)
                    if (g != null) g.SetPrecisionAid(1f);
        }

        private static bool HasGear(string id)
        {
            try
            {
                return CareerManager.Instance.HasUpgrade(id);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static int ChargeCount(string id)
        {
            try
            {
                return CareerManager.Instance.ConsumableCount(id);
            }
            catch (System.Exception)
            {
                return 0;
            }
        }

        private static bool TryUseCharge(string id)
        {
            try
            {
                return CareerManager.Instance.TryUseConsumable(id);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        /// <summary>Buy from the board shop (Fase E). Purchases never create debt.</summary>
        private void TryBuy(ShopItem item)
        {
            try
            {
                if (item.charges > 0) // consumable pack (Etapa E2): stacks, no ownership cap
                {
                    shopNotice = CareerManager.Instance.TryBuyConsumable(item.id, item.price, item.charges)
                        ? $"¡{item.title} ×{item.charges} a la bolsa! (-${item.price}) — en obra: [Q] usar, [X] cambiar"
                        : $"No te alcanza para {item.title} (${item.price}) — la tienda no fía";
                }
                else if (CareerManager.Instance.HasUpgrade(item.id))
                {
                    shopNotice = $"{item.title}: ya está en tu equipo";
                }
                else if (CareerManager.Instance.TryBuyUpgrade(item.id, item.price))
                {
                    shopNotice = $"¡Comprada! {item.title} (-${item.price}) — se aplica en el próximo contrato";
                }
                else
                {
                    shopNotice = $"No te alcanza para {item.title} (${item.price}) — la tienda no fía";
                }
            }
            catch (System.Exception e)
            {
                shopNotice = "Tienda no disponible (sin career activo)";
                Debug.LogWarning($"Shop unavailable: {e.Message}");
            }
        }

        /// <summary>Difficulty seed for the board: how far this career has come.</summary>
        private static int CareerProgress()
        {
            try
            {
                int progress = CareerManager.Instance.HasActiveCareer
                    ? CareerManager.Instance.ActiveCareer.totalJobsCompleted : 0;
                return Mathf.Max(progress, GameManager.Instance.SelectedJobIndex);
            }
            catch (System.Exception)
            {
                return 0;
            }
        }

        /// <summary>Ambient crows perched on the ridge and dumpster rim; they flee when approached.</summary>
        private void SpawnCrows(BuiltHouse house)
        {
            if (house.ridgeAnchor == null || player.materials == null) return;
            Vector3 ridge = house.ridgeAnchor.position;
            // Perch along the REAL ridge — hips (Etapa D5) shorten it to depth - width.
            float halfD = Mathf.Max(0.2f, house.ridgeLength * 0.5f - 0.6f);

            var perches = new System.Collections.Generic.List<Vector3>
            {
                ridge + new Vector3(0f, 0.32f, halfD * 0.8f),
                ridge + new Vector3(0f, 0.32f, 0f),
                ridge + new Vector3(0f, 0.32f, -halfD * 0.8f),
            };
            if (dumpster != null) perches.Add(dumpster.position + new Vector3(0.4f, 1.35f, 0.6f));

            AmbientCrows.Spawn(perches.ToArray(), player.materials.transform);
        }

        private void Update()
        {
            if (jobEnded || rebuilding) return;

            // Contract board input (Fase D): pick with 1-3, re-roll with R, shop with T.
            if (choosing)
            {
                // Shop (Fase E): number keys buy (1-5 gear, 6-8 consumable packs);
                // T or Backspace goes back to the board.
                if (shopping)
                {
                    for (int i = 0; i < Shop.Items.Length; i++)
                        if (UnityEngine.Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                            TryBuy(Shop.Items[i]);
                    for (int i = 0; i < Shop.Packs.Length; i++)
                        if (UnityEngine.Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha6 + i)))
                            TryBuy(Shop.Packs[i]);
                    if (UnityEngine.Input.GetKeyDown(KeyCode.T) || UnityEngine.Input.GetKeyDown(abandonKey))
                    {
                        shopping = false;
                        shopNotice = null;
                    }
                    return;
                }
                if (offers != null)
                {
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) && offers.Length > 0) StartContract(offers[0]);
                    else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) && offers.Length > 1) StartContract(offers[1]);
                    else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3) && offers.Length > 2) StartContract(offers[2]);
                    else if (UnityEngine.Input.GetKeyDown(KeyCode.R)) RollOffers();
                    else if (UnityEngine.Input.GetKeyDown(KeyCode.T)) shopping = true;
                    else if (UnityEngine.Input.GetKeyDown(abandonKey)) ReturnToCareer();
                }
                return;
            }

            // Abandon confirmation (Etapa D2): walking away from a taken contract
            // costs a fine — retry the same house or go back to the board.
            if (confirmingAbandon)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.R)) PayFineAndRetry();
                else if (UnityEngine.Input.GetKeyDown(abandonKey)) PayFineAndReturnToBoard();
                else if (UnityEngine.Input.GetKeyDown(finishKey)) confirmingAbandon = false;
                return;
            }

            if (roofComplete && !paid && UnityEngine.Input.GetKeyDown(finishKey))
            {
                FinishAndPay();
            }
            else if (UnityEngine.Input.GetKeyDown(abandonKey))
            {
                // Once the job is handed in (paid) leaving is free; before that it's a breach.
                if (paid && !MenuFlow())
                {
                    // Standalone playtest: straight to the next board — the REPO loop
                    // continues with offers scaled by the job just completed.
                    attempt = 1;
                    contract = null;
                    boardNotice = null;
                    TearDownWorld();
                    RollOffers();
                }
                else if (paid || contract == null) ReturnToCareer();
                else confirmingAbandon = true;
            }
        }

        /// <summary>True when the scene was entered through the menus (a job was selected).</summary>
        private static bool MenuFlow()
        {
            try
            {
                return GameManager.Instance.SelectedJobIndex >= 0;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        // ----- Setup -----

        private void BuildRoofGrid(BuiltHouse house)
        {
            allGrids.Clear();

            // The contract dictates how rotten the deck is and how many real ~1 m courses
            // the roof has; the serialized counts stay as the fallback.
            int up = contract != null ? contract.tilesUpSlope : tilesUpSlope;
            int across = contract != null ? contract.tilesAcross : tilesAcross;

            // Hip roofs (Etapa D5): the main faces are trapezoids and the ends triangles —
            // cells outside the polygon never get built (same rule the board counted with).
            bool hip = contract != null && contract.hipRoof && house.endFaceFront != null;
            float hipHalfW = hip ? contract.spec.width * 0.5f : 0f;
            float hipPitch = hip ? contract.spec.roofPitchDegrees : 0f;
            Vector2 mainSize = house.roofFaceSize;
            System.Func<int, int, bool> mainKeep = hip
                ? (r, c) => HipRoofMath.Inside(r, c, across, up, mainSize, hipPitch, hipHalfW, false)
                : (System.Func<int, int, bool>)null;

            roofGrid = BuildOneGrid("RoofGrid", house.roofFace, house.roofFaceSize, up, across, mainKeep);

            // Etapa D4 — the far slope and the L-wing are grids of their own; they share
            // the player's input and each one answers only for its own cells.
            if (contract != null && contract.bothFaces && house.roofFaceLeft != null)
                BuildOneGrid("RoofGrid_L", house.roofFaceLeft, house.roofFaceSize, up, across, mainKeep);
            if (hip && house.endFaceBack != null)
            {
                int endAcross = contract.endTilesAcross;
                Vector2 endSize = house.endFaceSize;
                System.Func<int, int, bool> endKeep =
                    (r, c) => HipRoofMath.Inside(r, c, endAcross, up, endSize, hipPitch, hipHalfW, true);
                BuildOneGrid("RoofGrid_EndF", house.endFaceFront, house.endFaceSize, up, endAcross, endKeep);
                BuildOneGrid("RoofGrid_EndB", house.endFaceBack, house.endFaceSize, up, endAcross, endKeep);
            }
            if (contract != null && contract.lWing && house.wingFaceA != null && house.wingFaceB != null)
            {
                BuildOneGrid("WingGrid_A", house.wingFaceA, house.wingFaceSize,
                    contract.wingTilesUpSlope, contract.wingTilesAcross);
                BuildOneGrid("WingGrid_B", house.wingFaceB, house.wingFaceSize,
                    contract.wingTilesUpSlope, contract.wingTilesAcross);
            }

            player.toolbelt.SetGrid(roofGrid);
            player.toolbelt.SetGrids(allGrids.ToArray());
            player.toolbelt.SetMaterials(player.materials);
            // Dropped shingles/debris use the real cell size the grid just computed.
            if (player.materials != null) player.materials.SetTileSize(roofGrid.CellUp, roofGrid.CellSide);

            // Wire the extra-tool functions (drill/saw fixtures, chalk, measure, cap nailer, knife)
            // onto the same click input the grid uses.
            var toolFunctions = new GameObject("ToolFunctions").AddComponent<ToolFunctions>();
            toolFunctions.Bind(player.input, player.toolbelt, player.materials, roofGrid);

            // Etapa 8: hold right-click with the nail gun for the precision close-up.
            var nailZoom = new GameObject("NailZoom").AddComponent<NailZoom>();
            nailZoom.Bind(player.camera, player.toolbelt, player.input, roofGrid);
        }

        /// <summary>One face's grid: build, bind to the shared input and track it (Etapa D4).
        /// <paramref name="keep"/> cuts cells outside hip trapezoids/triangles (Etapa D5).</summary>
        private RoofGrid BuildOneGrid(string gridName, Transform face, Vector2 size, int up, int across,
            System.Func<int, int, bool> keep = null)
        {
            var go = new GameObject(gridName);
            var g = go.AddComponent<RoofGrid>();
            if (contract != null) g.SetDeckDamageChance(contract.deckDamage01);
            g.Build(face, size, up, across, keep);
            g.Bind(player.input, player.toolbelt, player.materials);
            g.OnRoofComplete += HandleFaceComplete;
            allGrids.Add(g);
            return g;
        }

        // ----- Multi-face aggregates (Etapa D4) -----

        private int AllCellsTotal()
        {
            int t = 0;
            foreach (var g in allGrids) if (g != null) t += g.Total;
            return t;
        }

        private int AllCellsDone()
        {
            int d = 0;
            foreach (var g in allGrids) if (g != null) d += g.Completed;
            return d;
        }

        private float AllQuality()
        {
            int d = 0;
            float q = 0f;
            foreach (var g in allGrids)
            {
                if (g == null) continue;
                d += g.Completed;
                q += g.AverageQuality * g.Completed;
            }
            return d > 0 ? q / d : 1f;
        }

        /// <summary>A face finished — celebrate, and only call the job complete when ALL faces are.</summary>
        private void HandleFaceComplete()
        {
            int remaining = AllCellsTotal() - AllCellsDone();
            if (remaining > 0)
            {
                HudNotice.Show($"¡Agua terminada! Quedan {remaining} celdas en las otras caras", 4f);
                return;
            }
            HandleRoofComplete();
        }

        /// <summary>Puts fixtures on the roof: removable bolted antennas plus a fixed vent and dish.</summary>
        private void SpawnRoofFixtures(BuiltHouse house)
        {
            if (house.roofFace == null) return;
            Vector2 size = house.roofFaceSize;
            // Removable with the drill (4 corner bolts). Rough contracts ship a second one.
            RoofFixture.SpawnAntenna(house.roofFace, new Vector3(0.28f * size.x, 0.06f, -0.18f * size.y));
            if (contract != null && contract.antennas > 1)
                RoofFixture.SpawnAntenna(house.roofFace, new Vector3(-0.12f * size.x, 0.06f, -0.34f * size.y));
            // Fixed decoration that stays (shingle around it).
            RoofFixture.SpawnVent(house.roofFace, new Vector3(-0.30f * size.x, 0.06f, 0.22f * size.y));
            RoofFixture.SpawnDish(house.roofFace, new Vector3(0.06f * size.x, 0.06f, 0.34f * size.y));

            // Structural extras the contract advertises (Etapa D3). They occupy real
            // cells — the grid marks them obstructed and the courses flow around them.
            if (contract == null || roofGrid == null) return;
            if (contract.chimney)
            {
                int col = Mathf.Min(1, roofGrid.Cols - 1); // near the ridge, like a real stack
                // Centre-biased row: on hip trapezoids (D5) the ridge course only exists
                // near the middle; off-polygon picks land on null and simply skip.
                int row = Mathf.Clamp(roofGrid.Rows / 2 + UnityEngine.Random.Range(-2, 3),
                    1, roofGrid.Rows - 2);
                RoofObstacles.SpawnChimney(roofGrid, row, col);
            }
            if (contract.skylight)
            {
                int col = roofGrid.Cols / 2; // mid-slope, right where you'd love to stand
                int row = Mathf.Clamp(roofGrid.Rows / 2 + UnityEngine.Random.Range(-3, 3),
                    1, roofGrid.Rows - 3);
                RoofObstacles.SpawnSkylight(roofGrid, row, col, player.locomotion,
                    player.locomotion != null ? player.locomotion.GetComponent<PlayerStamina>() : null);
            }
            if (contract.tree)
            {
                // Planted beside the ladder approach (+X eave), canopy leaning over the climb.
                Vector3 at = house.root.TransformPoint(
                    new Vector3(contract.spec.width * 0.5f + 1.6f, 0f, 1.7f));
                Vector3 lean = house.root.TransformDirection(new Vector3(-0.35f, 0f, -0.9f));
                RoofObstacles.SpawnTree(at, lean);
            }
        }

        /// <summary>The equipment area near the spawn: separate material stations and a dumpster.</summary>
        private void BuildEquipmentArea(BuiltHouse house)
        {
            Vector3 s = house.playerSpawn;

            var shingles = CreateProp("Pallet_Shingles", s + new Vector3(2.6f, 0.25f, 2.4f),
                new Vector3(1.2f, 0.5f, 1.2f), new Color(0.22f, 0.42f, 0.58f));
            Texturize(shingles, RoofTextureLibrary.Surface.AsphaltShingle, 3f);
            AddShingleStack(shingles.transform);
            shingles.AddComponent<SupplyStation>().SetKind(MaterialKind.Shingles);

            var wood = CreateProp("Stack_Wood", s + new Vector3(0f, 0.2f, 3.0f),
                new Vector3(1.5f, 0.4f, 1.0f), new Color(0.74f, 0.58f, 0.36f));
            Texturize(wood, RoofTextureLibrary.Surface.WoodDeck, 2f);
            AddPlankStack(wood.transform);
            wood.AddComponent<SupplyStation>().SetKind(MaterialKind.Wood);

            var felt = CreateRoll("Roll_Felt", s + new Vector3(-2.6f, 0.5f, 2.4f),
                new Color(0.16f, 0.16f, 0.20f));
            Texturize(felt, RoofTextureLibrary.Surface.Felt, 2f);
            felt.AddComponent<SupplyStation>().SetKind(MaterialKind.Felt);

            dumpster = BuildDumpster(s + new Vector3(0f, 0f, -3.2f));

            // Debris wheelbarrow parked between the spawn and the dumpster (Etapa 5): catches
            // falling tear-off, gets pushed to the dumpster, and unloads six at a time.
            Wheelbarrow.Spawn(s + new Vector3(-1.6f, 0f, -1.6f), dumpster);

            // Staging tarp (Etapa 7): drops made on it snap to neat stacked slots.
            StagingArea.Build(s + new Vector3(-4.4f, 0f, 0.4f));

            // Anti-slip foam pads: carry one up (E) and set it where you work — no more sliding there.
            for (int i = 0; i < 3; i++)
                FoamPad.Spawn(s + new Vector3(1.6f, 0.03f + i * 0.11f, -2.1f));

            // Ground tool rack on the open side away from the house; look at a tool and press E
            // to swap it into your active hotbar slot (Etapa 2). Names only show on aim (the E prompt).
            ToolRack.Build(new Vector3(s.x + 3.0f, 0.02f, s.z), player.toolbelt);
        }

        private static GameObject CreateProp(string propName, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = propName;
            go.transform.position = pos;
            go.transform.localScale = scale;
            Paint(go, color);
            return go;
        }

        /// <summary>A felt roll lying on its side (a grabbable roll of underlayment).</summary>
        private static GameObject CreateRoll(string rollName, Vector3 pos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = rollName;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // lie down like a roll
            go.transform.localScale = new Vector3(0.5f, 0.7f, 0.5f);
            Paint(go, color);
            return go;
        }

        /// <summary>A nicer skip/dumpster: textured-metal body, a bright safety rim, ribs and wheels.</summary>
        private static Transform BuildDumpster(Vector3 pos)
        {
            var root = new GameObject("Dumpster");
            root.transform.position = new Vector3(pos.x, 0.10f, pos.z);
            root.AddComponent<DumpsterBin>();

            Color body = new Color(0.20f, 0.42f, 0.30f);  // industrial green
            Color rim = new Color(0.92f, 0.78f, 0.18f);    // yellow safety rim
            Color dark = new Color(0.16f, 0.18f, 0.18f);
            float w = 2.2f, h = 1.25f, d = 1.5f, t = 0.10f;

            // Body — textured metal; keeps colliders for physics and the E-interaction.
            AddMetalPanel(root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(w, t, d), body);            // floor
            AddMetalPanel(root.transform, new Vector3(0f, h * 0.5f,  d * 0.5f), new Vector3(w, h, t), body);  // front
            AddMetalPanel(root.transform, new Vector3(0f, h * 0.5f, -d * 0.5f), new Vector3(w, h, t), body);  // back
            AddMetalPanel(root.transform, new Vector3( w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), body);  // right
            AddMetalPanel(root.transform, new Vector3(-w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), body);  // left

            // Bright rim around the top opening.
            float ry = h + 0.03f;
            AddDecor(root.transform, new Vector3(0f, ry,  d * 0.5f), new Vector3(w + 0.12f, 0.10f, 0.14f), rim);
            AddDecor(root.transform, new Vector3(0f, ry, -d * 0.5f), new Vector3(w + 0.12f, 0.10f, 0.14f), rim);
            AddDecor(root.transform, new Vector3( w * 0.5f, ry, 0f), new Vector3(0.14f, 0.10f, d + 0.12f), rim);
            AddDecor(root.transform, new Vector3(-w * 0.5f, ry, 0f), new Vector3(0.14f, 0.10f, d + 0.12f), rim);

            // Vertical ribs on the front/back faces.
            for (int s = -1; s <= 1; s += 2)
                for (int k = -1; k <= 1; k++)
                    AddDecor(root.transform, new Vector3(k * w * 0.3f, h * 0.5f, s * (d * 0.5f + 0.03f)),
                        new Vector3(0.08f, h * 0.86f, 0.05f), dark);

            // Caster wheels at the four bottom corners.
            float cx = w * 0.5f - 0.18f, cz = d * 0.5f - 0.14f;
            Wheel(root.transform, new Vector3( cx, -0.08f,  cz));
            Wheel(root.transform, new Vector3(-cx, -0.08f,  cz));
            Wheel(root.transform, new Vector3( cx, -0.08f, -cz));
            Wheel(root.transform, new Vector3(-cx, -0.08f, -cz));

            return root.transform;
        }

        private static void AddMetalPanel(Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Panel";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                var m = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default")) { color = color };
                m.mainTexture = RoofTextureLibrary.Get(RoofTextureLibrary.Surface.Metal);
                m.mainTextureScale = new Vector2(Mathf.Max(1f, scale.x + scale.z), Mathf.Max(1f, scale.y + scale.z));
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.35f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.4f);
                r.sharedMaterial = m;
            }
        }

        private static void AddDecor(Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Decor";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            Paint(go, color);
        }

        private static void Wheel(Transform parent, Vector3 localPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Wheel";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(0.18f, 0.06f, 0.18f);
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            Paint(go, new Color(0.10f, 0.10f, 0.11f));
        }

        private static void Paint(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                { color = color };
            }
        }

        /// <summary>Give an existing prop a roofing detail texture so it reads as a real material.</summary>
        private static void Texturize(GameObject go, RoofTextureLibrary.Surface surf, float tile)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var m = r.sharedMaterial != null
                ? r.sharedMaterial
                : new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
            m.mainTexture = RoofTextureLibrary.Get(surf);
            m.mainTextureScale = new Vector2(tile, tile);
            r.sharedMaterial = m;
        }

        /// <summary>A loose stack of real shingles on top of the supply pallet (built on an unscaled holder).</summary>
        private static void AddShingleStack(Transform prop)
        {
            // Unparented holder so the stack isn't distorted by the prop's non-uniform scale.
            var holder = new GameObject("ShingleStack").transform;
            holder.position = prop.position + Vector3.up * 0.27f;
            // Real wrapped bundles (≈1.0 × 0.32 m) stacked on the pallet, as in the site photos.
            for (int i = 0; i < 8; i++)
                StackSlab(holder, new Vector3(Random.Range(-0.04f, 0.04f), i * 0.09f, Random.Range(-0.03f, 0.03f)),
                    new Vector3(1.0f, 0.085f, 0.34f), Quaternion.Euler(0f, Random.Range(-5f, 5f), 0f),
                    new Color(0.22f, 0.42f, 0.58f) * Random.Range(0.9f, 1.1f), RoofTextureLibrary.Surface.AsphaltShingle, 2f);
        }

        /// <summary>A few stacked planks on top of the wood supply.</summary>
        private static void AddPlankStack(Transform prop)
        {
            var holder = new GameObject("PlankStack").transform;
            holder.position = prop.position + Vector3.up * 0.22f;
            for (int i = 0; i < 4; i++)
                StackSlab(holder, new Vector3(Random.Range(-0.05f, 0.05f), i * 0.07f, Random.Range(-0.04f, 0.04f)),
                    new Vector3(1.3f, 0.06f, 0.85f), Quaternion.Euler(0f, Random.Range(-4f, 4f), 0f),
                    new Color(0.74f, 0.58f, 0.36f) * Random.Range(0.92f, 1.08f), RoofTextureLibrary.Surface.WoodDeck, 2f);
        }

        private static void StackSlab(Transform holder, Vector3 localPos, Vector3 scale, Quaternion rot,
            Color color, RoofTextureLibrary.Surface surf, float tile)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            slab.transform.SetParent(holder, false);
            slab.transform.localPosition = localPos;
            slab.transform.localRotation = rot;
            slab.transform.localScale = scale;
            var c = slab.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            var r = slab.GetComponent<Renderer>();
            if (r != null)
            {
                var m = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default")) { color = color };
                m.mainTexture = RoofTextureLibrary.Get(surf);
                m.mainTextureScale = new Vector2(tile, tile);
                r.sharedMaterial = m;
            }
        }

        private void DisableLegacyCameras()
        {
            // Turn off any pre-existing scene camera/listener so the new rig's camera wins.
            foreach (var cam in Object.FindObjectsOfType<Camera>())
            {
                cam.enabled = false;
            }
            foreach (var listener in Object.FindObjectsOfType<AudioListener>())
            {
                listener.enabled = false;
            }
        }

        // ----- Completion -----

        private void HandleRoofComplete()
        {
            roofComplete = true;
            Debug.Log("Roof complete! Press the finish key to hand the job in.");
        }

        private void FinishAndPay()
        {
            paid = true;
            payQuality = allGrids.Count > 0 ? AllQuality()
                : roofGrid != null ? roofGrid.AverageQuality : 1f;
            payCleanliness = player.materials != null ? player.materials.Cleanliness : 1f;
            int total = allGrids.Count > 0 ? AllCellsTotal()
                : roofGrid != null ? roofGrid.Total : 0;
            payAmount = Mathf.RoundToInt((50f + 8f * total)
                * Mathf.Lerp(0.5f, 1f, payQuality)
                * Mathf.Lerp(0.7f, 1f, payCleanliness)
                * (contract != null ? contract.payMultiplier : 1f)); // risk premium (Fase D)
            // Etapa E3: material taken beyond the contract's allowance comes out of the pay.
            materialPenalty = MaterialOverageCost();
            payAmount = Mathf.Max(0, payAmount - materialPenalty);
            Debug.Log($"Job handed in: ${payAmount} (quality {payQuality:P0}, cleanliness {payCleanliness:P0}, material overage -${materialPenalty})");

            RecordCompletionOnCareer();
        }

        /// <summary>
        /// Fase B reconnection: turn the payout into persistent career progress — money,
        /// completion history (unlocks the next job) and an immediate save. Standalone
        /// playtests (no career loaded) simply skip this.
        /// </summary>
        private void RecordCompletionOnCareer()
        {
            try
            {
                if (!CareerManager.Instance.HasActiveCareer) return;

                // Menu flow: the selected job. Standalone playtest: the career's current
                // job, so repeated editor runs still walk the catalog and unlock forward.
                int jobIndex = GameManager.Instance.SelectedJobIndex;
                if (jobIndex < 0) jobIndex = CareerManager.Instance.ActiveCareer.currentJobIndex;
                jobIndex = Mathf.Clamp(jobIndex, 0, CareerManager.Instance.TotalJobs - 1);
                var completion = new JobCompletion
                {
                    jobId = jobIndex,
                    completedDate = System.DateTime.UtcNow,
                    timeToComplete = System.TimeSpan.FromSeconds(Time.time - jobStartTime),
                    materialUsed = player.materials != null // units restocked at the pallets (E3)
                        ? player.materials.Restocked(MaterialKind.Shingles)
                          + player.materials.Restocked(MaterialKind.Felt)
                          + player.materials.Restocked(MaterialKind.Wood)
                        : 0f,
                    finalCoveragePercent = AllCellsTotal() > 0
                        ? 100f * AllCellsDone() / AllCellsTotal()
                        : roofGrid != null ? roofGrid.CompletionPercent : 100f,
                    attemptCount = attempt, // retries after fines count (Etapa D2)
                    qualityRating = RatingFor(payQuality),
                    payment = payAmount,
                    qualityScore = payQuality,
                    cleanliness = payCleanliness,
                    Job = CareerManager.Instance.GetJob(jobIndex),
                };

                CareerManager.Instance.RecordJobCompletion(completion); // adds money + saves
                GameManager.Instance.NotifySoloCompletion(completion);
                Debug.Log($"Career credited: +${payAmount} (total ${CareerManager.Instance.ActiveCareer.money}), job {jobIndex} saved.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Career recording skipped (standalone playtest?): {e.Message}");
            }
        }

        private static QualityRating RatingFor(float quality) =>
            quality >= 0.9f ? QualityRating.EXCELLENT
            : quality >= 0.75f ? QualityRating.GOOD
            : quality >= 0.55f ? QualityRating.ADEQUATE
            : QualityRating.POOR;

        /// <summary>
        /// Standalone play-test (scene opened directly, no menu flow): load — or create —
        /// a scratch "Playtest" career so money and progress persist between editor runs
        /// too. Careers made through the menus are untouched.
        /// </summary>
        private static void EnsureCareerForPlaytest()
        {
            try
            {
                if (CareerManager.Instance.HasActiveCareer) return;
                const string playtestName = "Playtest";
                if (CareerManager.Instance.LoadCareer(playtestName) == null)
                    CareerManager.Instance.CreateCareer(playtestName);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Playtest career unavailable (money won't persist): {e.Message}");
            }
        }

        /// <summary>
        /// Career money for the HUD, or int.MinValue when playing standalone (hides the
        /// chip). -1 is no longer a safe sentinel: debt makes negative money real (D2).
        /// </summary>
        private static int CareerMoney()
        {
            try
            {
                return CareerManager.Instance.HasActiveCareer
                    ? CareerManager.Instance.ActiveCareer.money : int.MinValue;
            }
            catch (System.Exception)
            {
                return int.MinValue;
            }
        }

        private static string FormatMoney(int money) => money < 0 ? $"-${-money}" : $"${money}";

        // ----- Fines + retry (Etapa D2) -----

        /// <summary>Breach fine: a quarter of the expected pay, never trivial.</summary>
        private int FineAmount() => contract == null ? 0
            : Mathf.Max(10, Mathf.RoundToInt(contract.expectedPay * 0.25f));

        /// <summary>Charge the fine to the career wallet. Debt (negative money) is allowed.</summary>
        private void PayFine()
        {
            int fine = FineAmount();
            try
            {
                if (!CareerManager.Instance.HasActiveCareer) return;
                CareerManager.Instance.RecordFine(fine);
                Debug.Log($"Contract fine paid: -${fine} (wallet ${CareerManager.Instance.ActiveCareer.money}).");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Fine skipped (standalone playtest?): {e.Message}");
            }
        }

        /// <summary>Pay the fine and rebuild the SAME contract from scratch (fresh weather plan).</summary>
        private void PayFineAndRetry()
        {
            int fine = FineAmount();
            PayFine();
            confirmingAbandon = false;
            attempt++;
            ContractOffer same = contract;
            TearDownWorld();
            rebuilding = true;
            StartCoroutine(RebuildNextFrame(same, fine));
        }

        /// <summary>
        /// Destroy() is deferred to end of frame, so wait one frame before rebuilding:
        /// the old WeatherSystem's OnDestroy must restore the sky first, or the new one
        /// would capture the mid-storm RenderSettings as its clean baseline.
        /// </summary>
        private System.Collections.IEnumerator RebuildNextFrame(ContractOffer same, int fine)
        {
            yield return null;
            rebuilding = false;
            StartContract(same);
            HudNotice.Show($"Multa pagada: -${fine} · Intento #{attempt} — misma casa, clima nuevo", 5f);
        }

        /// <summary>Pay the fine and walk away: back to the contract board with new offers.</summary>
        private void PayFineAndReturnToBoard()
        {
            int fine = FineAmount();
            PayFine();
            boardNotice = $"Contrato de {contract.client} abandonado — multa: -${fine}";
            confirmingAbandon = false;
            attempt = 1;
            contract = null;
            TearDownWorld();
            RollOffers();
        }

        /// <summary>
        /// Destroy everything BuildWorld created. All procedural objects are scene roots
        /// that didn't exist at Start, so anything not in the baseline snapshot goes.
        /// WeatherSystem.OnDestroy restores the sky/fog/sun on the way out.
        /// </summary>
        private void TearDownWorld()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root == gameObject || IsBaselineRoot(root)) continue;
                Destroy(root);
            }
            roofGrid = null;
            allGrids.Clear();
            materialPenalty = 0;
            player = default; // PlayerRig is a struct of references; the rig itself is destroyed
            dumpster = null;
            roofComplete = false;
            paid = false;
            boardCam = null; // recreated by RollOffers when needed
            // The player rig (and its cursor lock) is gone; free the mouse for the board.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private bool IsBaselineRoot(GameObject root)
        {
            if (baselineRoots == null) return false;
            for (int i = 0; i < baselineRoots.Length; i++)
            {
                if (baselineRoots[i] == root) return true;
            }
            return false;
        }

        private void ReturnToCareer()
        {
            if (jobEnded) return;
            jobEnded = true;
            TryGameManager(gm => gm.GoToCareerOverview());
        }

        private static void TryGameManager(System.Action<GameManager> action)
        {
            try
            {
                action(GameManager.Instance);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Scene transition skipped (standalone playtest?): {e.Message}");
            }
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box) { fontSize = 20, fontStyle = FontStyle.Bold };

            if (choosing && offers != null)
            {
                if (shopping) DrawShop(style);
                else DrawContractBoard(style);
            }

            // Career wallet (hidden in standalone playtests with no career loaded).
            // Debt (Etapa D2) shows in red so a bad streak of fines stings on sight.
            int money = CareerMoney();
            bool hasWallet = money != int.MinValue;
            if (hasWallet)
            {
                var chip = new GUIStyle(GUI.skin.box) { fontSize = 14, fontStyle = FontStyle.Bold };
                if (money < 0) chip.normal.textColor = new Color(1f, 0.45f, 0.4f);
                GUI.Box(new Rect(Screen.width - 176f, Screen.height - 42f, 160f, 28f),
                    money < 0 ? $"DEUDA: {FormatMoney(money)}" : $"Dinero: ${money}", chip);
            }

            // Abandon confirmation (Etapa D2): breach fine, retry or walk away.
            if (confirmingAbandon && contract != null)
            {
                var warn = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperCenter,
                    wordWrap = true,
                    padding = new RectOffset(16, 16, 14, 14)
                };
                GUI.Box(new Rect(Screen.width * 0.5f - 260f, Screen.height * 0.5f - 140f, 520f, 280f),
                    $"¿ABANDONAR EL CONTRATO?\n\nCliente: {contract.client}\nMulta por incumplimiento: -${FineAmount()}\n(se cobra aunque quedes en deuda)\n\n[R] pagar y REINTENTAR la misma casa\n[{abandonKey}] pagar y volver al tablero\n[{finishKey}] seguir trabajando", warn);
            }

            // Etapa E3: live warning once the material allowance is blown.
            if (!choosing && !paid && contract != null && !rebuilding)
            {
                int over = MaterialOverageCost();
                if (over > 0)
                {
                    var warnChip = new GUIStyle(GUI.skin.box) { fontSize = 13, fontStyle = FontStyle.Bold };
                    warnChip.normal.textColor = new Color(1f, 0.55f, 0.45f);
                    GUI.Box(new Rect(Screen.width - 216f, Screen.height - 74f, 200f, 26f),
                        $"Material de más: -${over}", warnChip);
                }
            }

            if (paid)
            {
                string total = hasWallet ? $"\nDinero total: {FormatMoney(money)}" : string.Empty;
                string overage = materialPenalty > 0 ? $"\nMaterial extra: -${materialPenalty}" : string.Empty;
                GUI.Box(new Rect(Screen.width * 0.5f - 240, Screen.height * 0.5f - 100, 480, 200),
                    $"TRABAJO ENTREGADO\n\nCalidad: {payQuality:P0}\nLimpieza: {payCleanliness:P0}{overage}\nPago: ${payAmount}{total}\n\n[{abandonKey}] volver", style);
            }
            else if (roofComplete && !confirmingAbandon)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 230, 24, 460, 40),
                    $"TECHO COMPLETO — pulsa {finishKey} para entregar", style);
            }
        }

        // ----- Contract board UI (Fase D) -----

        private void DrawContractBoard(GUIStyle titleStyle)
        {
            GUI.Box(new Rect(Screen.width * 0.5f - 340f, 56f, 680f, 42f),
                "TABLERO DE CONTRATOS — elige tu próximo trabajo", titleStyle);

            var panel = new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(14, 14, 12, 12),
                wordWrap = true
            };

            const float w = 370f, h = 330f, gap = 24f;
            float x0 = Screen.width * 0.5f - (w * 1.5f + gap);
            float y = 120f;
            for (int i = 0; i < offers.Length && i < 3; i++)
            {
                GUI.Box(new Rect(x0 + i * (w + gap), y, w, h), OfferText(i, offers[i]), panel);
            }

            var help = new GUIStyle(GUI.skin.box) { fontSize = 15, fontStyle = FontStyle.Bold };
            GUI.Box(new Rect(Screen.width * 0.5f - 320f, y + h + 18f, 640f, 30f),
                $"[1] / [2] / [3] tomar contrato · [R] otras ofertas · [T] tienda · [{abandonKey}] volver", help);

            // Etapa D2: what abandoning the last contract just cost.
            if (!string.IsNullOrEmpty(boardNotice))
            {
                var notice = new GUIStyle(GUI.skin.box) { fontSize = 14, fontStyle = FontStyle.Bold };
                notice.normal.textColor = new Color(1f, 0.55f, 0.45f);
                GUI.Box(new Rect(Screen.width * 0.5f - 320f, y + h + 56f, 640f, 28f), boardNotice, notice);
            }
        }

        /// <summary>The shop screen (Fase E): permanent gear bought with career money.</summary>
        private void DrawShop(GUIStyle titleStyle)
        {
            GUI.Box(new Rect(Screen.width * 0.5f - 340f, 56f, 680f, 42f),
                "TIENDA DEL TECHADOR — equipo permanente", titleStyle);

            var panel = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(16, 16, 10, 10),
                wordWrap = true
            };

            const float w = 700f, h = 52f, gap = 6f;
            float x = Screen.width * 0.5f - w * 0.5f;
            float y = 112f;
            for (int i = 0; i < Shop.Items.Length; i++)
            {
                ShopItem it = Shop.Items[i];
                string head = HasGear(it.id)
                    ? $"[{i + 1}] {it.title} — EN TU EQUIPO"
                    : $"[{i + 1}] {it.title} — ${it.price}";
                GUI.Box(new Rect(x, y + i * (h + gap), w, h), $"{head}\n{it.desc}", panel);
            }

            // Consumables (Etapa E2): bought in packs, spent in-level with [Q].
            float cy = y + Shop.Items.Length * (h + gap) + 6f;
            var section = new GUIStyle(GUI.skin.box) { fontSize = 13, fontStyle = FontStyle.Bold };
            GUI.Box(new Rect(x, cy, w, 24f), "— CONSUMIBLES (se gastan en la obra: [Q] usar · [X] cambiar) —", section);
            cy += 30f;
            for (int i = 0; i < Shop.Packs.Length; i++)
            {
                ShopItem it = Shop.Packs[i];
                int have = ChargeCount(it.id);
                string head = $"[{i + 6}] {it.title} ×{it.charges} — ${it.price}"
                    + (have > 0 ? $"  (en tu bolsa: ×{have})" : string.Empty);
                GUI.Box(new Rect(x, cy + i * (h + gap), w, h), $"{head}\n{it.desc}", panel);
            }

            var help = new GUIStyle(GUI.skin.box) { fontSize = 15, fontStyle = FontStyle.Bold };
            float hy = cy + Shop.Packs.Length * (h + gap) + 8f;
            GUI.Box(new Rect(Screen.width * 0.5f - 340f, hy, 680f, 30f),
                $"[1-{Shop.Items.Length}] equipo · [6-{5 + Shop.Packs.Length}] consumibles · [T] volver · la tienda NO fía", help);

            if (!string.IsNullOrEmpty(shopNotice))
            {
                var notice = new GUIStyle(GUI.skin.box) { fontSize = 14, fontStyle = FontStyle.Bold };
                notice.normal.textColor = new Color(1f, 0.85f, 0.4f);
                GUI.Box(new Rect(Screen.width * 0.5f - 320f, hy + 38f, 640f, 28f), shopNotice, notice);
            }
        }

        private static string OfferText(int index, ContractOffer o)
        {
            string pitch = o.spec.roofPitchDegrees < 33f ? "suave"
                : o.spec.roofPitchDegrees < 39f ? "empinada" : "MUY empinada";
            string damage = o.deckDamage01 < 0.3f ? "leve"
                : o.deckDamage01 < 0.45f ? "medio" : "grave";
            string extras = (o.chimney ? "chimenea · " : "")
                + (o.skylight ? "claraboya · " : "")
                + (o.tree ? "árbol molesto · " : "");
            extras = extras.Length > 0 ? extras.Substring(0, extras.Length - 3) : "ninguno";
            return $"[{index + 1}]  {o.client}\n"
                + $"Dificultad: {o.stars}/5\n\n"
                + $"Casa {o.spec.width:0.#}×{o.spec.depth:0.#} m · {o.spec.stories} piso{(o.spec.stories > 1 ? "s" : "")}\n"
                + $"Pendiente {o.spec.roofPitchDegrees:0}° ({pitch})\n"
                + $"Techo: {o.TotalCells} celdas ({(o.hipRoof ? o.lWing ? "4 AGUAS + ALA EN L" : "4 AGUAS" : o.lWing ? "2 aguas + ALA EN L" : o.bothFaces ? "2 aguas" : "1 agua")})\n"
                + $"Daño de madera: {damage}\n"
                + $"Antenas a quitar: {o.antennas}\n"
                + $"Extras: {extras}\n"
                + $"Pronóstico: {o.forecast}\n\n"
                + $"Pago estimado: ${o.expectedPay}";
        }
    }
}
