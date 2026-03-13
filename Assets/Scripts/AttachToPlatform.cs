using UnityEngine;

public class AttachToPlatform : MonoBehaviour
{
    private bool _isPlayerExiting;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.name == "Player")
        {
            collision.gameObject.transform.SetParent(transform);
        }
    }

    private void Update()
    {
        if (_isPlayerExiting)
        {
            _isPlayerExiting = false;
            transform.DetachChildren();
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.name == "Player")
        {
            _isPlayerExiting = true;
        }
    }
}