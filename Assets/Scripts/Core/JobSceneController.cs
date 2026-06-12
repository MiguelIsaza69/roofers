using UnityEngine;
using RoofingSimulator.Gameplay;
using RoofingSimulator.Input;
using RoofingSimulator.UI;

namespace RoofingSimulator.Core
{
    /// <summary>
    /// Drives the in-job gameplay scene. Sets up the job instance (loading the
    /// selected job's config via <see cref="GameManager"/>), ensures a roof surface
    /// and player rig exist, binds the HUD and material tool, and ends the job on
    /// completion, failure, or player input.
    ///
    /// All scene objects can be authored in the Editor and assigned to the serialized
    /// fields; anything left null is created procedurally so the loop is fully
    /// playable without hand-authoring the scene (a test/bootstrap harness).
    /// </summary>
    public class JobSceneController : MonoBehaviour
    {
        [Header("Scene References (optional - created if null)")]
        [SerializeField] private RoofingJobInstance jobInstance;
        [SerializeField] private RoofSurface roofSurface;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private RoofingMaterialTool materialTool;
        [SerializeField] private HUD hud;

        [Header("Controls")]
        [SerializeField] private KeyCode finishKey = KeyCode.Return;
        [SerializeField] private KeyCode abandonKey = KeyCode.Escape;

        private bool jobEnded;

        private void Start()
        {
            EnsureJobInstance();
            EnsureRoofSurface();

            // Load the selected job's configuration into the instance.
            if (!GameManager.Instance.InitializeActiveJob(jobInstance))
            {
                Debug.LogError("Failed to initialize the active job; returning to career.");
                GameManager.Instance.GoToCareerOverview();
                return;
            }

            EnsurePlayerRig();
            EnsureTool();

            if (hud != null)
            {
                hud.Bind(jobInstance);
            }

            jobInstance.OnJobCompleted += HandleJobCompleted;
            jobInstance.OnJobFailed += HandleJobFailed;

            jobInstance.StartJob();
        }

        private void Update()
        {
            if (jobEnded)
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(finishKey))
            {
                EndJob();
            }
            else if (UnityEngine.Input.GetKeyDown(abandonKey))
            {
                jobInstance.AbandonJob();
                jobEnded = true;
                GameManager.Instance.GoToCareerOverview();
            }
        }

        private void HandleJobCompleted(RoofingJobInstance instance)
        {
            // Objective met; the player may keep going or finish. Auto-finishing here
            // would remove that choice, so we just let the HUD reflect the met state.
        }

        private void HandleJobFailed(RoofingJobInstance instance, JobFailureReason reason)
        {
            EndJob();
        }

        private void EndJob()
        {
            if (jobEnded)
            {
                return;
            }
            jobEnded = true;
            GameManager.Instance.FinishJob(jobInstance);
        }

        // ----- Procedural setup helpers -----

        private void EnsureJobInstance()
        {
            if (jobInstance == null)
            {
                jobInstance = FindObjectOfType<RoofingJobInstance>();
            }
            if (jobInstance == null)
            {
                var go = new GameObject("RoofingJobInstance");
                jobInstance = go.AddComponent<RoofingJobInstance>();
            }
        }

        private void EnsureRoofSurface()
        {
            if (roofSurface == null)
            {
                roofSurface = FindObjectOfType<RoofSurface>();
            }
            if (roofSurface == null)
            {
                roofSurface = BuildProceduralRoof();
            }
            jobInstance.roofSurface = roofSurface;
        }

        /// <summary>
        /// Builds a flat square roof sized to the selected job's surface area. A real
        /// build would load an authored mesh (Assets/Models/*.fbx); this stand-in keeps
        /// the gameplay loop testable.
        /// </summary>
        private RoofSurface BuildProceduralRoof()
        {
            RoofingJob job = CareerManager.Instance.GetJob(GameManager.Instance.SelectedJobIndex);
            float area = job != null ? job.surfaceArea : 45f;
            float side = Mathf.Sqrt(area);

            var go = new GameObject("Roof");
            go.tag = "RoofSurface";

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildQuadMesh(side);

            var renderer = go.AddComponent<MeshRenderer>();
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            renderer.sharedMaterial = new Material(shader) { color = new Color(0.4f, 0.4f, 0.45f) };

            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;

            var surface = go.AddComponent<RoofSurface>();
            surface.id = job != null ? job.roofGeometryId : "procedural_roof";
            surface.totalArea = area;
            surface.roofMesh = filter.sharedMesh;
            surface.InitializeCoverageSampling();

            return surface;
        }

        /// <summary>Creates an upward-facing square quad of the given side length, centred at origin.</summary>
        private static Mesh BuildQuadMesh(float side)
        {
            float h = side * 0.5f;
            var mesh = new Mesh { name = "RoofQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-h, 0f, -h),
                new Vector3(-h, 0f,  h),
                new Vector3( h, 0f,  h),
                new Vector3( h, 0f, -h)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void EnsurePlayerRig()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
            if (playerCamera == null)
            {
                var camGo = new GameObject("PlayerCamera");
                playerCamera = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }

            // Position above and back from the roof, angled down to paint it.
            float side = Mathf.Sqrt(roofSurface != null ? roofSurface.totalArea : 45f);
            playerCamera.transform.position = new Vector3(0f, side * 0.8f, -side * 0.9f);
            playerCamera.transform.rotation = Quaternion.Euler(40f, 0f, 0f);

            if (playerInput == null)
            {
                playerInput = playerCamera.GetComponent<PlayerInput>()
                              ?? playerCamera.gameObject.AddComponent<PlayerInput>();
            }
        }

        private void EnsureTool()
        {
            if (materialTool == null)
            {
                materialTool = FindObjectOfType<RoofingMaterialTool>();
            }
            if (materialTool == null)
            {
                var go = new GameObject("RoofingMaterialTool");
                materialTool = go.AddComponent<RoofingMaterialTool>();
            }
            materialTool.Bind(jobInstance, playerInput);
        }
    }
}
