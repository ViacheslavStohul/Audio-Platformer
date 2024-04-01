using System.IO;
using UnityEngine;

namespace Assets.Scripts
{
    public static class JsonConverter
    {

        private static readonly string AppSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        private static readonly string StatisticsPath = Path.Combine(Directory.GetCurrentDirectory(), "statistics.json");

        #region AppSettings
        public static AppSettings GetAppSettings()
        {
            if (File.Exists(AppSettingsPath))
            {
                var json = File.ReadAllText(AppSettingsPath);
                return JsonUtility.FromJson<AppSettings>(json);
            }

            AppSettings defaultSettings = new()
            {
                VolumeThreshold = 0.05f,
                LowFrequencyThreshold = 60f,
                MiddleFrequencyThreshold = 800f,
                HighFrequencyThreshold = 2000f,
            };

            var defaultJson = JsonUtility.ToJson(defaultSettings);
            File.WriteAllText(AppSettingsPath, defaultJson);
            return defaultSettings;
        }

        public static void WriteSettings(AppSettings newSettings)
        {
            var existingSettings = GetAppSettings();

            existingSettings.VolumeThreshold = newSettings.VolumeThreshold;
            existingSettings.HighFrequencyThreshold = newSettings.HighFrequencyThreshold;
            existingSettings.MiddleFrequencyThreshold = newSettings.MiddleFrequencyThreshold;
            existingSettings.LowFrequencyThreshold = newSettings.LowFrequencyThreshold;

            var json = JsonUtility.ToJson(existingSettings);
            File.WriteAllText(AppSettingsPath, json);
        }
        #endregion

        #region Statistics
        public static Statistics GetStatistics()
        {
            if (File.Exists(StatisticsPath))
            {
                var json = File.ReadAllText(StatisticsPath);
                return JsonUtility.FromJson<Statistics>(json);
            }

            Statistics defaultStatistics = new()
            {
                ItemsCollected = 0,
                Deaths = 0
            };

            var defaultJson = JsonUtility.ToJson(defaultStatistics);
            File.WriteAllText(StatisticsPath, defaultJson);
            return defaultStatistics;
        }

        public static void AddCollectedItem()
        {
            var settings = GetStatistics();
            settings.ItemsCollected++;
            var json = JsonUtility.ToJson(settings);
            File.WriteAllText(StatisticsPath, json);
        }

        public static void AddDeath()
        {
            var settings = GetStatistics();
            settings.Deaths++;
            var json = JsonUtility.ToJson(settings);
            File.WriteAllText(StatisticsPath, json);
        }
        #endregion
    }
}
