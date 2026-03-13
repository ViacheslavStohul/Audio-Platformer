using System.IO;
using UnityEngine;

public static class JsonConverter
{
    private const int CurrentSettingsVersion = 3;

    private static readonly string AppSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
    private static readonly string StatisticsPath = Path.Combine(Directory.GetCurrentDirectory(), "statistics.json");

    #region AppSettings
    public static AppSettings GetDefaultAppSettings()
    {
        return new AppSettings
        {
            SettingsVersion = CurrentSettingsVersion,
            VolumeThreshold = 0.02f,
            LowFrequencyThreshold = 60f,
            MiddleFrequencyThreshold = 800f,
            HighFrequencyThreshold = 2000f,
            InputSmoothing = 12f
        };
    }

    public static AppSettings GetAppSettings()
    {
        if (File.Exists(AppSettingsPath))
        {
            var json = File.ReadAllText(AppSettingsPath);
            var settings = JsonUtility.FromJson<AppSettings>(json);
            var normalizedSettings = NormalizeAppSettings(settings);

            File.WriteAllText(AppSettingsPath, JsonUtility.ToJson(normalizedSettings));
            return normalizedSettings;
        }

        var defaultSettings = GetDefaultAppSettings();

        var defaultJson = JsonUtility.ToJson(defaultSettings);
        File.WriteAllText(AppSettingsPath, defaultJson);
        return defaultSettings;
    }

    public static void WriteSettings(AppSettings newSettings)
    {
        var normalizedSettings = NormalizeAppSettings(newSettings);
        var json = JsonUtility.ToJson(normalizedSettings);
        File.WriteAllText(AppSettingsPath, json);
    }

    private static AppSettings NormalizeAppSettings(AppSettings settings)
    {
        var normalizedSettings = GetDefaultAppSettings();

        if (settings == null)
        {
            return normalizedSettings;
        }

        if (settings.VolumeThreshold > 0)
        {
            normalizedSettings.VolumeThreshold = settings.VolumeThreshold;
        }

        if (settings.LowFrequencyThreshold > 0)
        {
            normalizedSettings.LowFrequencyThreshold = settings.LowFrequencyThreshold;
        }

        if (settings.MiddleFrequencyThreshold > 0)
        {
            normalizedSettings.MiddleFrequencyThreshold = settings.MiddleFrequencyThreshold;
        }

        if (settings.HighFrequencyThreshold > 0)
        {
            normalizedSettings.HighFrequencyThreshold = settings.HighFrequencyThreshold;
        }

        if (settings.InputSmoothing > 0)
        {
            normalizedSettings.InputSmoothing = settings.InputSmoothing;
        }

        normalizedSettings.SettingsVersion = CurrentSettingsVersion;
        return normalizedSettings;
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