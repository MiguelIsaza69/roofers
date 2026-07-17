using System.Collections.Generic;
using UnityEngine;
using RoofingSimulator.Gameplay;
using RoofingSimulator.Player;

namespace RoofingSimulator.World
{
    /// <summary>
    /// Dynamic weather (Fase C). The level plays out in staged phases (clear → windy →
    /// rain → storm...) that blend into each other: the sun dims and cools, light fog
    /// rolls in, rain falls (a cheap particle field that follows the player and slants
    /// with the wind), wind pushes the player around (harder the higher up you are) and
    /// rain soaks the roof so it slides sooner (<see cref="PlayerLocomotion.SetSlipperiness"/>
    /// — foam pads still grip). Shows the current weather top-right and warns before a
    /// change so players can reach safety. Snow, dense fog, lightning and temperature
    /// arrive in later etapas (C2–C4).
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        public enum Kind { Clear, Overcast, Windy, Rain, Storm, Snow, Blizzard, Fog }

        private struct Phase { public Kind kind; public float duration; }

        public static WeatherSystem Instance { get; private set; }

        // ----- Tuning -----
        private const float TransitionSeconds = 10f;  // how long a change takes to blend in
        private const float WarnSeconds = 12f;        // heads-up before the next phase
        private const float MaxWindSpeed = 6.0f;      // m/s push at full storm, fully exposed
        private const float SoakSeconds = 18f;        // rain time until the roof is fully wet
        private const float DrySeconds = 75f;         // time to dry once the rain stops
        private const float CoverSeconds = 50f;       // heavy snowfall time until cells are buried
        private const float MeltSeconds = 120f;       // snow melt once the snowfall stops
        private const float StrikeChargeSeconds = 3.5f; // exposed time at the ridge before the bolt lands

        private readonly List<Phase> plan = new List<Phase>();
        private int phaseIndex;
        private float phaseTimer;
        private bool warned;

        private float rain;     // eased 0..1
        private float wind01;   // eased 0..1
        private float snow01;   // eased 0..1
        private float fog01;    // eased 0..1 — dense fog as its own phase (Etapa C3)
        private float sun01;    // eased 0..1 — how much direct sun the phase lets through
        private float rainTarget, windTarget, snowTarget, fogTarget, sunTarget;
        private float wetness;  // 0 dry .. 1 soaked (dries slowly after the rain stops)
        private bool coldDay;   // cold levels snow instead of raining (Etapa C2)
        private float severity = 0.35f; // contract difficulty (Fase D): skews phases nastier

        // Temperature (Etapa C4): cold days chill you (regen slows — keep moving or get low);
        // heat-wave days cook you in direct sun (efforts cost more — rest in the shade).
        private bool hotDay;
        private float chill;    // 0..1 cold stress
        private float heat01;   // 0..1 heat stress

        // Lightning (Etapa C3): sky flashes during storms, and standing tall at the ridge
        // charges a strike you can shed by crouching or climbing down.
        private float flash;        // 0..1 sky/screen flash, decays fast
        private float flashTimer;   // next distant flash
        private float strikeCharge; // 0..1 buildup while you're the highest point
        private float ridgeY = float.NaN;
        private PlayerStamina stamina;

        private Light sun;
        private float sunBaseIntensity;
        private Color sunBaseColor;
        private float ambientBase;
        private bool fogWasOn;

        private ParticleSystem rainPs;
        private ParticleSystem snowPs;
        private PlayerLocomotion loco;
        private Transform playerT;
        private RoofGrid grid; // primary face (lightning ridge height)
        private readonly List<RoofGrid> grids = new List<RoofGrid>(); // every face (Etapa D4)
        private float roofSnow; // cell-weighted snow across all faces (HUD)
        private float windAngle;      // degrees; drifts slowly so gusts change direction
        private float gustCooldown;

        public Kind Current => plan.Count > 0 ? plan[phaseIndex].kind : Kind.Clear;
        public float Rain => rain;
        public float Wind01 => wind01;
        public float Snow => snow01;
        public float Wetness => wetness;

