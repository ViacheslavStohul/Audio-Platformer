using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts
{
    public class SettingsController : MonoBehaviour
    {
        private enum CalibrationTarget
        {
            Quiet,
            Comfortable,
            Loud
        }

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

        private const int SampleSize = 1024;
        private const float MinDetectableFrequency = 30f;
        private const float MaxDetectableFrequency = 3000f;
        private const float FrequencyGap = 60f;
        private const float CalibrationDuration = 2f;
        private const float CalibrationCooldown = 0.15f;
        private const float MinimumCaptureLevel = 0.0008f;
        private const float CorrectingValue = 2f;
        private const float MicrophoneStartupTimeout = 1f;

        private readonly float[] _spectrumData = new float[SampleSize];
        private readonly Dictionary<Slider, TMP_InputField> _inputsBySlider = new();

        private AudioSource _audioSource;
        private string _microphoneName;
        private TMP_InputField _volumeInput;
        private TMP_InputField _lowInput;
        private TMP_InputField _middleInput;
        private TMP_InputField _highInput;
        private TextMeshProUGUI _monitorText;
        private TextMeshProUGUI _statusText;
        private Coroutine _calibrationCoroutine;
        private bool _isSyncingUi;
        private bool _uiBuilt;
        private float _liveFrequency;
        private float _liveLevel;

        private void Start()
        {
            ConfigureSliderRanges();
            BuildRuntimeUi();
            HookSliderEvents();
            StartMicrophoneMonitor();
            GetSettings();

            _saveText.enabled = false;
            UpdateLabels();
            SyncInputsFromSliders(true);
        }

        private void Update()
        {
            UpdateLabels();
            SyncInputsFromSliders(false);
            UpdateLiveMonitor();
        }

        private void OnDisable()
        {
            if (_calibrationCoroutine != null)
            {
                StopCoroutine(_calibrationCoroutine);
                _calibrationCoroutine = null;
            }

            if (!string.IsNullOrWhiteSpace(_microphoneName))
            {
                Microphone.End(_microphoneName);
            }

            if (_audioSource != null)
            {
                _audioSource.Stop();
            }
        }

        public void ResetSettings()
        {
            var defaultSettings = JsonConverter.GetDefaultAppSettings();
            JsonConverter.WriteSettings(defaultSettings);

            GetSettings();
            UpdateLabels();
            SyncInputsFromSliders(true);
            SetStatus("Default settings restored.");

            _saveText.enabled = true;
            Invoke(nameof(DisableSaveText), 1.5f);
        }

        public void SaveSettings()
        {
            var settings = JsonConverter.GetAppSettings();
            settings.VolumeThreshold = Mathf.Round(_volumeSlider.value * 1000f) / 1000f;
            settings.LowFrequencyThreshold = Mathf.Round(_lowSlider.value);
            settings.MiddleFrequencyThreshold = Mathf.Round(_middleSlider.value);
            settings.HighFrequencyThreshold = Mathf.Round(_highSlider.value);

            JsonConverter.WriteSettings(settings);
            SetStatus("Settings saved.");

            _saveText.enabled = true;
            Invoke(nameof(DisableSaveText), 1.5f);
        }

        private void ConfigureSliderRanges()
        {
            _volumeSlider.minValue = 0.001f;
            _volumeSlider.maxValue = 0.1f;

            _lowSlider.minValue = 30f;
            _lowSlider.maxValue = 500f;

            _middleSlider.minValue = 120f;
            _middleSlider.maxValue = 1800f;

            _highSlider.minValue = 300f;
            _highSlider.maxValue = 3000f;
        }

        private void HookSliderEvents()
        {
            _volumeSlider.onValueChanged.AddListener(_ => OnSliderChanged(_volumeSlider));
            _lowSlider.onValueChanged.AddListener(_ => OnSliderChanged(_lowSlider));
            _middleSlider.onValueChanged.AddListener(_ => OnSliderChanged(_middleSlider));
            _highSlider.onValueChanged.AddListener(_ => OnSliderChanged(_highSlider));
        }

        private void OnSliderChanged(Slider changedSlider)
        {
            if (_isSyncingUi) return;

            if (changedSlider != _volumeSlider)
            {
                EnforceFrequencyOrder(changedSlider);
            }

            UpdateLabels();
            SyncInputsFromSliders(false);
        }

        private void GetSettings()
        {
            var settings = JsonConverter.GetAppSettings();

            _isSyncingUi = true;
            _volumeSlider.value = Mathf.Clamp(settings.VolumeThreshold, _volumeSlider.minValue, _volumeSlider.maxValue);
            _lowSlider.value = Mathf.Clamp(settings.LowFrequencyThreshold, _lowSlider.minValue, _lowSlider.maxValue);
            _middleSlider.value = Mathf.Clamp(settings.MiddleFrequencyThreshold, _middleSlider.minValue, _middleSlider.maxValue);
            _highSlider.value = Mathf.Clamp(settings.HighFrequencyThreshold, _highSlider.minValue, _highSlider.maxValue);
            EnforceFrequencyOrder(_middleSlider);
            _isSyncingUi = false;
        }

        private void UpdateLabels()
        {
            _volumeText.text = $"Noise threshold: {_volumeSlider.value:0.000}";
            _lowText.text = $"Quiet sound: {Mathf.Round(_lowSlider.value)} Hz";
            _middleText.text = $"Comfort / fly split: {Mathf.Round(_middleSlider.value)} Hz";
            _highText.text = $"Loud sound: {Mathf.Round(_highSlider.value)} Hz";
        }

        private void SyncInputsFromSliders(bool force)
        {
            if (!_uiBuilt) return;

            SyncInput(_volumeSlider, _volumeInput, "0.000", force);
            SyncInput(_lowSlider, _lowInput, "0", force);
            SyncInput(_middleSlider, _middleInput, "0", force);
            SyncInput(_highSlider, _highInput, "0", force);
        }

        private static void SyncInput(Slider slider, TMP_InputField input, string format, bool force)
        {
            if (input == null) return;
            if (!force && input.isFocused) return;

            input.SetTextWithoutNotify(slider.value.ToString(format, CultureInfo.InvariantCulture));
        }

        private void StartMicrophoneMonitor()
        {
            var microphones = Microphone.devices;

            if (microphones.Length == 0)
            {
                SetStatus("Microphone not found.");
                return;
            }

            _microphoneName = microphones[0];
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.mute = true;
            _audioSource.loop = true;

            if (Microphone.IsRecording(_microphoneName))
            {
                Microphone.End(_microphoneName);
            }

            _audioSource.clip = Microphone.Start(_microphoneName, true, 1, AudioSettings.outputSampleRate);
            var timeoutAt = Time.realtimeSinceStartup + MicrophoneStartupTimeout;

            while (!(Microphone.GetPosition(null) > 0))
            {
                if (Time.realtimeSinceStartup >= timeoutAt)
                {
                    SetStatus("Microphone startup timed out.");
                    return;
                }
            }

            _audioSource.Play();
            SetStatus("Speak into the mic to see live values.");
        }

        private void UpdateLiveMonitor()
        {
            if (_audioSource == null || _audioSource.clip == null || !_audioSource.isPlaying)
            {
                if (_monitorText != null)
                {
                    _monitorText.text = "Current mic: not available";
                }

                return;
            }

            if (TryReadCurrentFrequencySample(out var frequency, out var level))
            {
                _liveFrequency = frequency;
                _liveLevel = level;
            }

            if (_monitorText != null)
            {
                _monitorText.text =
                    $"Current mic: {Mathf.Round(_liveFrequency)} Hz | level {_liveLevel:0.0000} | threshold {_volumeSlider.value:0.000}";
            }
        }

        private bool TryReadCurrentFrequencySample(out float detectedFrequency, out float detectedLevel)
        {
            detectedFrequency = 0f;
            detectedLevel = 0f;

            if (_audioSource == null || _audioSource.clip == null || !_audioSource.isPlaying)
            {
                return false;
            }

            _audioSource.GetSpectrumData(_spectrumData, 0, FFTWindow.BlackmanHarris);

            var frequencyStep = _audioSource.clip.frequency / (float)SampleSize;
            var minIndex = Mathf.Clamp(Mathf.FloorToInt(MinDetectableFrequency * CorrectingValue / frequencyStep), 0, SampleSize - 1);
            var maxIndex = Mathf.Clamp(Mathf.FloorToInt(MaxDetectableFrequency * CorrectingValue / frequencyStep), minIndex, SampleSize - 1);

            var amplitudeSum = 0f;
            var weightedFrequencySum = 0f;
            var peakAmplitude = 0f;
            var peakFrequency = 0f;

            for (var i = minIndex; i <= maxIndex; i++)
            {
                var amplitude = _spectrumData[i];
                if (amplitude <= 0f) continue;

                var frequency = i * frequencyStep / CorrectingValue;
                amplitudeSum += amplitude;
                weightedFrequencySum += amplitude * frequency;

                if (amplitude > peakAmplitude)
                {
                    peakAmplitude = amplitude;
                    peakFrequency = frequency;
                }
            }

            if (amplitudeSum <= MinimumCaptureLevel)
            {
                return false;
            }

            var centroidFrequency = weightedFrequencySum / amplitudeSum;
            detectedFrequency = peakAmplitude > 0f
                ? Mathf.Lerp(peakFrequency, centroidFrequency, 0.35f)
                : centroidFrequency;
            detectedLevel = amplitudeSum;
            return true;
        }

        private void BeginCalibration(CalibrationTarget target)
        {
            if (_audioSource == null || _audioSource.clip == null)
            {
                SetStatus("Microphone is not ready.");
                return;
            }

            if (_calibrationCoroutine != null)
            {
                SetStatus("Calibration is already running.");
                return;
            }

            _calibrationCoroutine = StartCoroutine(CalibrationRoutine(target));
        }

        private IEnumerator CalibrationRoutine(CalibrationTarget target)
        {
            var targetLabel = target switch
            {
                CalibrationTarget.Quiet => "quiet",
                CalibrationTarget.Comfortable => "comfort",
                _ => "loud"
            };

            SetStatus($"Recording {targetLabel} sound for {CalibrationDuration:0.#} sec...");

            var capturedSamples = new List<(float Frequency, float Level)>();
            var endTime = Time.unscaledTime + CalibrationDuration;

            while (Time.unscaledTime < endTime)
            {
                if (TryReadCurrentFrequencySample(out var frequency, out var level))
                {
                    capturedSamples.Add((frequency, level));
                }

                yield return null;
            }

            if (capturedSamples.Count < 8)
            {
                SetStatus("Not enough sound detected. Try again with a steadier voice.");
                _calibrationCoroutine = null;
                yield break;
            }

            var strongestSamples = capturedSamples
                .OrderByDescending(sample => sample.Level)
                .Take(Mathf.Max(8, capturedSamples.Count / 2))
                .ToList();

            var recommendedFrequency = strongestSamples
                .OrderBy(sample => sample.Frequency)
                .ElementAt(strongestSamples.Count / 2)
                .Frequency;

            ApplyCalibration(target, recommendedFrequency);
            SetStatus($"{targetLabel} sound recorded: {Mathf.Round(recommendedFrequency)} Hz");

            yield return new WaitForSecondsRealtime(CalibrationCooldown);
            _calibrationCoroutine = null;
        }

        private void ApplyCalibration(CalibrationTarget target, float frequency)
        {
            _isSyncingUi = true;

            switch (target)
            {
                case CalibrationTarget.Quiet:
                    _lowSlider.value = Mathf.Clamp(frequency, _lowSlider.minValue, _lowSlider.maxValue);
                    break;
                case CalibrationTarget.Comfortable:
                    _middleSlider.value = Mathf.Clamp(frequency, _middleSlider.minValue, _middleSlider.maxValue);
                    break;
                case CalibrationTarget.Loud:
                    _highSlider.value = Mathf.Clamp(frequency, _highSlider.minValue, _highSlider.maxValue);
                    break;
            }

            EnforceFrequencyOrder(GetSliderForTarget(target));
            _isSyncingUi = false;

            UpdateLabels();
            SyncInputsFromSliders(true);
        }

        private Slider GetSliderForTarget(CalibrationTarget target)
        {
            return target switch
            {
                CalibrationTarget.Quiet => _lowSlider,
                CalibrationTarget.Comfortable => _middleSlider,
                _ => _highSlider
            };
        }

        private void EnforceFrequencyOrder(Slider changedSlider)
        {
            var low = _lowSlider.value;
            var middle = _middleSlider.value;
            var high = _highSlider.value;

            if (changedSlider == _lowSlider)
            {
                middle = Mathf.Max(middle, low + FrequencyGap);
                high = Mathf.Max(high, middle + FrequencyGap);
            }
            else if (changedSlider == _middleSlider)
            {
                low = Mathf.Min(low, middle - FrequencyGap);
                high = Mathf.Max(high, middle + FrequencyGap);
            }
            else if (changedSlider == _highSlider)
            {
                middle = Mathf.Min(middle, high - FrequencyGap);
                low = Mathf.Min(low, middle - FrequencyGap);
            }

            low = Mathf.Clamp(low, _lowSlider.minValue, _lowSlider.maxValue);
            middle = Mathf.Clamp(middle, Mathf.Max(_middleSlider.minValue, low + FrequencyGap), _middleSlider.maxValue);
            high = Mathf.Clamp(high, Mathf.Max(_highSlider.minValue, middle + FrequencyGap), _highSlider.maxValue);

            _lowSlider.SetValueWithoutNotify(low);
            _middleSlider.SetValueWithoutNotify(middle);
            _highSlider.SetValueWithoutNotify(high);
        }

        private void BuildRuntimeUi()
        {
            if (_uiBuilt) return;

            var canvasRect = GetComponent<RectTransform>();
            var fontAsset = _volumeText.font;

            _monitorText = CreateLabel("MicMonitorText", canvasRect, new Vector2(0f, 165f), new Vector2(640f, 28f), fontAsset, 20);
            _statusText = CreateLabel("CalibrationStatusText", canvasRect, new Vector2(0f, -115f), new Vector2(640f, 28f), fontAsset, 18);

            _volumeInput = CreateNumericInput("VolumeInput", canvasRect, GetInputPosition(_volumeSlider), fontAsset);
            _lowInput = CreateNumericInput("QuietInput", canvasRect, GetInputPosition(_lowSlider), fontAsset);
            _middleInput = CreateNumericInput("ComfortInput", canvasRect, GetInputPosition(_middleSlider), fontAsset);
            _highInput = CreateNumericInput("LoudInput", canvasRect, GetInputPosition(_highSlider), fontAsset);

            _inputsBySlider[_volumeSlider] = _volumeInput;
            _inputsBySlider[_lowSlider] = _lowInput;
            _inputsBySlider[_middleSlider] = _middleInput;
            _inputsBySlider[_highSlider] = _highInput;

            BindInput(_volumeInput, _volumeSlider, false);
            BindInput(_lowInput, _lowSlider, true);
            BindInput(_middleInput, _middleSlider, true);
            BindInput(_highInput, _highSlider, true);

            CreateActionButton("QuietRecordButton", canvasRect, new Vector2(-240f, -155f), "Record quiet", () => BeginCalibration(CalibrationTarget.Quiet), fontAsset);
            CreateActionButton("ComfortRecordButton", canvasRect, new Vector2(0f, -155f), "Record comfort", () => BeginCalibration(CalibrationTarget.Comfortable), fontAsset);
            CreateActionButton("LoudRecordButton", canvasRect, new Vector2(240f, -155f), "Record loud", () => BeginCalibration(CalibrationTarget.Loud), fontAsset);

            CreateLabel("CalibrationHintText", canvasRect, new Vector2(0f, -185f), new Vector2(700f, 24f), fontAsset, 16)
                .text = "Use Record buttons for your quiet, comfortable and loud voice. You can still edit values manually.";

            _uiBuilt = true;
        }

        private static Vector2 GetInputPosition(Component slider)
        {
            var rect = slider.GetComponent<RectTransform>();
            return rect.anchoredPosition + new Vector2(125f, 0f);
        }

        private void BindInput(TMP_InputField input, Slider slider, bool enforceOrder)
        {
            input.onEndEdit.AddListener(value =>
            {
                if (!TryParseFloat(value, out var parsedValue))
                {
                    SyncInput(slider, input, slider == _volumeSlider ? "0.000" : "0", true);
                    return;
                }

                _isSyncingUi = true;
                slider.value = Mathf.Clamp(parsedValue, slider.minValue, slider.maxValue);

                if (enforceOrder)
                {
                    EnforceFrequencyOrder(slider);
                }

                _isSyncingUi = false;

                UpdateLabels();
                SyncInputsFromSliders(true);
            });
        }

        private static bool TryParseFloat(string value, out float parsedValue)
        {
            var normalizedValue = value.Replace(',', '.');
            return float.TryParse(normalizedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue);
        }

        private TextMeshProUGUI CreateLabel(string objectName, RectTransform parent, Vector2 position, Vector2 size, TMP_FontAsset fontAsset, float fontSize)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);

            var rectTransform = labelObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = fontAsset;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.text = string.Empty;
            return label;
        }

        private TMP_InputField CreateNumericInput(string objectName, RectTransform parent, Vector2 position, TMP_FontAsset fontAsset)
        {
            var inputObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputObject.transform.SetParent(parent, false);

            var rectTransform = inputObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = new Vector2(88f, 30f);

            var background = inputObject.GetComponent<Image>();
            background.color = new Color(0.12f, 0.12f, 0.12f, 0.92f);

            var inputField = inputObject.GetComponent<TMP_InputField>();
            inputField.contentType = TMP_InputField.ContentType.DecimalNumber;
            inputField.lineType = TMP_InputField.LineType.SingleLine;

            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputObject.transform, false);
            var textAreaRect = textArea.GetComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(8f, 4f);
            textAreaRect.offsetMax = new Vector2(-8f, -4f);

            var placeholder = CreateInputText("Placeholder", textAreaRect, fontAsset, "0");
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);

            var text = CreateInputText("Text", textAreaRect, fontAsset, string.Empty);
            text.color = Color.white;

            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            return inputField;
        }

        private static TextMeshProUGUI CreateInputText(string objectName, RectTransform parent, TMP_FontAsset fontAsset, string text)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            var rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            var textComponent = textObject.GetComponent<TextMeshProUGUI>();
            textComponent.font = fontAsset;
            textComponent.fontSize = 20;
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.text = text;
            return textComponent;
        }

        private static void CreateActionButton(string objectName, RectTransform parent, Vector2 position, string title, UnityEngine.Events.UnityAction action, TMP_FontAsset fontAsset)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = new Vector2(180f, 30f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);

            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = fontAsset;
            text.fontSize = 18;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.text = title;
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }
        }

        private void DisableSaveText()
        {
            _saveText.enabled = false;
        }
    }
}
