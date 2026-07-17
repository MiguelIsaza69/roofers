using UnityEngine;
using RoofingSimulator.World;

namespace RoofingSimulator.Core
{
    /// <summary>A job offer on the contract board (Fase D): the house, the trouble, the pay.</summary>
    public class ContractOffer
    {
        public string client;
        public HouseSpec spec;
        public int tilesUpSlope;
        public int tilesAcross;
        public float deckDamage01;    // fraction of 2×3 deck blocks that are rotten
        public int antennas;          // removable fixtures to unscrew before roofing
        public bool chimney;          // a brick chimney occupies a cell — roof around it (D3)
        public bool skylight;         // two glass cells mid-slope — don't stand on them (D3)
        public bool tree;             // yard tree crowding the ladder (D3)
        public float weatherSeverity; // 0..1 — skews the weather plan toward the nasty end
        public int climate;           // 0 mild, 1 cold (snow), 2 heat wave
        public float payMultiplier;   // risk premium over the base cell rate
        public int expectedPay;       // what the board advertises (at perfect quality)
        public int stars;             // 1..5 difficulty display
        public string forecast;       // human label for the board

        // Etapa D4 — roof layout: single slope, both slopes, or both + an L-wing.
        public bool bothFaces;        // the far slope is part of the job (cross the ridge!)
        public bool lWing;            // lower perpendicular wing with two faces of its own
        public int wingTilesUpSlope;
        public int wingTilesAcross;

        // Etapa D5 — hip roof: 4 sloped faces (2 trapezoids + 2 end triangles).
        public bool hipRoof;
        public int endTilesAcross;    // across the triangular end faces (their up-slope = tilesUpSlope)

        // Playable cells across every face. Assigned by the generator: hip cuts remove
        // corner cells, so this is counted with the SAME rule the builder uses.
        public int TotalCells;
    }

    /// <summary>
    /// Rolls the contract board (Fase D): near-random offers whose odds of "worse
    /// everything" — size, pitch, stories, deck damage, weather — creep up with career
    /// progress, REPO-style. Offer 1 is the tamest of the roll, offer 3 the spiciest
    /// (and the best paid). Everything nasty pays a premium.
    /// </summary>
    public static class LevelGenerator
    {
        private static readonly string[] Clients =
        {
            "Sra. Ramírez", "Don Pablo", "Familia Ortiz", "Sr. Castro",
            "Doña Lucía", "Los Herrera", "Escuela Vieja 12", "Granja El Roble"
        };

        public static ContractOffer[] Roll(int progress, int count = 3)
        {
            var offers = new ContractOffer[count];
            for (int i = 0; i < count; i++)
                offers[i] = RollOne(progress + i); // a gentle ladder: last offer is the dare
            return offers;
        }