        public static WeatherSystem Spawn(PlayerLocomotion locomotion, Transform player, RoofGrid roofGrid,
            float severity01 = 0.35f, int climate = -1)
        {
            var go = new GameObject("WeatherSystem");
            var ws = go.AddComponent<WeatherSystem>();
            ws.loco = locomotion;
            ws.playerT = player;
            ws.grid = roofGrid;
            ws.RegisterGrid(roofGrid);
            ws.stamina = locomotion != null ? locomotion.GetComponent<PlayerStamina>() : null;
            ws.Init(severity01, climate);
            return ws;
        }

        /// <summary>
        /// Extra roof faces (the far slope, the L-wing) so snow falls and melts on every
        /// grid of a multi-face job (Etapa D4). The primary grid keeps driving the
        /// lightning ridge height.
        /// </summary>
        public void RegisterGrid(RoofGrid roofGrid)
        {
            if (roofGrid != null && !grids.Contains(roofGrid)) grids.Add(roofGrid);
        }

        // Fase E gear: thermal clothing / canteen slow how fast cold and heat build up.
        private float coldProtection;
        private float heatProtection;
        public void SetProtection(float cold01, float heat01Protection)
        {
            coldProtection = Mathf.Clamp(cold01, 0f, 0.8f);
            heatProtection = Mathf.Clamp(heat01Protection, 0f, 0.8f);
        }

        private void Awake()
        {
            Instance = this;
            FindOrCreateSun();
            ambientBase = RenderSettings.ambientIntensity;
            fogWasOn = RenderSettings.fog;
            BuildRainField();
            BuildSnowField();
            windAngle = Random.Range(0f, 360f);
        }

        /// <summary>
        /// Difficulty knobs from the contract (Fase D): <paramref name="severity01"/> skews
        /// the phase plan toward the violent end and shortens the opening calm; climate
        /// forces a cold day (1), a heat wave (2), a mild day (0), or rolls the legacy
        /// random split (-1). Must run before the plan is built — Spawn does it.
        /// </summary>
        private void Init(float severity01, int climate)
        {
            severity = Mathf.Clamp01(severity01);
            coldDay = climate < 0 ? Random.value < 0.45f : climate == 1;
            hotDay = climate < 0 ? !coldDay && Random.value < 0.35f : climate == 2;
            BuildPlan();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            RenderSettings.fog = fogWasOn;
            RenderSettings.ambientIntensity = ambientBase;
            if (sun != null) { sun.intensity = sunBaseIntensity; sun.color = sunBaseColor; }
            stamina?.SetClimate(1f, 1f);
        }

        // ----- Phase plan -----

        private void BuildPlan()
        {
            plan.Clear();
            // Always start calm so the player gets their footing — but rough contracts
            // give you less of a grace period before the dice start rolling.
            plan.Add(new Phase
            {
                kind = Random.value < 0.6f ? Kind.Clear : Kind.Overcast,
                duration = Random.Range(50f, 75f) * Mathf.Lerp(1f, 0.6f, severity)
            });
            for (int i = 0; i < 3; i++) AppendRandomPhase();
            ApplyPhase(0, announce: false);
        }

        private void AppendRandomPhase()
        {
            Kind last = plan.Count > 0 ? plan[plan.Count - 1].kind : Kind.Clear;
            Kind pick = last;
            for (int guard = 0; guard < 12 && pick == last; guard++)
            {
                // Kinds are ordered benign → violent; the severity skew (Fase D) pushes the
                // roll toward the top end, so rough contracts really do storm more.
                float r = Mathf.Pow(Random.value, 1f / (1f + severity * 1.4f));
                pick = r < 0.18f ? Kind.Clear
                    : r < 0.40f ? Kind.Overcast
                    : r < 0.56f ? Kind.Windy
                    : r < 0.70f ? Kind.Fog
                    : r < 0.90f ? Kind.Rain
                    : Kind.Storm;
                // Cold levels snow instead of raining; storms come as blizzards (Etapa C2).
                if (coldDay)
                    pick = pick == Kind.Rain ? Kind.Snow
                        : pick == Kind.Storm ? Kind.Blizzard
                        : pick;
            }
            bool violent = pick == Kind.Storm || pick == Kind.Blizzard;
            float dur = violent ? Random.Range(35f, 55f)
                : pick == Kind.Fog ? Random.Range(40f, 65f)
                : Random.Range(50f, 90f);
            plan.Add(new Phase { kind = pick, duration = dur });
        }

