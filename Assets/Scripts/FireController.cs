using UnityEngine;

namespace Assets.Scripts
{
    public class FireController : MonoBehaviour
    {
        private Animator _animator;
        private CapsuleCollider2D _collider;

        private FireState _fireState = FireState.Fire;

        private void Start()
        {
            _animator = gameObject.GetComponent<Animator>();
            _collider = gameObject.GetComponent<CapsuleCollider2D>();

            Invoke(nameof(SetFireState), 3f);
        }

        private void SetFireState()
        {
            _fireState = _fireState == FireState.Fire ? FireState.NoFire : FireState.Fire;
            _collider.enabled = _fireState == FireState.Fire;
            _animator.SetInteger(nameof(FireState), (int)_fireState);

            Invoke(nameof(SetFireState), 3f);
        }
    }
}
