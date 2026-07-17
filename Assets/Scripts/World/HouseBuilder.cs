using UnityEngine;

namespace RoofingSimulator.World
{
    /// <summary>Parameters for a generated house (a procedural level will randomize these).</summary>
    public struct HouseSpec
    {
        public float width;            // X span of the building (gable span)
        public float depth;            // Z span (ridge runs along Z)
        public float wallHeight;       // total eave height (StoryHeight × stories)
        public float roofPitchDegrees; // 30.26 = a 7/12 pitch (real, moderate)
        public int stories;            // 1 or 2 floors (windows per floor; taller = scarier fall)

        // L-wing (Etapa D4): a lower 1-story volume attached to the -X wall, ridge
        // perpendicular to the main one. Its roof stays UNDER the main eave, so the two
        // roofs never intersect — no valleys, just more faces to shingle.
        public bool lWing;
        public float wingWidth;        // wing gable span (runs along world Z)
        public float wingDepth;        // how far the wing extends from the main wall (world -X)

        // Hip roof (Etapa D5): the gable triangles become two sloped triangular faces, the
        // main faces become trapezoids and the ridge shortens to depth - width. Requires
        // depth > width + ~1 m so the ridge actually exists (equal pitch on all faces).
        public bool hipRoof;

        public const float StoryHeight = 2.6f; // real per-floor height (m)

        // Real-scale house (1 unit = 1 m). 10 tiles across 10 m depth = exactly 1.0 m each
        // (a real shingle's width); 2 floors by default (eave at 5.2 m) for a scarier fall;
        // a believable 7/12 roof.
        public static HouseSpec Default => new HouseSpec
        {
            width = 8f,
            depth = 10f,
            wallHeight = StoryHeight * 2f,
            roofPitchDegrees = 30.26f,
            stories = 2
        };
    }

    /// <summary>What <see cref="HouseBuilder"/> hands back so the scene can place the player and roof grid.</summary>
    public struct BuiltHouse
    {
        public Transform root;
        public Vector3 playerSpawn;
        public Transform roofFace;     // unit-scaled anchor on the shingled face (+Y = roof normal)
        public Vector2 roofFaceSize;   // (slope length, depth) in the face's local X / Z
        public Transform ridgeAnchor;
        public Ladder ladder;

        // Etapa D4 — extra playable faces. Same convention as roofFace: anchor local +X
        // runs ridge→eave, +Y is the face normal. Null when the house doesn't have them.
        public Transform roofFaceLeft; // the far slope of the main gable
        public Transform wingFaceA;    // L-wing faces (when spec.lWing)
        public Transform wingFaceB;
        public Vector2 wingFaceSize;   // (slope length, wing length)

        // Etapa D5 — hip roof: the two sloped end triangles (tiling rectangle: the grid
        // covers the bounding box and the corner cells outside the triangle never build).
        public Transform endFaceFront; // +Z end
        public Transform endFaceBack;  // -Z end
        public Vector2 endFaceSize;    // (slope length, width)
        public float ridgeLength;      // depth for gables, depth - width for hips
    }

    /// <summary>
    /// Shared hip-roof cell rule (Etapa D5): the generator's advertised cell count and the
    /// built grids must cut the SAME cells, so both call this. Main faces are trapezoids
    /// (both hips cut in); end faces are triangles (apex at the ridge end). A cell exists
    /// when its CENTER is inside the polygon; the ragged edge hides under the hip caps.
    /// </summary>
    public static class HipRoofMath
    {
        public static bool Inside(int row, int col, int rows, int cols, Vector2 faceSize,
            float pitchDegrees, float mainHalfWidth, bool endFace)
        {
            float u = (col + 0.5f) * (faceSize.x / cols);            // up-slope from the ridge
            float z = -faceSize.y * 0.5f + (row + 0.5f) * (faceSize.y / rows);
            float run = u * Mathf.Cos(pitchDegrees * Mathf.Deg2Rad); // horizontal run from the ridge
            float limit = endFace ? run : faceSize.y * 0.5f - mainHalfWidth + run;
            return Mathf.Abs(z) <= limit + 0.001f;
        }

