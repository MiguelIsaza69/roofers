using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using RoofingSimulator.Core;

namespace RoofingSimulator.Persistence
{
    /// <summary>
    /// Manages career save and load operations.
    /// Handles JSON serialization, file I/O, and backup management.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        private static SaveManager instance;
        private string savePath;

        public static SaveManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<SaveManager>();
                    if (instance == null)
                    {
                        var go = new GameObject("SaveManager");
                        instance = go.AddComponent<SaveManager>();
                    }
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            // Set save path
            savePath = Path.Combine(Application.persistentDataPath, "saves");
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            Debug.Log($"SaveManager initialized. Save path: {savePath}");
        }

        public void SaveCareer(Career career)
        {
            if (career == null)
            {
                Debug.LogError("Cannot save null career");
                return;
            }

            try
            {
                career.UpdateLastModified();
                string fileName = $"{career.playerName}_career.json";
                string filePath = Path.Combine(savePath, fileName);

                // Create backup of existing save
                if (File.Exists(filePath))
                {
                    string backupPath = Path.Combine(savePath, $"{career.playerName}_career.backup.json");
                    File.Copy(filePath, backupPath, true);
                    Debug.Log($"Backup created: {backupPath}");
                }

                // Serialize career to JSON
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore,
                    DateFormatString = "yyyy-MM-ddTHH:mm:ssZ"
                };

                string json = JsonConvert.SerializeObject(career, settings);

                // Write to file
                File.WriteAllText(filePath, json);
                Debug.Log($"Career saved successfully: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save career: {e.Message}");
            }
        }

        public Career LoadCareer(string playerName)
        {
            try
            {
                string fileName = $"{playerName}_career.json";
                string filePath = Path.Combine(savePath, fileName);

                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"Save file not found: {filePath}");
                    return null;
                }

                string json = File.ReadAllText(filePath);

                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DateFormatString = "yyyy-MM-ddTHH:mm:ssZ"
                };

                Career career = JsonConvert.DeserializeObject<Career>(json, settings);

                if (career == null)
                {
                    Debug.LogError("Failed to deserialize career JSON");
                    return null;
                }

                Debug.Log($"Career loaded successfully: {playerName}");
                return career;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load career: {e.Message}");
                return null;
            }
        }

        public string[] GetSavedCareerNames()
        {
            try
            {
                if (!Directory.Exists(savePath))
                    return new string[0];

                string[] files = Directory.GetFiles(savePath, "*_career.json");
                string[] names = new string[files.Length];

                for (int i = 0; i < files.Length; i++)
                {
                    string fileName = Path.GetFileName(files[i]);
                    names[i] = fileName.Replace("_career.json", "");
                }

                return names;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to get saved career names: {e.Message}");
                return new string[0];
            }
        }

        public bool CareerExists(string playerName)
        {
            string fileName = $"{playerName}_career.json";
            string filePath = Path.Combine(savePath, fileName);
            return File.Exists(filePath);
        }

        public void DeleteCareer(string playerName)
        {
            try
            {
                string fileName = $"{playerName}_career.json";
                string filePath = Path.Combine(savePath, fileName);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Debug.Log($"Career deleted: {playerName}");
                }
                else
                {
                    Debug.LogWarning($"Career file not found: {playerName}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to delete career: {e.Message}");
            }
        }

        public void RestoreFromBackup(string playerName)
        {
            try
            {
                string backupPath = Path.Combine(savePath, $"{playerName}_career.backup.json");
                string filePath = Path.Combine(savePath, $"{playerName}_career.json");

                if (!File.Exists(backupPath))
                {
                    Debug.LogWarning($"Backup file not found: {backupPath}");
                    return;
                }

                File.Copy(backupPath, filePath, true);
                Debug.Log($"Career restored from backup: {playerName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to restore from backup: {e.Message}");
            }
        }
    }
}
