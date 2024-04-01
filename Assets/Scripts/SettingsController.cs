using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts
{
    public class SettingsController : MonoBehaviour
    {
#pragma warning disable CS0649
        [SerializeField] private TMP_Text _volumeText;
        [SerializeField] private TMP_Text _lowText;
        [SerializeField] private TMP_Text _middleText;
        [SerializeField] private TMP_Text _highText;
        [SerializeField] private TMP_Text _saveText;
        [SerializeField] private Slider _volumeSlider;
        [SerializeField] private Slider _lowSlider;
        [SerializeField] private Slider _middleSlider;
        [SerializeField] private Slider _highSlider;
#pragma warning restore CS0649

        private void Start()
        {
            GetSettings();

            _saveText.enabled = false;
        }

        private void FixedUpdate()
        {
            _volumeText.text = $"Noise reduction: {Mathf.Round(_volumeSlider.value * 1000f)}";
            _lowText.text = $"Minimal frequency: {Mathf.Round(_lowSlider.value * 1f) / 1f}Gz";
            _middleText.text = $"Flight frequency: {Mathf.Round(_middleSlider.value * 1f) / 1f}Gz";
            _highText.text = $"Max frequency: {Mathf.Round(_highSlider.value * 1f) / 1f}Gz";
        }

        public void ResetSettings()
        {
            AppSettings defaultSettings = new()
            {
                VolumeThreshold = 0.05f,
                LowFrequencyThreshold = 60f,
                MiddleFrequencyThreshold = 800f,
                HighFrequencyThreshold = 2000f,
            };

            JsonConverter.WriteSettings(defaultSettings);

            GetSettings();

            _saveText.enabled = true;
            Invoke(nameof(DisableSaveText), 1.5f);
        }

        public void SaveSettings()
        {
            AppSettings settings = new()
            {
                VolumeThreshold = Mathf.Round(_volumeSlider.value * 1000f) / 1000f,
                LowFrequencyThreshold = Mathf.Round(_lowSlider.value * 1f) / 1f,
                MiddleFrequencyThreshold = Mathf.Round(_middleSlider.value * 1f) / 1f,
                HighFrequencyThreshold = Mathf.Round(_highSlider.value * 1f) / 1f,
            };

            JsonConverter.WriteSettings(settings);

            _saveText.enabled = true;

            Invoke(nameof(DisableSaveText), 1.5f);
        }

        private void GetSettings()
        {
            var settings = JsonConverter.GetAppSettings();

            _volumeText.text = $"Noise reduction: {Mathf.Round(settings.VolumeThreshold * 1000f)}";
            _volumeSlider.value = settings.VolumeThreshold;

            _lowText.text = $"Minimal frequency: {settings.LowFrequencyThreshold}Gz";
            _lowSlider.value = settings.LowFrequencyThreshold;

            _middleText.text = $"Flight frequency: {settings.MiddleFrequencyThreshold}Gz";
            _middleSlider.value = settings.MiddleFrequencyThreshold;

            _highText.text = $"Max frequency: {settings.HighFrequencyThreshold}Gz";
            _highSlider.value = settings.HighFrequencyThreshold;
        }

        private void DisableSaveText()
        {
            _saveText.enabled = false;
        }
    }
}
