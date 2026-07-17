using UnityEngine;

namespace RoofingSimulator.World
{
    /// <summary>
    /// A climbable ladder. Exposes the vertical climb line, the step-off point on the
    /// roof, and the direction the player faces to climb. The trigger volume in front of
    /// it is what <see cref="RoofingSimulator.Player.LadderClimber"/> latches onto.
    ///
    /// If <see cref="stable"/> is false the ladder gets a Rigidbody and can be knocked
    /// over (a hazard when it's not propped properly) — off by default for now.
    /// </summary>
    public class Ladder : MonoBehaviour
    {
        [SerializeField] private bool stable = true;

        public Vector3 ClimbBottom { get; private set; }
        public Vector3 ClimbTop { get; private set; }
        public Vector3 TopExit { get; private set; }
        public Vector3 FaceDirection { get; private set; }

        /// <summary>
        /// Build the ladder between two world points, facing <paramref name="faceDir"/>,
        /// stepping off at <paramref name="topExit"/>. Creates rails, rungs and the
        /// climb trigger volume.
        /// </summary>
        public void Setup(Vector3 bottom, Vector3 top, Vector3 faceDir, Vector3 topExit)
        {
            ClimbBottom = bottom;
            ClimbTop = top;
            TopExit = topExit;
            FaceDirection = new Vector3(faceDir.x, 0f, faceDir.z).normalized;

            transform.position = (bottom + top) * 0.5f;

            BuildVisual(bottom, top);
            BuildTrigger(bottom, top);

            if (!stable)
            {
                var rb = gameObject.AddComponent<Rigidbody>();
                rb.mass = 12f;
            }
        }

        private void BuildVisual(Vector3 bottom, Vector3 top)
        {
            float height = Vector3.Distance(bottom, top);
            Vector3 dir = (top - bottom).normalized;
            // A horizontal axis perpendicular to the climb direction, for rail spacing.
            Vector3 side = Vector3.Cross(dir, FaceDirection).normalized;
            if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(dir, Vector3.forward).normalized;

            Quaternion rot = Quaternion.LookRotation(FaceDirection, dir);
            Color wood = new Color(0.55f, 0.36f, 0.18f);

            CreateBox("Rail_L", (bottom + top) * 0.5f + side * 0.28f, rot,
                new Vector3(0.06f, height, 0.06f), wood);
            CreateBox("Rail_R", (bottom + top) * 0.5f - side * 0.28f, rot,
                new Vector3(0.06f, height, 0.06f), wood);

            int rungs = Mathf.Max(2, Mathf.RoundToInt(height / 0.35f));
            for (int i = 1; i < rungs; i++)
            {
                Vector3 p = Vector3.Lerp(bottom, top, i / (float)rungs);
                CreateBox($"Rung_{i}", p, rot, new Vector3(0.62f, 0.05f, 0.05f), wood);
            }
        }

        private void BuildTrigger(Vector3 bottom, Vector3 top)
        {
            float height = Vector3.Distance(bottom, top);
            var trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            // Local space: the climb volume around the ladder, a bit toward the player.
            trigger.center = transform.InverseTransformPoint((bottom + top) * 0.5f)
                             + new Vector3(0f, 0f, 0f);
            trigger.size = new Vector3(1.3f, height + 0.6f, 1.3f);
        }

        private void CreateBox(string boxName, Vector3 pos, Quaternion rot, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = boxName;
            go.transform.SetParent(transform, true);
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                { color = color };
            }
        }
    }
}
