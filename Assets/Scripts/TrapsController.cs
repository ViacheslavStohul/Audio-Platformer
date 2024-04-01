using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts
{
    public class PlayerLife : MonoBehaviour
    {
        private Animator _animator;
        private Rigidbody2D _rigidBody;

#pragma warning disable CS0649
        [SerializeField] private AudioSource _deathSoundEffect;
        [SerializeField] private AudioSource _backgroundMusic;
#pragma warning restore CS0649

        private void Start()
        {
            _animator = GetComponent<Animator>();
            _rigidBody = GetComponent<Rigidbody2D>();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!collision.gameObject.CompareTag("Trap")) return;
            CommonCollisionHandling();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!collision.gameObject.CompareTag("Trap")) return;
            CommonCollisionHandling();
        }

        private void CommonCollisionHandling()
        {
            if (_rigidBody.bodyType == RigidbodyType2D.Static) return;
            _backgroundMusic.Stop();
            _deathSoundEffect.Play();
            Die();
        }


        private void Die()
        {
            JsonConverter.AddDeath();
            _rigidBody.bodyType = RigidbodyType2D.Static;
            _animator.SetTrigger("DeathTrigger");
        }

        private void RestartLevelAfterDeath()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}