        private void ApplyPhase(int index, bool announce)
        {
            phaseIndex = index;
            phaseTimer = plan[index].duration;
            warned = false;
            rainTarget = 0f; windTarget = 0f; snowTarget = 0f; fogTarget = 0f; sunTarget = 0f;
            switch (plan[index].kind)
            {
                case Kind.Clear: windTarget = 0.08f; sunTarget = 1f; break;
                case Kind.Overcast: windTarget = 0.18f; sunTarget = 0.45f; break;
                case Kind.Windy: windTarget = 0.75f; sunTarget = 0.85f; break;
                case Kind.Rain: rainTarget = 0.65f; windTarget = 0.30f; sunTarget = 0.15f; break;
                case Kind.Storm: rainTarget = 1f; windTarget = 1f; sunTarget = 0.05f; break;
                case Kind.Snow: snowTarget = 0.7f; windTarget = 0.25f; sunTarget = 0.2f; break;
                case Kind.Blizzard: snowTarget = 1f; windTarget = 1f; sunTarget = 0.05f; break;
                case Kind.Fog: fogTarget = 1f; windTarget = 0.10f; sunTarget = 0.35f; break; // still, thick air
            }
            if (announce) HudNotice.Show($"Clima: {Label(plan[index].kind)}");
        }

        // ----- Per-frame drive -----

        private void Update()
        {
            // Safety net for a component added without Spawn(): default mild plan.
            if (plan.Count == 0) BuildPlan();

            float dt = Time.deltaTime;

            // Advance the schedule; keep appending phases so the weather never runs out.
            phaseTimer -= dt;
            if (phaseTimer <= 0f)
            {
                if (phaseIndex + 2 >= plan.Count) AppendRandomPhase();
                ApplyPhase(phaseIndex + 1, announce: true);
            }

            // One-time warning before a real change so players can get to safety.
            if (!warned && phaseTimer <= WarnSeconds && phaseIndex + 1 < plan.Count
                && plan[phaseIndex + 1].kind != Current)
            {
                warned = true;
                HudNotice.Show($"Se acerca: {Label(plan[phaseIndex + 1].kind)} — asegúrate (arnés/espumas)");
            }

            // Ease intensities toward the phase targets (weather rolls in, not pops in).
            rain = Mathf.MoveTowards(rain, rainTarget, dt / TransitionSeconds);
            wind01 = Mathf.MoveTowards(wind01, windTarget, dt / TransitionSeconds);
            snow01 = Mathf.MoveTowards(snow01, snowTarget, dt / TransitionSeconds);
            fog01 = Mathf.MoveTowards(fog01, fogTarget, dt / TransitionSeconds);
            sun01 = Mathf.MoveTowards(sun01, sunTarget, dt / TransitionSeconds);

            // Snow lands on the roof cells; once the snowfall stops, it melts away (Etapa C2).
            // Multi-face jobs (Etapa D4) tick every registered grid; the average weighs by cells.
            float avgSnow = 0f;
            int snowCells = 0;
            for (int i = 0; i < grids.Count; i++)
            {
                RoofGrid g = grids[i];
                if (g == null) continue;
                if (snow01 > 0.1f) g.TickSnow(dt * snow01 / CoverSeconds, 0f);
                else if (g.AverageSnow > 0.004f) g.TickSnow(0f, dt / MeltSeconds);
                avgSnow += g.AverageSnow * g.Total;
                snowCells += g.Total;
            }
            avgSnow = snowCells > 0 ? avgSnow / snowCells : 0f;

            // Rain soaks the roof; melting snow is wet too; otherwise it dries slowly.
            if (rain > 0.2f) wetness = Mathf.Min(1f, wetness + dt * rain / SoakSeconds);
            else if (snow01 <= 0.1f && avgSnow > 0.03f && wetness < 0.65f)
                wetness = Mathf.Min(0.65f, wetness + dt / 45f); // meltwater
            else wetness = Mathf.Max(0f, wetness - dt / DrySeconds);

            UpdateSky();
            UpdateWind(dt);
            UpdateRain();
            UpdateSnow();
            UpdateLightning(dt);
            UpdateTemperature(dt);

            // Standing snow is slippery on its own; wet shingles too — worst of both applies.
            loco?.SetSlipperiness(Mathf.Max(wetness, avgSnow * 0.85f));
            roofSnow = avgSnow; // combined value for the HUD label
        }