        public static int CellCount(int rows, int cols, Vector2 faceSize,
            float pitchDegrees, float mainHalfWidth, bool endFace)
        {
            int n = 0;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (Inside(r, c, rows, cols, faceSize, pitchDegrees, mainHalfWidth, endFace)) n++;
            return n;
        }
    }

    /// <summary>
    /// Builds a simple gable-roofed house out of primitives: ground, a wall block, two
    /// sloped roof faces (steep enough to slide on), gable end caps, a ridge anchor for
    /// the harness, and a ladder leaning on one eave. Everything is sized from a
    /// <see cref="HouseSpec"/> so procedural levels can vary it later.
    /// </summary>
    public static class HouseBuilder
    {
        public static BuiltHouse Build(HouseSpec spec, Vector3 origin)
        {
            var root = new GameObject("House").transform;
            root.position = origin;

            float halfW = spec.width * 0.5f;
            float pitchRad = spec.roofPitchDegrees * Mathf.Deg2Rad;
            float rise = halfW * Mathf.Tan(pitchRad);
            float ridgeY = spec.wallHeight + rise;
            float slopeLen = halfW / Mathf.Cos(pitchRad);

            Color groundColor = new Color(0.30f, 0.42f, 0.24f);
            Color wallColor = new Color(0.78f, 0.74f, 0.66f);
            Color roofColor = new Color(0.36f, 0.36f, 0.40f);

            // A random wall style for this level (clay-textured panels share these textures).
            Material wallBase = WallStyleLibrary.GetRandom(out int _);

            // Ground (large, so the player can walk around and fall onto it).
            CreateBox(root, "Ground", new Vector3(0f, -0.05f, 0f), Quaternion.identity,
                new Vector3(60f, 0.1f, 60f), groundColor, null);

            // Solid wall block (collision + backing); textured panels go on its faces.
            CreateBox(root, "Walls", new Vector3(0f, spec.wallHeight * 0.5f, 0f), Quaternion.identity,
                new Vector3(spec.width, spec.wallHeight, spec.depth), wallColor, null);

            // Exterior wall panels, each tiled to its own face so the texture isn't stretched.
            float pt = 0.06f, hh = spec.wallHeight, hy = spec.wallHeight * 0.5f;
            WallPanel(root, "Wall_F", new Vector3(0f, hy, spec.depth * 0.5f + pt * 0.5f),
                new Vector3(spec.width, hh, pt), wallBase, spec.width, hh);
            WallPanel(root, "Wall_B", new Vector3(0f, hy, -spec.depth * 0.5f - pt * 0.5f),
                new Vector3(spec.width, hh, pt), wallBase, spec.width, hh);
            WallPanel(root, "Wall_R", new Vector3(spec.width * 0.5f + pt * 0.5f, hy, 0f),
                new Vector3(pt, hh, spec.depth), wallBase, spec.depth, hh);
            WallPanel(root, "Wall_L", new Vector3(-spec.width * 0.5f - pt * 0.5f, hy, 0f),
                new Vector3(pt, hh, spec.depth), wallBase, spec.depth, hh);

            // Two roof faces. Right face tilts about -Z, left about +Z.
            Quaternion rightRot = Quaternion.Euler(0f, 0f, -spec.roofPitchDegrees);
            Quaternion leftRot = Quaternion.Euler(0f, 0f, spec.roofPitchDegrees);
            Vector3 rightCenter = new Vector3(halfW * 0.5f, spec.wallHeight + rise * 0.5f, 0f);
            Vector3 leftCenter = new Vector3(-halfW * 0.5f, spec.wallHeight + rise * 0.5f, 0f);
            Vector3 faceScale = new Vector3(slopeLen + 0.5f, 0.12f, spec.depth + 0.5f);

            // Hip roof (Etapa D5): trapezoid/triangle faces are flat MESHES — a box face
            // would jut through its neighbours near the ridge ends.
            bool hip = spec.hipRoof && spec.depth > spec.width + 0.9f;
            float ridgeHalf = hip ? (spec.depth - spec.width) * 0.5f : spec.depth * 0.5f;
            float faceLift = hip ? 0.02f : faceScale.y * 0.5f + 0.02f;
            Transform endFront = null, endBack = null;
            Vector2 endFaceSize = default;

            if (hip)
            {
                float d2 = spec.depth * 0.5f, wh = spec.wallHeight;
                float sinP = Mathf.Sin(pitchRad), cosP = Mathf.Cos(pitchRad);

                // Main trapezoids (full eave, shortened ridge).
                RoofMesh(root, "RoofFace_R", new[]
                {
                    new Vector3(halfW, wh, -d2), new Vector3(halfW, wh, d2),
                    new Vector3(0f, ridgeY, ridgeHalf), new Vector3(0f, ridgeY, -ridgeHalf)
                }, new Vector3(sinP, cosP, 0f), roofColor);
                RoofMesh(root, "RoofFace_L", new[]
                {
                    new Vector3(-halfW, wh, d2), new Vector3(-halfW, wh, -d2),
                    new Vector3(0f, ridgeY, -ridgeHalf), new Vector3(0f, ridgeY, ridgeHalf)
                }, new Vector3(-sinP, cosP, 0f), roofColor);

                // Sloped end triangles (they replace the gable walls).
                float endRun = d2 - ridgeHalf; // = halfW when the pitch matches all faces
                float endPitchDeg = Mathf.Atan2(rise, endRun) * Mathf.Rad2Deg;
                float endSlope = Mathf.Sqrt(rise * rise + endRun * endRun);
                float sinE = rise / endSlope, cosE = endRun / endSlope;
                RoofMesh(root, "RoofFace_F", new[]
                {
                    new Vector3(-halfW, wh, d2), new Vector3(halfW, wh, d2),
                    new Vector3(0f, ridgeY, ridgeHalf)
                }, new Vector3(0f, cosE, sinE), roofColor);
                RoofMesh(root, "RoofFace_B", new[]
                {
                    new Vector3(halfW, wh, -d2), new Vector3(-halfW, wh, -d2),
                    new Vector3(0f, ridgeY, -ridgeHalf)
                }, new Vector3(0f, cosE, -sinE), roofColor);

                BuildHipCaps(root, halfW, wh, ridgeY, d2, ridgeHalf, roofColor);

                // End-face anchors — same convention: local +X runs ridge→eave.
                Vector3 endCenterF = new Vector3(0f, (ridgeY + wh) * 0.5f, (ridgeHalf + d2) * 0.5f);
                Vector3 endCenterB = new Vector3(0f, (ridgeY + wh) * 0.5f, -(ridgeHalf + d2) * 0.5f);

                endFront = new GameObject("RoofGridAnchorF").transform;
                endFront.SetParent(root, false);
                endFront.localRotation = Quaternion.Euler(endPitchDeg, 0f, 0f) * Quaternion.Euler(0f, -90f, 0f);
                endFront.localPosition = endCenterF + (Quaternion.Euler(endPitchDeg, 0f, 0f) * Vector3.up) * faceLift;

                endBack = new GameObject("RoofGridAnchorB").transform;
                endBack.SetParent(root, false);
                endBack.localRotation = Quaternion.Euler(-endPitchDeg, 0f, 0f) * Quaternion.Euler(0f, 90f, 0f);
                endBack.localPosition = endCenterB + (Quaternion.Euler(-endPitchDeg, 0f, 0f) * Vector3.up) * faceLift;
                endFaceSize = new Vector2(endSlope, spec.width);
            }
            else
            {
                CreateBox(root, "RoofFace_R", rightCenter, rightRot, faceScale, roofColor, "RoofSurface");
                CreateBox(root, "RoofFace_L", leftCenter, leftRot, faceScale, roofColor, "RoofSurface");

                // Gable end caps close the triangles at each Z end (same wall texture).
                CreateGable(root, "Gable_F", halfW, spec.wallHeight, ridgeY, spec.depth * 0.5f, wallBase);
                CreateGable(root, "Gable_B", halfW, spec.wallHeight, ridgeY, -spec.depth * 0.5f, wallBase);
            }

            BuildWindows(root, spec);
            BuildLights(root, spec);

            // Unit-scaled anchor sitting on the right face's surface, used to tile shingle cells.
            var faceAnchor = new GameObject("RoofGridAnchor").transform;
            faceAnchor.SetParent(root, false);
            faceAnchor.localRotation = rightRot;
            faceAnchor.localPosition = rightCenter + (rightRot * Vector3.up) * faceLift;

            // Far-slope anchor (Etapa D4). The 180° yaw flips its local +X so it still runs
            // ridge→eave (the grid's convention: col 0 at the ridge, last col at the eave).
            var faceAnchorL = new GameObject("RoofGridAnchorL").transform;
            faceAnchorL.SetParent(root, false);
            faceAnchorL.localRotation = leftRot * Quaternion.Euler(0f, 180f, 0f);
            faceAnchorL.localPosition = leftCenter + (leftRot * Vector3.up) * faceLift;

            // Ridge anchor (harness clip point) with a small post and collider. Hips
            // shorten it to the real (cut) ridge so it doesn't float over the end faces.
            Transform ridgePost = CreateBox(root, "RidgeAnchor", new Vector3(0f, ridgeY + 0.15f, 0f),
                Quaternion.identity, new Vector3(0.12f, 0.3f, hip ? Mathf.Max(0.6f, ridgeHalf * 2f) : spec.depth),
                new Color(0.5f, 0.3f, 0.2f), null);
            ridgePost.gameObject.AddComponent<RoofingSimulator.Player.HarnessAnchor>();

            // Ladder on the +X eave.
            var ladderGo = new GameObject("Ladder");
            ladderGo.transform.SetParent(root, false);
            var ladder = ladderGo.AddComponent<Ladder>();
            Vector3 climbX = new Vector3(halfW + 0.45f, 0f, 0f);
            Vector3 bottom = root.TransformPoint(climbX);
            Vector3 top = root.TransformPoint(climbX + Vector3.up * spec.wallHeight);
            // Step-off point a little up the right face.
            Vector2 eave = new Vector2(halfW, spec.wallHeight);
            Vector2 toRidge = new Vector2(-halfW, rise).normalized;
            Vector2 exitXY = eave + toRidge * 1.6f;
            Vector3 topExit = root.TransformPoint(new Vector3(exitXY.x, exitXY.y + 0.4f, 0f));
            ladder.Setup(bottom, top, root.TransformDirection(Vector3.left), topExit);

            Vector3 spawn = root.TransformPoint(new Vector3(halfW + 4.5f, 0.2f, 0f));

            // L-wing (Etapa D4): a lower perpendicular volume off the -X wall.
            Transform wingFaceA = null, wingFaceB = null;
            Vector2 wingFaceSize = default;
            if (spec.lWing && spec.wingWidth > 2f && spec.wingDepth > 2f)
                BuildWing(root, spec, wallBase, roofColor, wallColor,
                    out wingFaceA, out wingFaceB, out wingFaceSize);

            return new BuiltHouse
            {
                root = root,
                playerSpawn = spawn,
                roofFace = faceAnchor,
                roofFaceSize = new Vector2(slopeLen, spec.depth),
                ridgeAnchor = ridgePost,
                ladder = ladder,
                roofFaceLeft = faceAnchorL,
                wingFaceA = wingFaceA,
                wingFaceB = wingFaceB,
                wingFaceSize = wingFaceSize,
                endFaceFront = endFront,
                endFaceBack = endBack,
                endFaceSize = endFaceSize,
                ridgeLength = hip ? ridgeHalf * 2f : spec.depth
            };
        }

