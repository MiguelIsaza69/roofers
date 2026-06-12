using UnityEngine;
using UnityEngine.UI;
using RoofingSimulator.Core;

namespace RoofingSimulator.Multiplayer
{
    /// <summary>
    /// Multiplayer lobby + in-session status UI. Handles hosting/joining a session,
    /// shows the session code and player count, and surfaces disconnect notifications
    /// with continue/abandon options (T062/T063).
    /// </summary>
    public class MultiplayerUI : MonoBehaviour
    {
        [Header("Lobby")]
        [SerializeField] private InputField playerNameInput;
        [SerializeField] private InputField joinCodeInput;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;

        [Header("Session Status")]
        [SerializeField] private GameObject statusPanel;
        [SerializeField] private Text sessionCodeLabel;
        [SerializeField] private Text playerCountLabel;
        [SerializeField] private Button leaveButton;

        [Header("Notifications")]
        [SerializeField] private GameObject disconnectPanel;
        [SerializeField] private Text disconnectMessage;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button abandonButton;

        [Header("Feedback")]
        [SerializeField] private Text errorLabel;

        private MultiplayerManager Mp => MultiplayerManager.Instance;

        private void Awake()
        {
            if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
            if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
            if (abandonButton != null) abandonButton.onClick.AddListener(OnAbandonClicked);

            SetActive(statusPanel, false);
            SetActive(disconnectPanel, false);
        }

        private void OnEnable()
        {
            Mp.OnSessionStarted += HandleSessionStarted;
            Mp.OnJoinedSession += HandleJoinedSession;
            Mp.OnSessionError += HandleError;
            Mp.OnDisconnected += HandleDisconnected;
            Mp.OnPlayerCountChanged += HandlePlayerCount;
        }

        private void OnDisable()
        {
            Mp.OnSessionStarted -= HandleSessionStarted;
            Mp.OnJoinedSession -= HandleJoinedSession;
            Mp.OnSessionError -= HandleError;
            Mp.OnDisconnected -= HandleDisconnected;
            Mp.OnPlayerCountChanged -= HandlePlayerCount;
        }

        // ----- Button handlers -----

        private void OnHostClicked()
        {
            ClearError();
            Mp.CreateSession(GetPlayerName());
        }

        private void OnJoinClicked()
        {
            ClearError();
            Mp.JoinSession(joinCodeInput != null ? joinCodeInput.text : null, GetPlayerName());
        }

        private void OnLeaveClicked()
        {
            Mp.LeaveSession();
            SetActive(statusPanel, false);
            GameManager.Instance.GoToCareerOverview();
        }

        private void OnContinueClicked()
        {
            // Host left or a peer dropped; remaining player keeps the job solo.
            SetActive(disconnectPanel, false);
        }

        private void OnAbandonClicked()
        {
            SetActive(disconnectPanel, false);
            Mp.LeaveSession();
            GameManager.Instance.GoToCareerOverview();
        }

        // ----- Event handlers -----

        private void HandleSessionStarted(string code)
        {
            SetActive(statusPanel, true);
            if (sessionCodeLabel != null)
            {
                sessionCodeLabel.text = $"Session code: {code}";
            }
            HandlePlayerCount(Mp.PlayerCount);
        }

        private void HandleJoinedSession()
        {
            SetActive(statusPanel, true);
        }

        private void HandlePlayerCount(int count)
        {
            if (playerCountLabel != null)
            {
                playerCountLabel.text = $"Players: {Mathf.Max(1, count)} / 4";
            }
        }

        private void HandleDisconnected()
        {
            SetActive(disconnectPanel, true);
            if (disconnectMessage != null)
            {
                disconnectMessage.text = Mp.IsHost
                    ? "A player disconnected."
                    : "Disconnected from host.";
            }
        }

        private void HandleError(string message)
        {
            if (errorLabel != null)
            {
                errorLabel.text = message;
            }
            Debug.LogWarning($"[Multiplayer] {message}");
        }

        // ----- Helpers -----

        private string GetPlayerName()
        {
            return playerNameInput != null ? playerNameInput.text : "Roofer";
        }

        private void ClearError()
        {
            if (errorLabel != null)
            {
                errorLabel.text = string.Empty;
            }
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null)
            {
                go.SetActive(active);
            }
        }
    }
}