        /// <summary>
        /// Temperature (Etapa C4). Cold days: exposure (snowfall, wind chill, being up high)
        /// builds CHILL that slows stamina regen — working/moving keeps you warm, and ground
        /// level is sheltered. Heat waves: direct sun builds HEAT that makes every effort cost
        /// more — clouds, rain or standing in a shadow cool you back down. Mild days do nothing.
        /// </summary>
        private void UpdateTemperature(float dt)
        {
            if (coldDay)
            {
                heat01 = 0f;
                // Up on the roof the wind bites; at ground level the house shelters you.
                float exposure = playerT != null
                    ? Mathf.Lerp(0.45f, 1f, Mathf.InverseLerp(1.5f, 5f, playerT.position.y))
                    : 1f;
                // Thermal clothing (Fase E) cuts the exposure, not the warming.
                float ambient = (0.45f + 0.4f * snow01 + 0.35f * wind01) * exposure
                    * (1f - coldProtection);
                // Working keeps you warm; sprinting positively cooks.
                float speed = loco != null ? loco.CurrentSpeed : 0f;
                float warming = speed > 4.6f ? 1.15f : speed > 1.1f ? 0.55f : 0f;
                chill = Mathf.Clamp01(chill + (ambient - warming) * dt / 40f);
            }
            else if (hotDay)
            {
                chill = 0f;
                bool relief = IsInShade() || sun01 < 0.25f; // shadow, clouds or rain
                float exertion = loco != null && loco.CurrentSpeed > 4.6f ? 0.35f : 0f;
                // The canteen (Fase E) slows how fast the sun cooks you; relief still cools fully.
                float delta = relief ? -1.5f : (sun01 + exertion) * (1f - heatProtection);
                heat01 = Mathf.Clamp01(heat01 + delta * dt / 40f);
            }
            else
            {
                chill = Mathf.Max(0f, chill - dt / 20f);
                heat01 = Mathf.Max(0f, heat01 - dt / 20f);
            }

            // Cold quarters the regen at full chill; heat nearly doubles every cost.
            stamina?.SetClimate(1f - 0.75f * chill, 1f + 0.9f * heat01);
        }