        /// <summary>
        /// A flat roof-face mesh (trapezoid or triangle) with a MeshCollider. The winding
        /// auto-flips so the face always renders toward <paramref name="outwardNormal"/>.
        /// </summary>
        private static void RoofMesh(Transform parent, string faceName, Vector3[] verts,
            Vector3 outwardNormal, Color color)
        {
            var go = new GameObject(faceName);
            go.transform.SetParent(parent, false);
            go.tag = "RoofSurface";

            var mesh = new Mesh { name = faceName };
            mesh.vertices = verts;
            int[] tris = verts.Length == 4 ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 1, 2 };
            Vector3 n = Vector3.Cross(verts[1] - verts[0], verts[2] - verts[0]);
            if (Vector3.Dot(n, outwardNormal) < 0f)
            {
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int t = tris[i + 1];
                    tris[i + 1] = tris[i + 2];
                    tris[i + 2] = t;
                }
            }
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().mesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
            { color = color };
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>
        /// Cap strips along the four hip edges plus the short ridge cap: a wide flat
        /// underlay hides the sawtooth edge of the cut cells, and the raised cap on top
        /// reads like real hip-cap shingles.
        /// </summary>
        private static void BuildHipCaps(Transform root, float halfW, float wallH, float ridgeY,
            float halfD, float ridgeHalf, Color roofColor)
        {
            Color capColor = new Color(roofColor.r * 0.8f, roofColor.g * 0.8f, roofColor.b * 0.8f);
            Color underColor = new Color(roofColor.r * 0.55f, roofColor.g * 0.55f, roofColor.b * 0.55f);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 eave = new Vector3(sx * halfW, wallH, sz * halfD);
                    Vector3 top = new Vector3(0f, ridgeY, sz * ridgeHalf);
                    Vector3 mid = (eave + top) * 0.5f;
                    Vector3 dir = top - eave;
                    float len = dir.magnitude + 0.25f;
                    Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    Strip(root, "HipUnderlay", mid, rot, new Vector3(0.95f, 0.05f, len), underColor);
                    Strip(root, "HipCap", mid + Vector3.up * 0.05f, rot, new Vector3(0.5f, 0.11f, len), capColor);
                }
            }
            Strip(root, "RidgeCap", new Vector3(0f, ridgeY + 0.05f, 0f), Quaternion.identity,
                new Vector3(0.5f, 0.12f, Mathf.Max(0.5f, ridgeHalf * 2f + 0.3f)), capColor);
        }

        private static void Strip(Transform parent, string stripName, Vector3 pos, Quaternion rot,
            Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = stripName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c); // decorative — nothing to snag on
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                { color = color };
            }
        }

        /// <summary>
        /// The L-wing (Etapa D4): a 1-story gable volume attached to the -X wall, ridge
        /// perpendicular to the main one. Built in a child frame yawed -90° so the exact
        /// same right/left face math as the main house applies. Its ridge is clamped
        /// under the main eave, so the roofs never intersect (no valleys). Comes with its
        /// own small ladder on the far half of its +Z-side eave.
        /// </summary>
        private static void BuildWing(Transform root, HouseSpec spec, Material wallBase,
            Color roofColor, Color wallColor, out Transform faceA, out Transform faceB, out Vector2 faceSize)
        {
            float halfW = spec.width * 0.5f;
            float pitchRad = spec.roofPitchDegrees * Mathf.Deg2Rad;

            // Wing frame: local +X = world +Z (its gable span), local +Z = world -X (extension).
            var wing = new GameObject("Wing").transform;
            wing.SetParent(root, false);
            wing.localRotation = Quaternion.Euler(0f, -90f, 0f);
            wing.localPosition = new Vector3(-halfW, 0f, spec.depth * 0.20f);

            float wHalf = spec.wingWidth * 0.5f;
            float wWall = HouseSpec.StoryHeight;
            // Keep the wing ridge safely below the main eave — that's what avoids valleys.
            float wRise = Mathf.Min(wHalf * Mathf.Tan(pitchRad), spec.wallHeight - wWall - 0.2f);
            float wSlope = Mathf.Sqrt(wHalf * wHalf + wRise * wRise);
            float wPitch = Mathf.Atan2(wRise, wHalf) * Mathf.Rad2Deg;
            float wLen = spec.wingDepth;
            float pt = 0.06f;

            // Volume runs local z 0 (flush against the main wall) .. wLen (far gable end).
            CreateBox(wing, "WingWalls", new Vector3(0f, wWall * 0.5f, wLen * 0.5f), Quaternion.identity,
                new Vector3(spec.wingWidth, wWall, wLen), wallColor, null);
            WallPanel(wing, "WingWall_R", new Vector3(wHalf + pt * 0.5f, wWall * 0.5f, wLen * 0.5f),
                new Vector3(pt, wWall, wLen), wallBase, wLen, wWall);
            WallPanel(wing, "WingWall_L", new Vector3(-wHalf - pt * 0.5f, wWall * 0.5f, wLen * 0.5f),
                new Vector3(pt, wWall, wLen), wallBase, wLen, wWall);
            WallPanel(wing, "WingWall_End", new Vector3(0f, wWall * 0.5f, wLen + pt * 0.5f),
                new Vector3(spec.wingWidth, wWall, pt), wallBase, spec.wingWidth, wWall);

            // Roof faces — the main-house math, verbatim, in wing space.
            Quaternion wRightRot = Quaternion.Euler(0f, 0f, -wPitch);
            Quaternion wLeftRot = Quaternion.Euler(0f, 0f, wPitch);
            Vector3 wRightC = new Vector3(wHalf * 0.5f, wWall + wRise * 0.5f, wLen * 0.5f);
            Vector3 wLeftC = new Vector3(-wHalf * 0.5f, wWall + wRise * 0.5f, wLen * 0.5f);
            Vector3 wScale = new Vector3(wSlope + 0.3f, 0.12f, wLen + 0.3f);
            CreateBox(wing, "WingRoof_R", wRightC, wRightRot, wScale, roofColor, "RoofSurface");
            CreateBox(wing, "WingRoof_L", wLeftC, wLeftRot, wScale, roofColor, "RoofSurface");
            CreateGable(wing, "WingGable", wHalf, wWall, wWall + wRise, wLen, wallBase);

            faceA = new GameObject("WingAnchorA").transform;
            faceA.SetParent(wing, false);
            faceA.localRotation = wRightRot;
            faceA.localPosition = wRightC + (wRightRot * Vector3.up) * (wScale.y * 0.5f + 0.02f);

            faceB = new GameObject("WingAnchorB").transform;
            faceB.SetParent(wing, false);
            faceB.localRotation = wLeftRot * Quaternion.Euler(0f, 180f, 0f); // +X still ridge→eave
            faceB.localPosition = wLeftC + (wLeftRot * Vector3.up) * (wScale.y * 0.5f + 0.02f);

            faceSize = new Vector2(wSlope, wLen);

            // Small ladder onto the wing's +X-local eave, out toward the far end.
            var wingLadderGo = new GameObject("WingLadder");
            wingLadderGo.transform.SetParent(wing, false);
            var wl = wingLadderGo.AddComponent<Ladder>();
            Vector3 wb = wing.TransformPoint(new Vector3(wHalf + 0.45f, 0f, wLen * 0.6f));
            Vector3 wt = wing.TransformPoint(new Vector3(wHalf + 0.45f, wWall, wLen * 0.6f));
            Vector2 wEave = new Vector2(wHalf, wWall);
            Vector2 wToRidge = new Vector2(-wHalf, wRise).normalized;
            Vector2 wExit = wEave + wToRidge * 1.4f;
            Vector3 wTopExit = wing.TransformPoint(new Vector3(wExit.x, wExit.y + 0.4f, wLen * 0.6f));
            wl.Setup(wb, wt, wing.TransformDirection(Vector3.left), wTopExit);
        }

        private static Transform CreateBox(Transform parent, string boxName, Vector3 localPos,
            Quaternion localRot, Vector3 localScale, Color color, string tag)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = boxName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            if (!string.IsNullOrEmpty(tag)) go.tag = tag;

            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                { color = color };
            }
            return go.transform;
        }

        /// <summary>Places framed windows (frame + glass + muntins + sill) on all four walls.</summary>
        private static void BuildWindows(Transform root, HouseSpec spec)
        {
            int stories = Mathf.Max(1, spec.stories);
            float storyH = spec.wallHeight / stories;
            float w = 1.1f, hgt = 1.3f;
            float zf = spec.depth * 0.5f + 0.07f;
            float xf = spec.width * 0.5f + 0.07f;
            int sideCount = Mathf.Max(2, Mathf.RoundToInt(spec.depth / 4f));

            // One row of windows per floor, centred in that floor's band.
            for (int s = 0; s < stories; s++)
            {
                float y = s * storyH + storyH * 0.5f;

                // Front & back walls (face ±Z): two windows across the width.
                foreach (float x in new[] { -spec.width * 0.25f, spec.width * 0.25f })
                {
                    BuildWindow(root, new Vector3(x, y, zf), true, w, hgt);
                    BuildWindow(root, new Vector3(x, y, -zf), true, w, hgt);
                }

                // Side walls (face ±X): spaced evenly along the depth.
                for (int k = 0; k < sideCount; k++)
                {
                    float z = -spec.depth * 0.5f + (k + 0.5f) / sideCount * spec.depth;
                    BuildWindow(root, new Vector3(xf, y, z), false, w, hgt);
                    BuildWindow(root, new Vector3(-xf, y, z), false, w, hgt);
                }
            }
        }

        /// <summary>One window mounted on a wall face. <paramref name="faceZ"/> = on a ±Z wall, else a ±X wall.</summary>
        private static void BuildWindow(Transform parent, Vector3 center, bool faceZ, float w, float hgt)
        {
            Color frameC = new Color(0.93f, 0.91f, 0.86f);
            Color glassC = new Color(0.34f, 0.48f, 0.58f);
            Color sillC = new Color(0.80f, 0.78f, 0.72f);

            float outSign = faceZ ? Mathf.Sign(center.z) : Mathf.Sign(center.x);
            Vector3 outDir = faceZ ? new Vector3(0f, 0f, outSign) : new Vector3(outSign, 0f, 0f);

            Vector3 frameScale = faceZ ? new Vector3(w + 0.18f, hgt + 0.18f, 0.10f)
                                       : new Vector3(0.10f, hgt + 0.18f, w + 0.18f);
            CreateBox(parent, "WindowFrame", center, Quaternion.identity, frameScale, frameC, null);

            Vector3 glassScale = faceZ ? new Vector3(w, hgt, 0.06f) : new Vector3(0.06f, hgt, w);
            Transform glass = CreateBox(parent, "WindowGlass", center + outDir * 0.03f,
                Quaternion.identity, glassScale, glassC, null);
            var gr = glass.GetComponent<Renderer>();
            if (gr != null && gr.sharedMaterial != null)
            {
                if (gr.sharedMaterial.HasProperty("_Glossiness")) gr.sharedMaterial.SetFloat("_Glossiness", 0.85f);
                if (gr.sharedMaterial.HasProperty("_Metallic")) gr.sharedMaterial.SetFloat("_Metallic", 0.1f);
            }

            // Muntin bars (a simple cross) sitting just proud of the glass.
            Vector3 vBar = faceZ ? new Vector3(0.04f, hgt, 0.07f) : new Vector3(0.07f, hgt, 0.04f);
            Vector3 hBar = faceZ ? new Vector3(w, 0.04f, 0.07f) : new Vector3(0.07f, 0.04f, w);
            CreateBox(parent, "Muntin", center + outDir * 0.035f, Quaternion.identity, vBar, frameC, null);
            CreateBox(parent, "Muntin", center + outDir * 0.035f, Quaternion.identity, hBar, frameC, null);

            // Sill ledge under the window.
            Vector3 sillPos = center + outDir * 0.04f + new Vector3(0f, -(hgt * 0.5f + 0.08f), 0f);
            Vector3 sillScale = faceZ ? new Vector3(w + 0.28f, 0.08f, 0.16f) : new Vector3(0.16f, 0.08f, w + 0.28f);
            CreateBox(parent, "WindowSill", sillPos, Quaternion.identity, sillScale, sillC, null);
        }

        private const float WallTileSize = 2.6f; // target real-world size of one texture tile (m)

        /// <summary>A thin textured panel for one wall face, tiled so the texture keeps its proportions.</summary>
        private static void WallPanel(Transform parent, string panelName, Vector3 localPos,
            Vector3 localScale, Material baseMat, float faceWidth, float faceHeight)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = panelName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col); // the solid wall block already handles collision

            var r = go.GetComponent<Renderer>();
            if (r != null && baseMat != null)
            {
                var m = new Material(baseMat); // shares textures, own tiling
                m.mainTextureScale = new Vector2(
                    Mathf.Max(1f, faceWidth / WallTileSize),
                    Mathf.Max(1f, faceHeight / WallTileSize));
                r.sharedMaterial = m;
            }
        }

        /// <summary>
        /// House lights for ambience (Etapa 7): a warm porch light at door height on the entry
        /// side plus two eave-corner lights. Cheap point lights, no shadows.
        /// </summary>
        private static void BuildLights(Transform root, HouseSpec spec)
        {
            float x = spec.width * 0.5f;
            PlaceLight(root, new Vector3(x + 0.16f, 2.1f, 1.6f), 1.3f, 7f);                               // porch
            PlaceLight(root, new Vector3(x + 0.16f, spec.wallHeight - 0.25f, spec.depth * 0.5f - 0.4f), 0.9f, 5f);
            PlaceLight(root, new Vector3(x + 0.16f, spec.wallHeight - 0.25f, -spec.depth * 0.5f + 0.4f), 0.9f, 5f);
        }

        private static void PlaceLight(Transform root, Vector3 localPos, float intensity, float range)
        {
            Color warm = new Color(1f, 0.82f, 0.55f);

            var fixture = new GameObject("HouseLight");
            fixture.transform.SetParent(root, false);
            fixture.transform.localPosition = localPos;

            // Dark backplate + glowing bulb.
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var pc = plate.GetComponent<Collider>();
            if (pc != null) Object.Destroy(pc);
            plate.transform.SetParent(fixture.transform, false);
            plate.transform.localScale = new Vector3(0.10f, 0.22f, 0.10f);
            var pr = plate.GetComponent<Renderer>();
            if (pr != null)
                pr.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                { color = new Color(0.15f, 0.15f, 0.17f) };

            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var bc = bulb.GetComponent<Collider>();
            if (bc != null) Object.Destroy(bc);
            bulb.transform.SetParent(fixture.transform, false);
            bulb.transform.localPosition = new Vector3(0.07f, 0f, 0f);
            bulb.transform.localScale = Vector3.one * 0.09f;
            var br = bulb.GetComponent<Renderer>();
            if (br != null)
            {
                var m = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default")) { color = warm };
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", warm * 1.5f);
                br.sharedMaterial = m;
            }

            var light = fixture.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = warm;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        /// <summary>A triangular cap filling the gable end at the given Z (local space).</summary>
        private static void CreateGable(Transform parent, string gableName, float halfW,
            float wallHeight, float ridgeY, float z, Material baseMat)
        {
            var go = new GameObject(gableName);
            go.transform.SetParent(parent, false);

            float gh = Mathf.Max(0.01f, ridgeY - wallHeight);
            var mesh = new Mesh { name = "Gable" };
            mesh.vertices = new[]
            {
                new Vector3(-halfW, wallHeight, z),
                new Vector3(halfW, wallHeight, z),
                new Vector3(0f, ridgeY, z)
            };
            // UVs scaled to the wall tile size so the gable matches the walls below it.
            float us = (2f * halfW) / WallTileSize, vs = gh / WallTileSize;
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(us, 0f),
                new Vector2(us * 0.5f, vs)
            };
            // Two-sided so it reads from inside and out.
            mesh.triangles = z > 0f ? new[] { 0, 2, 1 } : new[] { 0, 1, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = baseMat != null
                ? baseMat
                : new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
        }
    }
}
