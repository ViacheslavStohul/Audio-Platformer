using UnityEngine;

namespace Assets.Scripts
{
    public class CameraController : MonoBehaviour
    {
        #pragma warning disable CS0649
        [SerializeField] private Transform _player;
        #pragma warning restore CS0649

        private void FixedUpdate()
        {
            transform.position = new Vector3(_player.position.x, _player.position.y, transform.position.z);
        }
    }
}