        /// <summary>Is something between the player's head and the sun? (House, roof, dumpster...)</summary>
        private bool IsInShade()
        {
            if (playerT == null || sun == null) return false;
            Vector3 from = playerT.position + Vector3.up * 1.9f;
            return Physics.Raycast(from, -sun.transform.forward, 80f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Readable thermometer for the HUD (flavour — the stress meters do the work).</summary>
        private int TemperatureC()
        {
            if (coldDay) return Mathf.RoundToInt(Mathf.Lerp(-1f, -9f, Mathf.Max(snow01, wind01 * 0.6f)));
            if (hotDay) return Mathf.RoundToInt(Mathf.Lerp(27f, 36f, sun01));
            return Mathf.RoundToInt(Mathf.Lerp(16f, 22f, sun01));
        }

        /// <summary>
        /// Electrical storms (Etapa C3): distant sky flashes, and if you stay the tallest
        /// point (up at the ridge, standing) a strike charges up on YOU — crouch or climb
        /// down to shed it. A landed bolt stuns, shoves and burns max stamina (never kills).
        /// </summary>
        private void UpdateLightning(float dt)
        {
            flash = Mathf.MoveTowards(flash, 0f, dt * 3f);

            // Only true electrical storms carry lightning (not blizzards).
            bool stormy = Current == Kind.Storm && rain > 0.6f;
            if (!stormy)
            {
                strikeCharge = Mathf.Max(0f, strikeCharge - dt * 1.4f);
                return;
            }

            // The ridge is the roof's highest course (col 0); read its height once. Hip
            // roofs (Etapa D5) cut the corner cells, so scan the course for a live one.
            if (float.IsNaN(ridgeY) && grid != null)
            {
                for (int r = 0; r < grid.Rows && float.IsNaN(ridgeY); r++)
                {
                    RoofCell top = grid.CellAt(r, 0);
                    if (top != null) ridgeY = top.transform.position.y;
                }
            }

            // Distant flashes for mood.
            flashTimer -= dt;
            if (flashTimer <= 0f)
            {
                flashTimer = Random.Range(6f, 14f);
                flash = Mathf.Max(flash, 0.55f);
            }

            // Standing tall near the ridge = you're the lightning rod.
            bool exposed = !float.IsNaN(ridgeY) && playerT != null && loco != null
                && playerT.position.y > ridgeY - 1.1f
                && !loco.IsCrouching;
            strikeCharge = Mathf.Clamp01(strikeCharge
                + (exposed ? dt / StrikeChargeSeconds : -dt * 1.4f));
            if (strikeCharge >= 1f)
            {
                strikeCharge = 0f;
                StrikePlayer();
            }
        }

        private void StrikePlayer()
        {
            flash = 1f;
            if (playerT != null) BuildBoltVisual(playerT.position + Vector3.up * 0.9f);
            stamina?.AddInjury(20f); // burns max stamina — heals slowly, never kills
            if (loco != null)
            {
                Vector3 shove = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                shove = (shove.sqrMagnitude < 0.01f ? Vector3.forward : shove.normalized);
                loco.AddImpulse((shove + Vector3.up * 0.5f) * 4.5f); // the stun: knocked off your feet
            }
            HudNotice.Show("¡RAYO! Te alcanzó por ser el punto más alto — baja o agáchate", 4f);
        }

        /// <summary>A quick jagged bolt from the sky to the strike point, gone in a blink.</summary>
        private static void BuildBoltVisual(Vector3 at)
        {
            var go = new GameObject("LightningBolt");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 8;
            Vector3 top = at + Vector3.up * 30f;
            for (int i = 0; i < lr.positionCount; i++)
            {
                float t = i / (lr.positionCount - 1f);
                Vector3 p = Vector3.Lerp(top, at, t);
                if (i > 0 && i < lr.positionCount - 1)
                    p += new Vector3(Random.Range(-0.9f, 0.9f), 0f, Random.Range(-0.9f, 0.9f));
                lr.SetPosition(i, p);
            }
            lr.startWidth = 0.30f;
            lr.endWidth = 0.08f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = new Color(0.92f, 0.95f, 1f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            Destroy(go, 0.35f);
        }

        private void UpdateSky()
        {
            float gloom = Mathf.Max(Mathf.Max(rain, fog01 * 0.5f),
                Mathf.Max(snow01 * 0.75f, wind01 * 0.35f));
            if (sun != null)
            {
                // Lightning flashes punch the light up for a blink.
                sun.intensity = sunBaseIntensity * Mathf.Lerp(1f, 0.42f, gloom)
                    * (1f + flash * 2.2f);
                // Rain cools the light toward gray-blue; snow washes it toward cold white.
                Color dim = Color.Lerp(new Color(0.66f, 0.70f, 0.78f),
                    new Color(0.80f, 0.84f, 0.92f), Mathf.Max(snow01, fog01 * 0.7f));
                sun.color = Color.Lerp(sunBaseColor, dim, gloom);
            }
            RenderSettings.ambientIntensity = ambientBase
                * Mathf.Lerp(1f, 0.55f, gloom) * (1f + flash * 1.2f);
            RenderSettings.fog = gloom > 0.03f || fogWasOn;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = Color.Lerp(new Color(0.62f, 0.66f, 0.72f),
                new Color(0.84f, 0.86f, 0.90f), Mathf.Max(snow01, fog01));
            // Blizzard whiteout and the dense-fog phase both really cut visibility.
            RenderSettings.fogDensity = gloom * 0.012f
                + snow01 * wind01 * 0.045f
                + fog01 * 0.075f;
        }

        private void UpdateWind(float dt)
        {
            // Direction drifts slowly; strength breathes with Perlin gusts.
            windAngle += (Mathf.PerlinNoise(Time.time * 0.05f, 3.7f) - 0.5f) * 30f * dt;
            Vector3 dir = Quaternion.Euler(0f, windAngle, 0f) * Vector3.forward;
            float gust = Mathf.PerlinNoise(Time.time * 0.35f, 17.3f); // 0..1 breathing
            float strength = wind01 * MaxWindSpeed * (0.45f + 0.55f * gust);

            // Up on the roof you're exposed; at ground level the house shelters you.
            float exposure = playerT != null
                ? Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(1.5f, 5f, playerT.position.y))
                : 1f;
            loco?.SetWind(dir * strength * exposure);

            // Windy/storm phases land an extra shove now and then (the harness earns its keep).
            gustCooldown -= dt;
            if (gustCooldown <= 0f)
            {
                gustCooldown = Random.Range(4.5f, 9f);
                if (wind01 > 0.55f && loco != null)
                    loco.AddImpulse(dir * Random.Range(1.4f, 2.6f) * exposure);
            }
        }

        private void UpdateRain()
        {
            if (rainPs == null) return;

            // The field follows the player from above so it never has to cover the map.
            if (playerT != null)
                rainPs.transform.position = playerT.position + Vector3.up * 13f;

            var emission = rainPs.emission;
            emission.rateOverTime = rain * 750f;

            // Wind slants the streaks (they fall in world space).
            Vector3 w = Quaternion.Euler(0f, windAngle, 0f) * Vector3.forward
                        * (wind01 * MaxWindSpeed);
            var vel = rainPs.velocityOverLifetime;
            vel.x = new ParticleSystem.MinMaxCurve(w.x);
            vel.z = new ParticleSystem.MinMaxCurve(w.z);
        }

        private void UpdateSnow()
        {
            if (snowPs == null) return;

            if (playerT != null)
                snowPs.transform.position = playerT.position + Vector3.up * 11f;

            var emission = snowPs.emission;
            emission.rateOverTime = snow01 * 350f;

            // Flakes get carried sideways by the wind (less than rain — they flutter).
            Vector3 w = Quaternion.Euler(0f, windAngle, 0f) * Vector3.forward
                        * (wind01 * MaxWindSpeed * 0.7f);
            var vel = snowPs.velocityOverLifetime;
            vel.x = new ParticleSystem.MinMaxCurve(w.x);
            vel.z = new ParticleSystem.MinMaxCurve(w.z);
        }

        // ----- Construction -----

        private void FindOrCreateSun()
        {
            foreach (var l in Object.FindObjectsOfType<Light>())
                if (l.type == LightType.Directional && l.enabled) { sun = l; break; }
            if (sun == null)
            {
                var go = new GameObject("Sun");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.05f;
                sun.shadows = LightShadows.Soft;
                go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            sunBaseIntensity = sun.intensity;
            sunBaseColor = sun.color;
        }

        private void BuildRainField()
        {
            var go = new GameObject("RainFX");
            go.transform.SetParent(transform, false);
            rainPs = go.AddComponent<ParticleSystem>();

            var main = rainPs.main;
            main.loop = true;
            main.startLifetime = 1.15f;
            main.startSpeed = 0f; // velocityOverLifetime drives the fall
            main.startSize = 0.035f;
            main.startColor = new Color(0.72f, 0.80f, 0.94f, 0.42f);
            main.maxParticles = 2000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = rainPs.emission;
            emission.rateOverTime = 0f;

            var shape = rainPs.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26f, 0.5f, 26f);

            var vel = rainPs.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(-13f);

            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Stretch;
            pr.lengthScale = 1f;
            pr.velocityScale = 0.05f; // long thin streaks along the fall direction
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.material = new Material(
                Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                ?? Shader.Find("Sprites/Default"));
        }

        private void BuildSnowField()
        {
            var go = new GameObject("SnowFX");
            go.transform.SetParent(transform, false);
            snowPs = go.AddComponent<ParticleSystem>();

            var main = snowPs.main;
            main.loop = true;
            main.startLifetime = 7f;
            main.startSpeed = 0f;
            main.startSize = 0.055f;
            main.startColor = new Color(0.97f, 0.97f, 1f, 0.9f);
            main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = snowPs.emission;
            emission.rateOverTime = 0f;

            var shape = snowPs.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26f, 0.5f, 26f);

            var vel = snowPs.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(-2.1f); // flakes drift down, they don't pour

            // A little turbulence so the flakes flutter instead of falling in rails.
            var noise = snowPs.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.6f;

            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.material = new Material(
                Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                ?? Shader.Find("Sprites/Default"));
        }

        // ----- HUD -----

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box) { fontSize = 13, fontStyle = FontStyle.Bold };

