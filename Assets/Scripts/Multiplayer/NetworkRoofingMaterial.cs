using System.Collections.Generic;
using Mirror;
using UnityEngine;
using RoofingSimulator.Core;
using RoofingSimulator.Gameplay;
using RoofingSimulator.UI;

namespace RoofingSimulator.Multiplayer
{
    /// <summary>
    /// Shared, server-authoritative roofing material field for a multiplayer job.
    /// One instance lives in the multiplayer scene. Clients request applications via
    /// <see cref="CmdApply"/>; the server validates against the job budget, assigns a
    /// deterministic blob id, and broadcasts the (quantized) application to every
    /// client via <see cref="RpcApply"/>. Because <see cref="MaterialPhysics.Deform"/>
    /// is deterministic, replaying the same id+point on every client converges the
    /// material state — far cheaper than streaming raw mesh vertices, while satisfying
    /// the contract's "delta + quantized positions" goal (T058/T059).
    ///
    /// Server-authoritative coverage % is published via a SyncVar (T061) so every
    /// client's HUD shows the same canonical value.
    ///
    /// Job selection is synchronized via the <see cref="activeJobIndex"/> SyncVar: the
    /// server picks the job (from the host's selection / career) and every client loads
    /// and starts the *same* job config, so a co-op match plays one shared job. This
    /// component owns the job lifecycle in multiplayer — the multiplayer scene does NOT
    /// use JobSceneController.
    /// </summary>
    public class NetworkRoofingMaterial : NetworkBehaviour
    {
        [Header("Shared Job")]
        [SerializeField] private RoofingJobInstance jobInstance;
        [Tooltip("Optional HUD bound to the shared job when it starts.")]
        [SerializeField] private HUD hud;

        [Header("Application")]
        [SerializeField] private float massPerApply = 2f;
        [SerializeField] private float mergeRadius = 0.3f;
        [SerializeField] private float quantizeStep = 0.01f; // 1cm

        [Header("Coverage Sync")]
        [SerializeField] private float coverageSyncInterval = 1f;

        [SyncVar] private float syncedCoverage;
        public float SyncedCoverage => syncedCoverage;

        // Synchronized job selection: server sets it, clients load the same job.
        [SyncVar(hook = nameof(OnActiveJobChanged))]
        private int activeJobIndex = -1;
        public int ActiveJobIndex => activeJobIndex;

        // Per-client reproduction of the shared blobs, keyed by server-assigned id.
        private readonly Dictionary<int, RoofingMaterial> blobs = new Dictionary<int, RoofingMaterial>();
        private int nextBlobId;
        private float coverageTimer;
        private bool jobStarted;

        public void BindJob(RoofingJobInstance instance)
        {
            jobInstance = instance;
        }

        // ----- Synchronized job lifecycle -----

        public override void OnStartServer()
        {
            // The server decides which job the session plays and publishes it.
            activeJobIndex = ResolveServerJobIndex();
            InitializeAndStartJob(activeJobIndex);
        }

        public override void OnStartClient()
        {
            // Host already initialized in OnStartServer. A pure client usually has the
            // SyncVar populated by spawn time; the hook covers a late arrival.
            if (!isServer && activeJobIndex >= 0)
            {
                InitializeAndStartJob(activeJobIndex);
            }
        }

        private void OnActiveJobChanged(int _, int newIndex)
        {
            if (!isServer)
            {
                InitializeAndStartJob(newIndex);
            }
        }

        /// <summary>
        /// Pick the session's job: the host's explicit selection, else their career's
        /// current job, else the first job. Server-side only.
        /// </summary>
        private int ResolveServerJobIndex()
        {
            int selected = GameManager.Instance != null ? GameManager.Instance.SelectedJobIndex : -1;
            if (selected >= 0)
            {
                return selected;
            }

            Career career = CareerManager.Instance.ActiveCareer;
            return career != null ? Mathf.Clamp(career.currentJobIndex, 0, CareerManager.Instance.TotalJobs - 1) : 0;
        }

        /// <summary>
        /// Load the shared job config into the local job instance and start it.
        /// Idempotent: safe to call from OnStartServer/OnStartClient and the SyncVar hook.
        /// </summary>
        private void InitializeAndStartJob(int index)
        {
            if (jobStarted || jobInstance == null || index < 0)
            {
                return;
            }

            if (CareerManager.Instance.InitializeJobInstance(jobInstance, index))
            {
                jobInstance.StartJob();
                if (hud != null)
                {
                    hud.Bind(jobInstance);
                }
                jobStarted = true;
            }
        }

        /// <summary>
        /// Client -> server request to apply material at a world point. requiresAuthority
        /// is false because the field is a shared scene object, not owned by any client.
        /// </summary>
        [Command(requiresAuthority = false)]
        public void CmdApply(Vector3 point)
        {
            if (jobInstance == null || jobInstance.CurrentState != JobState.IN_PROGRESS)
            {
                return;
            }
            if (!jobInstance.HasMaterialAvailable)
            {
                return;
            }

            int blobId = ResolveBlobId(point);

            // Apply authoritatively on the server, then consume budget.
            ApplyToBlob(blobId, point);
            jobInstance.ConsumeMaterial(massPerApply);

            RpcApply(Quantize(point), blobId);
        }

        [ClientRpc]
        private void RpcApply(Vector3 point, int blobId)
        {
            // The host already applied during the Command; avoid double-applying.
            if (isServer)
            {
                return;
            }
            ApplyToBlob(blobId, point);
        }

        /// <summary>Find the blob this application belongs to, assigning a new id if none is near.</summary>
        private int ResolveBlobId(Vector3 point)
        {
            float nearestSqr = mergeRadius * mergeRadius;
            int nearestId = -1;

            foreach (var kvp in blobs)
            {
                if (kvp.Value == null)
                {
                    continue;
                }
                float sqr = (kvp.Value.transform.position - point).sqrMagnitude;
                if (sqr <= nearestSqr)
                {
                    nearestSqr = sqr;
                    nearestId = kvp.Key;
                }
            }

            return nearestId >= 0 ? nearestId : nextBlobId++;
        }

        /// <summary>Create or continue the blob with the given id at the point (runs on every peer).</summary>
        private void ApplyToBlob(int blobId, Vector3 point)
        {
            if (blobs.TryGetValue(blobId, out RoofingMaterial blob) && blob != null)
            {
                blob.ApplyAt(point, massPerApply);
                return;
            }

            RoofingMaterial spawned = MaterialBlobFactory.Spawn(point, massPerApply);
            blobs[blobId] = spawned;

            // Register with the local job instance so coverage sampling sees it.
            if (jobInstance != null)
            {
                jobInstance.RegisterMaterial(spawned);
            }

            // Keep server-side nextBlobId ahead of any id we learn about as a client.
            if (blobId >= nextBlobId)
            {
                nextBlobId = blobId + 1;
            }
        }

        private void Update()
        {
            // Server publishes canonical coverage on a slow tick (T061).
            if (!isServer || jobInstance == null)
            {
                return;
            }

            coverageTimer += Time.deltaTime;
            if (coverageTimer >= coverageSyncInterval)
            {
                coverageTimer = 0f;
                syncedCoverage = jobInstance.CurrentCoveragePercent;
            }
        }

        private Vector3 Quantize(Vector3 v)
        {
            return new Vector3(
                Mathf.Round(v.x / quantizeStep) * quantizeStep,
                Mathf.Round(v.y / quantizeStep) * quantizeStep,
                Mathf.Round(v.z / quantizeStep) * quantizeStep);
        }
    }
}
