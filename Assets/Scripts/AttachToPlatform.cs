using UnityEngine;

namespace Assets.Scripts
{
    public class AttachToPlatform : MonoBehaviour
    {
        private bool isPlayerExiting;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.gameObject.name == "Player")
            {
                collision.gameObject.transform.SetParent(transform);
            }
        }

        private void Update()
        {
            if (isPlayerExiting)
            {
                isPlayerExiting = false;
                transform.DetachChildren();
            }
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (collision.gameObject.name == "Player")
            {
                isPlayerExiting = true;
            }
        }
    }
}