            string line = $"Clima: {Label(Current)} · {TemperatureC()}°C";
            if (roofSnow > 0.35f) line += " · techo NEVADO (pala)";
            else if (wetness > 0.25f) line += " · techo MOJADO (resbala)";
            GUI.Box(new Rect(Screen.width - 346f, 14f, 330f, 26f), line, style);

            // Persistent countdown banner while a real change is incoming.
            float nextY = 44f;
            if (phaseTimer <= WarnSeconds && phaseIndex + 1 < plan.Count
                && plan[phaseIndex + 1].kind != Current)
            {
                var warn = new GUIStyle(style) { normal = { textColor = new Color(1f, 0.85f, 0.35f) } };
                GUI.Box(new Rect(Screen.width - 346f, nextY, 330f, 26f),
                    $"Se acerca: {Label(plan[phaseIndex + 1].kind)} ({Mathf.CeilToInt(phaseTimer)}s)", warn);
                nextY += 30f;
            }

            // Temperature stress warnings (Etapa C4).
            if (chill > 0.35f)
            {
                var cold = new GUIStyle(style) { normal = { textColor = new Color(0.55f, 0.8f, 1f) } };
                GUI.Box(new Rect(Screen.width - 346f, nextY, 330f, 26f),
                    "FRÍO: regeneras lento — muévete o baja", cold);
            }
            else if (heat01 > 0.35f)
            {
                var hot = new GUIStyle(style) { normal = { textColor = new Color(1f, 0.65f, 0.3f) } };
                GUI.Box(new Rect(Screen.width - 346f, nextY, 330f, 26f),
                    "CALOR: todo cansa más — descansa a la SOMBRA", hot);
            }

