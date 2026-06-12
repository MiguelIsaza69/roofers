using System;
using Mirror;
using UnityEngine;

namespace RoofingSimulator.Multiplayer
{
    /// <summary>
    /// Custom Mirror <see cref="NetworkManager"/> for roofing sessions. Caps the
    /// session at 4 players, spawns player avatars at spawn points, and surfaces
    /// connect/disconnect events for <see cref="MultiplayerManager"/> to relay to UI.
    ///
    /// EDITOR SETUP (T051/T054): place this on a NetworkManager GameObject with a
    /// Transport component, assign <c>playerPrefab</c> to a PlayerAvatar prefab that
    /// has a NetworkIdentity + <see cref="PlayerAvatarSync"/>, and register the
    /// material/roof networked objects as spawnable prefabs.
    /// </summary>
    public class RoofingNetworkManager : NetworkManager
    {
        [Header("Roofing Session")]
        [Tooltip("Optional spawn points; players cycle through these. Falls back to origin.")]
        [SerializeField] private Transform[] spawnPoints;

        /// <summary>Server: raised when a player connects or disconnects (arg = player count).</summary>
        public event Action<int> ServerPlayerCountChanged;
        /// <summary>Client: raised when this client loses its connection to the host.</summary>
        public event Action ClientDisconnectedFromHost;

        private int spawnIndex;

        public override void Awake()
        {
            base.Awake();
            // Hard cap per the spec (2-4 players); host counts as one connection.
            maxConnections = 4;
        }

        // ----- Server callbacks -----

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            Transform spawn = GetNextSpawn();

            GameObject player = spawn != null
                ? Instantiate(playerPrefab, spawn.position, spawn.rotation)
                : Instantiate(playerPrefab);

            NetworkServer.AddPlayerForConnection(conn, player);
            ServerPlayerCountChanged?.Invoke(NetworkServer.connections.Count);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // Base destroys the player object. Material the player applied is owned by
            // the shared NetworkRoofingMaterial (scene object), so it is preserved.
            base.OnServerDisconnect(conn);
            ServerPlayerCountChanged?.Invoke(NetworkServer.connections.Count);
        }

        // ----- Client callbacks -----

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            ClientDisconnectedFromHost?.Invoke();
        }

        private Transform GetNextSpawn()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return null;
            }
            Transform spawn = spawnPoints[spawnIndex % spawnPoints.Length];
            spawnIndex++;
            return spawn;
        }
    }
}