        private static ContractOffer RollOne(int level)
        {
            // 0..1 difficulty tier with noise — "casi aleatorio", ramping over ~8 jobs.
            float tier = Mathf.Clamp01((level + Random.Range(-0.5f, 0.8f)) / 8f);

            HouseSpec spec = HouseSpec.Default;
            spec.width = Mathf.Round(Mathf.Lerp(7f, 12f, tier * Random.Range(0.7f, 1.2f)) * 2f) / 2f;
            spec.depth = Mathf.Round(spec.width * Random.Range(1.1f, 1.35f) * 2f) / 2f;
            spec.roofPitchDegrees = Mathf.Lerp(30.26f, 45f, tier * Random.Range(0.6f, 1.1f));
            spec.stories = level >= 5 && Random.value < 0.35f ? 3
                : Random.value < Mathf.Lerp(0.35f, 0.75f, tier) ? 2 : 1;
            spec.wallHeight = HouseSpec.StoryHeight * spec.stories;

            // Hip roof (Etapa D5): needs depth > width so the shortened ridge exists.
            bool hipR = spec.depth - spec.width >= 1.0f
                && Random.value < Mathf.Lerp(0.10f, 0.45f, tier);
            spec.hipRoof = hipR;

            // L-wing (Etapa D4): needs 2+ stories so its roof fits under the main eave.
            bool wing = spec.stories >= 2 && Random.value < Mathf.Lerp(0f, 0.55f, tier);
            if (wing)
            {
                spec.lWing = true;
                spec.wingWidth = Mathf.Round(Mathf.Clamp(
                    spec.width * Random.Range(0.45f, 0.6f), 3.5f, Mathf.Min(6f, spec.depth * 0.55f)) * 2f) / 2f;
                spec.wingDepth = Mathf.Round(Mathf.Clamp(
                    spec.depth * Random.Range(0.35f, 0.5f), 3f, 6f) * 2f) / 2f;
            }

            var o = new ContractOffer
            {
                client = Clients[Random.Range(0, Clients.Length)],
                spec = spec,
                deckDamage01 = Mathf.Clamp(
                    Mathf.Lerp(0.18f, 0.55f, tier) + Random.Range(-0.05f, 0.08f), 0.1f, 0.65f),
                antennas = 1 + (Random.value < tier * 0.9f ? 1 : 0),
                // Structural extras (Etapa D3) get likelier as the career deepens.
                chimney = Random.value < Mathf.Lerp(0.25f, 0.85f, tier),
                skylight = Random.value < Mathf.Lerp(0.15f, 0.60f, tier),
                tree = Random.value < Mathf.Lerp(0.25f, 0.55f, tier),
                // Roof layout (Etapa D4/D5): wing and hip jobs are always full-roof jobs.
                lWing = wing,
                hipRoof = hipR,
                bothFaces = wing || hipR || Random.value < Mathf.Lerp(0.25f, 0.95f, tier),
                weatherSeverity = Mathf.Clamp01(
                    Mathf.Lerp(0.12f, 0.85f, tier) + Random.Range(-0.08f, 0.1f)),
            };

            // Climate: cold and heat-wave days get more common the deeper you go.
            float r = Random.value;
            o.climate = r < Mathf.Lerp(0.20f, 0.40f, tier) ? 1
                : r < Mathf.Lerp(0.32f, 0.65f, tier) ? 2
                : 0;

            // Work size: cells keep the real ~1 m shingle width (Etapa 0), so a bigger
            // house genuinely means more courses to lay — and more pay.
            o.tilesAcross = Mathf.Max(8, Mathf.RoundToInt(spec.depth));
            float slopeLen = (spec.width * 0.5f)
                / Mathf.Cos(spec.roofPitchDegrees * Mathf.Deg2Rad);
            o.tilesUpSlope = Mathf.Clamp(Mathf.RoundToInt(slopeLen / 0.77f), 5, 12);

            // Wing tiles mirror HouseBuilder's clamped-rise geometry so cells stay ~1 m real.
            if (wing)
            {
                float wHalf = spec.wingWidth * 0.5f;
                float wRise = Mathf.Min(wHalf * Mathf.Tan(spec.roofPitchDegrees * Mathf.Deg2Rad),
                    spec.wallHeight - HouseSpec.StoryHeight - 0.2f);
                float wSlope = Mathf.Sqrt(wHalf * wHalf + wRise * wRise);
                o.wingTilesUpSlope = Mathf.Clamp(Mathf.RoundToInt(wSlope / 0.77f), 3, 8);
                o.wingTilesAcross = Mathf.Max(3, Mathf.RoundToInt(spec.wingDepth));
            }

            // Playable cells across every face. Hip cuts (D5) are counted with the same
            // shared rule the builder applies, so the board's number is exact.
            if (hipR)
            {
                o.endTilesAcross = Mathf.Max(3, Mathf.RoundToInt(spec.width));
                float halfW = spec.width * 0.5f;
                o.TotalCells =
                    2 * HipRoofMath.CellCount(o.tilesAcross, o.tilesUpSlope,
                        new Vector2(slopeLen, spec.depth), spec.roofPitchDegrees, halfW, false)
                    + 2 * HipRoofMath.CellCount(o.endTilesAcross, o.tilesUpSlope,
                        new Vector2(slopeLen, spec.width), spec.roofPitchDegrees, halfW, true);
            }
            else
            {
                o.TotalCells = o.tilesUpSlope * o.tilesAcross * (o.bothFaces ? 2 : 1);
            }
            if (wing) o.TotalCells += 2 * o.wingTilesUpSlope * o.wingTilesAcross;

            // Risk premium: size, weather, height, climate and obstacles sweeten the cheque.
            o.payMultiplier = 1f
                + 0.5f * tier
                + 0.2f * o.weatherSeverity
                + 0.12f * (spec.stories - 1)
                + (o.climate != 0 ? 0.12f : 0f)
                + (o.chimney ? 0.06f : 0f)
                + (o.skylight ? 0.06f : 0f)
                + (o.tree ? 0.04f : 0f)
                + (o.bothFaces ? 0.05f : 0f)  // moving gear over the ridge
                + (o.lWing ? 0.08f : 0f)      // two roofs' worth of logistics
                + (o.hipRoof ? 0.08f : 0f);   // four faces, cut shingles at every hip
            o.expectedPay = Mathf.RoundToInt((50f + 8f * o.TotalCells) * o.payMultiplier);

            float danger = tier * 0.5f + o.weatherSeverity * 0.25f
                + (spec.stories - 1) * 0.15f + o.deckDamage01 * 0.2f
                + (o.skylight ? 0.05f : 0f)
                + (o.bothFaces ? 0.06f : 0f) + (o.lWing ? 0.08f : 0f)
                + (o.hipRoof ? 0.06f : 0f);
            o.stars = Mathf.Clamp(1 + Mathf.RoundToInt(danger * 5f), 1, 5);

            o.forecast = ForecastLabel(o);
            return o;
        }

        private static string ForecastLabel(ContractOffer o)
        {
            string label = o.climate == 1 ? "Frío — nevadas"
                : o.climate == 2 ? "Ola de calor"
                : "Templado";
            if (o.weatherSeverity > 0.6f) label += o.climate == 1 ? " y ventiscas" : ", tormentoso";
            else if (o.weatherSeverity > 0.35f) label += ", inestable";
            return label;
        }
    }
}
