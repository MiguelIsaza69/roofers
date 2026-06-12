using Mirror;
using UnityEngine;
using RoofingSimulator.Input;

namespace RoofingSimulator.Multiplayer
{
    /// <summary>
    /// Networked player avatar. Synchronizes the player's name, transform, and
    /// "applying" state across clients, and (for the local player) forwards material
    /// applications to the shared <see cref="NetworkRoofingMaterial"/>.
    ///
    /// Local-only objects (camera, PlayerInput, tool visuals) are enabled only on the
    /// owning client so each player drives just their own avatar.
    /// </summary>
    public class PlayerAvatarSync : NetworkBehaviour
    {
        [Header("Local-only objects (camera/input/tool)")]
        [SerializeField] private GameObject[] localOnlyObjects;

        [Header("Name Label")]
        [SerializeField] private TextMesh nameLabel;

        [Header("Sync")]
        [SerializeField] private float sendInterval = 0.05f; // 20 Hz
        [SerializeField] private float interpolationSpeed = 12f;

        [Header("Local Input")]
        [SerializeField] private PlayerInput playerInput;

        [SyncVar(hook = nameof(OnNameChanged))]
        private string playerName = "Roofer";

        [SyncVar] private Vector3 syncPosition;
        [SyncVar] private float syncYaw;
        [SyncVar] private bool syncApplying;

        private float sendTimer;
        private NetworkRoofingMaterial sharedMaterial;

        public string PlayerName => playerName;
        public bool IsApplying => syncApplying;

        public override void OnStartClient()
        {
            MultiplayerManager.Instance?.RegisterPlayer(this);
            ApplyLocalOnlyVisibility();
            OnNameChanged(playerName, playerName);
        }

        public override void OnStopClient()
        {
            MultiplayerManager.Instance?.UnregisterPlayer(this);
        }

        public override void OnStartLocalPlayer()
        {
            // Tell the server our chosen name.
            CmdSetName(MultiplayerManager.Instance != null
                ? MultiplayerManager.Instance.LocalPlayerName
                : "Roofer");

            if (playerInput == null)
            {
                playerInput = GetComponentInChildren<PlayerInput>();
            }
            if (playerInput != null)
            {
                playerInput.OnApplyHeld += HandleLocalApply;
                playerInput.OnApplyStarted += HandleLocalApply;
            }
        }

        private void OnDestroy()
        {
            if (isLocalPlayer && playerInput != null)
            {
                playerInput.OnApplyHeld -= HandleLocalApply;
                playerInput.OnApplyStarted -= HandleLocalApply;
            }
        }

        private void Update()
        {
            if (isLocalPlayer)
            {
                SendLocalTransform();
            }
            else
            {
                InterpolateRemote();
            }
        }

        private void SendLocalTransform()
        {
            sendTimer += Time.deltaTime;
            if (sendTimer < sendInterval)
            {
                return;
            }
            sendTimer = 0f;

            bool applying = playerInput != null && playerInput.IsApplying;
            CmdUpdateState(transform.position, transform.eulerAngles.y, applying);
        }

        private void InterpolateRemote()
        {
            transform.position = Vector3.Lerp(transform.position, syncPosition, Time.deltaTime * interpolationSpeed);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.Euler(0f, syncYaw, 0f), Time.deltaTime * interpolationSpeed);
        }

        private void HandleLocalApply(RoofingRaycastHit hit)
        {
            if (!hit.isRoofSurface && !hit.isExistingMaterial)
            {
                return;
            }

            if (sharedMaterial == null)
            {
                sharedMaterial = FindObjectOfType<NetworkRoofingMaterial>();
            }
            // Server-authoritative: request application; the server validates and
            // broadcasts so every client reproduces the same deformation.
            sharedMaterial?.CmdApply(hit.point);
        }

        [Command]
        private void CmdSetName(string requestedName)
        {
            playerName = string.IsNullOrWhiteSpace(requestedName) ? "Roofer" : requestedName.Trim();
        }

        [Command]
        private void CmdUpdateState(Vector3 position, float yaw, bool applying)
        {
            syncPosition = position;
            syncYaw = yaw;
            syncApplying = applying;
        }

        private void OnNameChanged(string _, string newName)
        {
            if (nameLabel != null)
            {
                nameLabel.text = newName;
            }
        }

        private void ApplyLocalOnlyVisibility()
        {
            if (localOnlyObjects == null)
            {
                return;
            }
            foreach (var go in localOnlyObjects)
            {
                if (go != null)
                {
                    go.SetActive(isLocalPlayer);
                }
            }
        }
    }
}
