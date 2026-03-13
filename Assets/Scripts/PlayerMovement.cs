using System;
using System.Diagnostics.CodeAnalysis;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;

[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
public class PlayerMovement : MonoBehaviour
{
    #region Public
    public float UserStamina
    {
        private get => _userStamina;
        set
        {
            _userStamina = value switch
            {
                < 0 => 0,
                > 100 => 100,
                _ => value
            };
        }
    }
    #endregion

    #region Private

    [SerializeField] private float _forwardSpeedMultiplier;
    [SerializeField] private float _upwardSpeedMultiplier;
    [SerializeField] private float _gravityScale = 3f;
    [SerializeField] private float _staminaMultiplier = 0.001f;

    private MovementState _movementState;

    private AudioSource _audioSource;
    private Animator _animator;
    private Rigidbody2D _playerRigidBody;
    private BoxCollider2D _collider;

#pragma warning disable CS0649
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private LayerMask _ground;
    [SerializeField] private TMP_Text _staminaText;
    [SerializeField] private float _minStaminaConsumption = 1f;
#pragma warning restore CS0649

    private const int SampleSize = 1024;
    private const float MaxSpeed = 10f;
    private const float FanStrength = 5f;
    private const float CorrectingValue = 2f;
    private const float ReferenceFrameRate = 60f;
    private const float MaxDeltaTime = 0.05f;
    private const float MicrophoneStartupTimeout = 1f;

    private readonly float[] _spectrumData = new float[SampleSize];
    private string _microphoneName;
    private float _lowFrequencyThreshold;
    private float _middleFrequencyThreshold;
    private float _highFrequencyThreshold;
    private float _volumeThreshold;
    private float _inputSmoothing;
    private float _smoothedLowIntensity;
    private float _smoothedHighIntensity;
    private float _userStamina;
    private bool _hasAudioSample;
    private bool _isInFanZone;
    private Vector2 _currentPlayerMovement;

    private bool Grounded =>
        Physics2D.BoxCast(_collider.bounds.center, _collider.bounds.size, 0f, Vector2.down, .2f, _ground);

    #endregion

    #region UnityMethods

    private void Awake()
    {
        var settings = JsonConverter.GetAppSettings();
        _volumeThreshold = settings.VolumeThreshold;
        _lowFrequencyThreshold = settings.LowFrequencyThreshold;
        _middleFrequencyThreshold = settings.MiddleFrequencyThreshold;
        _highFrequencyThreshold = settings.HighFrequencyThreshold;
        _inputSmoothing = settings.InputSmoothing > 0f ? settings.InputSmoothing : 12f;

        if (_forwardSpeedMultiplier <= 0f) _forwardSpeedMultiplier = 70f;
        if (_upwardSpeedMultiplier <= 0f) _upwardSpeedMultiplier = 70f;

        UserStamina = 100f;
        _movementState = MovementState.Idle;
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.Stop();
        _animator = GetComponent<Animator>();
        _collider = GetComponent<BoxCollider2D>();

        _playerRigidBody = GetComponent<Rigidbody2D>();
        _playerRigidBody.gravityScale = _gravityScale;
        _playerRigidBody.constraints = RigidbodyConstraints2D.FreezeRotation;

        var microphones = Microphone.devices;

        if (microphones.Length == 0) throw new Exception("Unable to connect to microphone");

        _microphoneName = microphones[0];
        _staminaText.text = _microphoneName;

        if (_audioMixer == null) throw new NullReferenceException("Unable to find audio mixer");

        try
        {
            _audioSource.outputAudioMixerGroup = _audioMixer.FindMatchingGroups("MicrophoneGroup")[0];
        }
        catch
        {
            throw new NullReferenceException("Unable to find audio mixer group");
        }
    }

    private void Start()
    {
        StartMicrophoneRecord();
    }

    private void Update()
    {
        if (_playerRigidBody.bodyType == RigidbodyType2D.Static) return;

        RefreshAudioState(Time.deltaTime);
        _currentPlayerMovement = CalculatePlayerMovement();
        SetAnimation(_currentPlayerMovement);
    }

    private void FixedUpdate()
    {
        if (_playerRigidBody.bodyType == RigidbodyType2D.Static) return;

        _currentPlayerMovement = CalculatePlayerMovement();
        _playerRigidBody.velocity = _currentPlayerMovement;
        CalculateStamina(_currentPlayerMovement, Time.fixedDeltaTime);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus) return;

        Microphone.End(_microphoneName);
        _audioSource.Stop();
        _hasAudioSample = false;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            StartMicrophoneRecord();
        }
    }

    private void OnTriggerEnter2D(Collider2D triggerCollider)
    {
        if (triggerCollider.CompareTag("Fan"))
        {
            _isInFanZone = true;
        }
    }

    private void OnTriggerExit2D(Collider2D triggerCollider)
    {
        if (triggerCollider.CompareTag("Fan"))
        {
            _isInFanZone = false;
        }
    }

    private void OnDisable()
    {
        Microphone.End(_microphoneName);
        _audioSource.Stop();
    }

    #endregion

    #region ServiceFunctions

    private float CalculateIntensity(float minFrequency, float maxFrequency)
    {
        var intensity = 0f;
        var frequencyStep = _audioSource.clip.frequency / (float)SampleSize;
        var minIndex = Mathf.Clamp(Mathf.FloorToInt(minFrequency * CorrectingValue / frequencyStep), 0, SampleSize - 1);
        var maxIndex = Mathf.Clamp(Mathf.FloorToInt(maxFrequency * CorrectingValue / frequencyStep), minIndex, SampleSize - 1);

        for (var i = minIndex; i <= maxIndex; i++)
        {
            intensity += _spectrumData[i];
        }

        return intensity;
    }

    private void RefreshAudioState(float deltaTime)
    {
        if (_audioSource.clip == null) return;

        _audioSource.GetSpectrumData(_spectrumData, 0, FFTWindow.BlackmanHarris);
        var lowFrequencyIntensity = CalculateIntensity(_lowFrequencyThreshold, _middleFrequencyThreshold);
        var highFrequencyIntensity = CalculateIntensity(_middleFrequencyThreshold, _highFrequencyThreshold);

        if (!_hasAudioSample)
        {
            _smoothedLowIntensity = lowFrequencyIntensity;
            _smoothedHighIntensity = highFrequencyIntensity;
            _hasAudioSample = true;
            return;
        }

        var blendFactor = GetBlendFactor(_inputSmoothing, deltaTime);
        _smoothedLowIntensity = Mathf.Lerp(_smoothedLowIntensity, lowFrequencyIntensity, blendFactor);
        _smoothedHighIntensity = Mathf.Lerp(_smoothedHighIntensity, highFrequencyIntensity, blendFactor);
    }

    private static float GetBlendFactor(float speed, float deltaTime)
    {
        var safeDeltaTime = Mathf.Min(deltaTime, MaxDeltaTime);
        return 1f - Mathf.Exp(-speed * safeDeltaTime);
    }

    private MovementState GetMovementState(Vector2 movement)
    {
        var movementState = movement.x > 0 ? MovementState.Running : MovementState.Idle;

        if (!Grounded) movementState = MovementState.Flying;

        if (movement is { x: 0f, y: < -.1f } || (UserStamina == 0 && movement.y != 0))
            movementState = MovementState.Falling;

        return movementState;
    }

    private void SetAnimation(Vector2 movement)
    {
        _movementState = GetMovementState(movement);
        _animator.SetInteger(nameof(MovementState), (int)_movementState);
    }

    private Vector2 CalculatePlayerMovement()
    {
        var playerMovement = Vector2.zero;

        var horizontalIntensity = _smoothedLowIntensity >= _volumeThreshold ? _smoothedLowIntensity : 0f;
        var verticalIntensity = _smoothedHighIntensity >= _volumeThreshold ? _smoothedHighIntensity : 0f;

        if ((horizontalIntensity > 0f || verticalIntensity > 0f) && UserStamina > 0)
        {
            playerMovement.x = horizontalIntensity * _forwardSpeedMultiplier;
            playerMovement.y = verticalIntensity * _upwardSpeedMultiplier;
        }
        else
        {
            playerMovement.x = 0f;
            playerMovement.y = _playerRigidBody.velocity.y switch
            {
                < 0 => _playerRigidBody.velocity.y,
                > 0 => -.1f,
                _ => 0
            };
        }

        playerMovement.x = Mathf.Min(playerMovement.x, MaxSpeed);
        playerMovement.y = Mathf.Min(playerMovement.y, MaxSpeed);
        playerMovement.x = _isInFanZone ? playerMovement.x - FanStrength : playerMovement.x;

        return playerMovement;
    }

    private void CalculateStamina(Vector2 playerMovement, float deltaTime)
    {
        _movementState = GetMovementState(playerMovement);

        var timeScale = Mathf.Min(deltaTime, MaxDeltaTime) * ReferenceFrameRate;

        switch (_movementState)
        {
            case MovementState.Flying:
            {
                var deltaStamina = (playerMovement.y * playerMovement.x) * _staminaMultiplier;
                UserStamina -= Mathf.Max(deltaStamina, _minStaminaConsumption) * timeScale;
                break;
            }
            case MovementState.Idle or MovementState.Running:
            {
                if (UserStamina < 100) UserStamina += 25f * timeScale;
                break;
            }
        }

        _staminaText.text = $"Stamina: {math.round(UserStamina)}";
    }

    private void StartMicrophoneRecord()
    {
        _hasAudioSample = false;

        if (Microphone.IsRecording(_microphoneName))
        {
            Microphone.End(_microphoneName);
        }

        _audioSource.Stop();
        _audioSource.clip = Microphone.Start(_microphoneName, true, 1, AudioSettings.outputSampleRate);
        _audioSource.loop = true;
        var timeoutAt = Time.realtimeSinceStartup + MicrophoneStartupTimeout;

        while (!(Microphone.GetPosition(null) > 0))
        {
            if (Time.realtimeSinceStartup >= timeoutAt)
            {
                throw new TimeoutException("Unable to start microphone recording.");
            }
        }

        _audioSource.Play();
    }

    #endregion
}