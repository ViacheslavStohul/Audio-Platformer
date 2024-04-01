using UnityEngine;

namespace Assets.Scripts
{
    public class ItemCollector : MonoBehaviour
    {
        private int _melons = 0;

#pragma warning disable CS0649
        [SerializeField] private AudioSource _collectionSound;
        [SerializeField] private PlayerMovement _playerMovement;
#pragma warning restore CS0649

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!collision.gameObject.CompareTag("Melon")) return;

            JsonConverter.AddCollectedItem();
            _playerMovement.UserStamina = 100;
            _collectionSound.Play();
            _melons++;
            Destroy(collision.gameObject);
        }
    }
}
