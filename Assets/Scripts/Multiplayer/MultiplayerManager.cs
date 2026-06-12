using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace RoofingSimulator.Multiplayer
{
    /// <summary>
    /// High-level facade over Mirror used by the multiplayer UI. Wraps host/client
    /// startup, maps session codes to network addresses, tracks the player roster, and
    /// re-raises connect/disconnect events for the UI.
    ///
    /// Session codes: for direct connections a code IS the host's network address
    /// (IP/hostname). A production build would front this with a relay/matchmaker
    /// (e.g. Mirror's Edgegap relay) that maps short human codes to real addresses;
    /// that mapping is intentionally isolated in <see cref="ResolveAddress"/>.
    /// </summary>
    public class MultiplayerManager : MonoBehaviour
    {
        private static MultiplayerManager instance;
        public static MultiplayerManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<MultiplayerManager>();
                    if (instance == null)
                    {
                        var go = new GameObject("MultiplayerManager");
                        instance = go.AddComponent<MultiplayerManager>();
                    }
                }
                return instance;
            }
        }

        public string LocalPlayerName { get; private set; } = "Roofer";
        public string SessionCode { get; private set; }
        public bool IsHost { get; private set; }

        private readonly List<PlayerAvatarSync> players = new List<PlayerAvatarSync>();
        public IReadOnlyList<PlayerAvatarSync> Players => players;
        public int PlayerCount => players.Count;

        public event Action<string> OnSessionStarted;   // arg = session code
        public event Action OnJoinedSession;
        public event Action<string> OnSessionError;      // arg = message
        public event Action OnDisconnected;
        public event Action<int> OnPlayerCountChanged;

        private RoofingNetworkManager Net => NetworkManager.singleton as RoofingNetworkManager;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ----- Session lifecycle -----

        /// <summary>Host a new session and return its join code.</summary>
        public string CreateSession(string playerName)
        {
            if (!EnsureNetworkManager())
            {
                return null;
            }

            LocalPlayerName = Sanitize(playerName);
            HookNetworkEvents();

            Net.StartHost();
            IsHost = true;
            SessionCode = EncodeCode(Net.networkAddress);

            OnSessionStarted?.Invoke(SessionCode);
            return SessionCode;
        }

        /// <summary>Join an existing session by its code.</summary>
        public void JoinSession(string code, string playerName)
        {
            if (!EnsureNetworkManager())
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                OnSessionError?.Invoke("Enter a session code to join.");
                return;
            }

            LocalPlayerName = Sanitize(playerName);
            HookNetworkEvents();

            Net.networkAddress = ResolveAddress(code);
            SessionCode = code;
            IsHost = false;
            Net.StartClient();
        }

        /// <summary>Leave the current session, stopping host/client as appropriate.</summary>
        public void LeaveSession()
        {
            if (Net == null)
            {
                return;
            }

            if (NetworkServer.active && NetworkClient.isConnected)
            {
                Net.StopHost();
            }
            else if (NetworkClient.isConnected)
            {
                Net.StopClient();
            }
            else if (NetworkServer.active)
            {
                Net.StopServer();
            }

            players.Clear();
            SessionCode = null;
            IsHost = false;
        }

        // ----- Player roster (T061) -----

        public void RegisterPlayer(PlayerAvatarSync player)
        {
            if (player != null && !players.Contains(player))
            {
                players.Add(player);
                OnPlayerCountChanged?.Invoke(players.Count);
            }
        }

        public void UnregisterPlayer(PlayerAvatarSync player)
        {
            if (players.Remove(player))
            {
                OnPlayerCountChanged?.Invoke(players.Count);
            }
        }

        // ----- Internals -----

        private void HookNetworkEvents()
        {
            if (Net == null)
            {
                return;
            }
            // Avoid double-subscription.
            Net.ServerPlayerCountChanged -= HandleServerPlayerCount;
            Net.ServerPlayerCountChanged += HandleServerPlayerCount;
            Net.ClientDisconnectedFromHost -= HandleClientDisconnected;
            Net.ClientDisconnectedFromHost += HandleClientDisconnected;
        }

        private void HandleServerPlayerCount(int count)
        {
            OnPlayerCountChanged?.Invoke(count);
        }

        private void HandleClientDisconnected()
        {
            players.Clear();
            OnDisconnected?.Invoke();
        }

        private bool EnsureNetworkManager()
        {
            if (Net == null)
            {
                OnSessionError?.Invoke("No RoofingNetworkManager in the scene.");
                Debug.LogError("MultiplayerManager requires a RoofingNetworkManager (NetworkManager.singleton).");
                return false;
            }
            return true;
        }

        private static string Sanitize(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? "Roofer" : name.Trim();
        }

        // Direct-connect codes are just the address. A relay layer would swap these out.
        private static string EncodeCode(string address)
        {
            return string.IsNullOrEmpty(address) ? "localhost" : address;
        }

        private static string ResolveAddress(string code)
        {
            return code.Trim();
        }
    }
}
