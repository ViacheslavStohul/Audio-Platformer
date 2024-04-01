using UnityEngine;

namespace Assets.Scripts
{
    public class PortalEnter : MonoBehaviour
    {
        private Animator _animator;
        private Rigidbody2D _rigidbody;

#pragma warning disable CS0649
        [SerializeField] private AudioSource _passSound;
        [SerializeField] private AudioSource _backgroundMusic;
        [SerializeField] private Canvas _canvas;
#pragma warning restore CS0649

        private void Start()
        {
            _animator = GetComponent<Animator>();
            _rigidbody = GetComponent<Rigidbody2D>();
            _canvas.gameObject.SetActive(false);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!collision.gameObject.CompareTag("Portal")) return;

            _animator.SetInteger(nameof(MovementState), (int)MovementState.Idle);
            _backgroundMusic.Stop();
            _rigidbody.bodyType = RigidbodyType2D.Static;

            Invoke(nameof(SetMusicAndAnimation), 1f);
        }

        private void SetMusicAndAnimation()
        {
            _passSound.Play();
            _animator.SetTrigger("CompleteTrigger");
            _canvas.gameObject.SetActive(true);
        }
    }
}