            // Subtle whole-screen tint as the body stress builds (blue = cold, warm = heat).
            if (chill > 0.05f || heat01 > 0.05f)
            {
                Color prevTint = GUI.color;
                GUI.color = chill > heat01
                    ? new Color(0.5f, 0.7f, 1f, 0.10f * chill)
                    : new Color(1f, 0.45f, 0.2f, 0.08f * heat01);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prevTint;
            }

            // Lightning-rod warning: you're charging a strike — a bar fills under the alert.
            if (strikeCharge > 0.03f)
            {
                float cx = Screen.width * 0.5f;
                var danger = new GUIStyle(style)
                { fontSize = 15, normal = { textColor = new Color(1f, 0.5f, 0.3f) } };
                GUI.Box(new Rect(cx - 250f, Screen.height * 0.30f, 500f, 30f),
                    "PELIGRO DE RAYO: ¡eres el punto más alto! BAJA o AGÁCHATE (Ctrl)", danger);
                Color prevBar = GUI.color;
                GUI.color = Color.Lerp(new Color(1f, 0.9f, 0.3f), new Color(1f, 0.25f, 0.15f), strikeCharge);
                GUI.DrawTexture(new Rect(cx - 250f, Screen.height * 0.30f + 32f,
                    500f * strikeCharge, 6f), Texture2D.whiteTexture);
                GUI.color = prevBar;
            }

            // Whole-screen lightning flash (strong when the bolt lands on you).
            if (flash > 0.01f)
            {
                Color prevFlash = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.45f * flash);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prevFlash;
            }
        }

        public static string Label(Kind k) => k switch
        {
            Kind.Clear => "Despejado",
            Kind.Overcast => "Nublado",
            Kind.Windy => "Ventoso",
            Kind.Rain => "Lluvia",
            Kind.Storm => "Tormenta",
            Kind.Snow => "Nevada",
            Kind.Blizzard => "Ventisca",
            Kind.Fog => "Neblina",
            _ => k.ToString()
        };
    }
}
