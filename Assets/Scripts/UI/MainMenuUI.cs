using UnityEngine;
using UnityEngine.UI;
using RoofingSimulator.Core;

namespace RoofingSimulator.UI
{
    /// <summary>
    /// Main menu controller. Lets the player start a new career (entering a name) or
    /// load an existing one from a list of saves. Wire the serialized fields to the
    /// scene's UI objects in the Unity Editor.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("New Career")]
        [SerializeField] private InputField playerNameInput;
        [SerializeField] private Button newCareerButton;

        [Header("Load Career")]
        [SerializeField] private Button loadCareerButton;
        [SerializeField] private Transform savedCareerListContainer;
        [SerializeField] private Button savedCareerButtonPrefab;

        [Header("Feedback")]
        [SerializeField] private Text messageLabel;

        private void Awake()
        {
            if (newCareerButton != null)
            {
                newCareerButton.onClick.AddListener(OnNewCareerClicked);
            }
            if (loadCareerButton != null)
            {
                loadCareerButton.onClick.AddListener(RefreshSavedCareers);
            }
        }

        private void Start()
        {
            RefreshSavedCareers();
        }

        private void OnNewCareerClicked()
        {
            string playerName = playerNameInput != null ? playerNameInput.text : null;

            if (string.IsNullOrWhiteSpace(playerName))
            {
                ShowMessage("Please enter a player name.");
                return;
            }

            if (CareerManager.Instance.GetSavedCareerNames().Length > 0 &&
                System.Array.IndexOf(CareerManager.Instance.GetSavedCareerNames(), playerName) >= 0)
            {
                ShowMessage($"A career named '{playerName}' already exists. Load it instead.");
                return;
            }

            GameManager.Instance.StartNewCareer(playerName.Trim());
        }

        private void RefreshSavedCareers()
        {
            if (savedCareerListContainer == null || savedCareerButtonPrefab == null)
            {
                return;
            }

            // Clear existing entries.
            for (int i = savedCareerListContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(savedCareerListContainer.GetChild(i).gameObject);
            }

            string[] names = CareerManager.Instance.GetSavedCareerNames();
            if (names.Length == 0)
            {
                ShowMessage("No saved careers found. Start a new one!");
                return;
            }

            foreach (string name in names)
            {
                Button entry = Instantiate(savedCareerButtonPrefab, savedCareerListContainer);
                var label = entry.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = name;
                }

                string captured = name; // avoid closure capture bug
                entry.onClick.AddListener(() => OnLoadCareer(captured));
            }
        }

        private void OnLoadCareer(string playerName)
        {
            if (!GameManager.Instance.ContinueCareer(playerName))
            {
                ShowMessage($"Failed to load career '{playerName}'.");
            }
        }

        private void ShowMessage(string message)
        {
            if (messageLabel != null)
            {
                messageLabel.text = message;
            }
            Debug.Log($"[MainMenu] {message}");
        }
    }
}
