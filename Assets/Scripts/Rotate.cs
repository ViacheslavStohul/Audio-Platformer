using UnityEngine;

namespace Assets.Scripts
{
    public class Rotate : MonoBehaviour
    {
        [SerializeField] private float _speed = 1f;

        void Update()
        {
            transform.Rotate(0, 0, 360 * _speed * Time.deltaTime);
        }
    }
}
