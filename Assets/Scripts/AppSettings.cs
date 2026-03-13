using System;

[Serializable]
public class AppSettings
{
    public int SettingsVersion;
    public float VolumeThreshold;
    public float LowFrequencyThreshold;
    public float MiddleFrequencyThreshold;
    public float HighFrequencyThreshold;
    public float InputSmoothing;
}