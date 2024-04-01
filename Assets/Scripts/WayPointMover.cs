using System.Linq;
using UnityEngine;

namespace Assets.Scripts
{
    public class WayPointMover : MonoBehaviour
    {
#pragma warning disable CS0649
        [SerializeField] private GameObject[] _wayPoints;
#pragma warning restore CS0649

        private int _index = 0;

        [SerializeField] private float _speed = 2f;

        private void Update()
        {
            if (!_wayPoints.Any()) return;
            if (Vector2.Distance(_wayPoints[_index].transform.position, transform.position) < .1f)
            {
                _index = _index + 1 >= _wayPoints.Length ? 0 : _index + 1;
            }

            transform.position = Vector2.MoveTowards(transform.position, _wayPoints[_index].transform.position, Time.deltaTime * _speed);
        }
    }
}
