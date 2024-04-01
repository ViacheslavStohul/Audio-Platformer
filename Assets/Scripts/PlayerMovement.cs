using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using TMPro;

namespace Assets.Scripts
{
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
    public class PlayerMovement : MonoBehaviour
    {
        #region Public
        public float UserStamina {
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
        private const float MaxSpeed = 10;
        private const float FanStrength = 5;
        private float[] _spectrumData;
        private string _microphoneName;
        private float _lowFrequencyThreshold;
        private float _middleFrequencyThreshold;
        private float _highFrequencyThreshold;
        private float _volumeThreshold;
        private float _userStamina;

        private const float CorrectingValue = 2f;
        private bool _isInFanZone = false;

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

            _forwardSpeedMultiplier = 70;
            _upwardSpeedMultiplier = 70;
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

            _spectrumData = new float[SampleSize];
        }

        private void Start()
        {
            StartMicrophoneRecord();
        }

        private void Update()
        {
            if (_playerRigidBody.bodyType == RigidbodyType2D.Static) return;

            var playerMovement = CalculatePlayerMovement();

            SetAnimation(playerMovement);

            CalculateStamina(playerMovement);

            _playerRigidBody.velocity = playerMovement;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                Microphone.End(_microphoneName);
                _audioSource.Stop();
            }
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
        }

        #endregion

        #region ServiceFunctions

        private float CalculateIntensity(float minFrequency, float maxFrequency)
        {
            var intensity = 0f;
            var minIndex = Mathf.FloorToInt(minFrequency * CorrectingValue / (_audioSource.clip.frequency / (float)SampleSize));
            var maxIndex = Mathf.FloorToInt(maxFrequency * CorrectingValue / (_audioSource.clip.frequency / (float)SampleSize));

            for (var i = minIndex; i <= maxIndex; i++) intensity += _spectrumData[i];

            return intensity;
        }

        private void SetAnimation(Vector2 movement)
        {
            _movementState = movement.x > 0 ? MovementState.Running : MovementState.Idle;

            if (!Grounded) _movementState = MovementState.Flying;

            if (movement is { x: 0f, y: < -.1f } || (UserStamina == 0 && movement.y != 0))
                _movementState = MovementState.Falling;

            _animator.SetInteger(nameof(MovementState), (int)_movementState);
        }

        private Vector2 CalculatePlayerMovement()
        {
            _audioSource.GetSpectrumData(_spectrumData, 0, FFTWindow.BlackmanHarris);
            var lowFrequencyIntensity = CalculateIntensity(_lowFrequencyThreshold, _middleFrequencyThreshold);
            var highFrequencyIntensity = CalculateIntensity(_middleFrequencyThreshold, _highFrequencyThreshold);

            var playerMovement = Vector2.zero;

            if ((lowFrequencyIntensity > _volumeThreshold || highFrequencyIntensity > _volumeThreshold) &&
                UserStamina > 0)
            {
                playerMovement.x = lowFrequencyIntensity * _forwardSpeedMultiplier;
                playerMovement.y = highFrequencyIntensity * _upwardSpeedMultiplier;
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


            playerMovement.x = playerMovement.x > MaxSpeed ? MaxSpeed : playerMovement.x;
            playerMovement.y = playerMovement.y > MaxSpeed ? MaxSpeed : playerMovement.y;
            playerMovement.x = _isInFanZone ? playerMovement.x - FanStrength: playerMovement.x;

            return playerMovement;
        }

        private void CalculateStamina(Vector2 playerMovement)
        {
            switch (_movementState)
            {
                case MovementState.Flying:
                {
                    var deltaStamina = (playerMovement.y * playerMovement.x) * _staminaMultiplier;
                    UserStamina -= deltaStamina > _minStaminaConsumption ? deltaStamina : _minStaminaConsumption;

                    break;
                }
                case MovementState.Idle or MovementState.Running:
                {
                    if (UserStamina < 100) UserStamina += 25;

                    break;
                }
            }

            _staminaText.text = $"Stamina: {math.round(UserStamina)}";
        }

        private void StartMicrophoneRecord()
        {
            _audioSource.clip = Microphone.Start(_microphoneName, true, 1, AudioSettings.outputSampleRate);
            _audioSource.loop = true;

            while (!(Microphone.GetPosition(null) > 0))
            {
            }

            _audioSource.Play();
        }

        #endregion
    }
}