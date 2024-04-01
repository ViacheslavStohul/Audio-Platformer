using TMPro;
using UnityEngine;

namespace Assets.Scripts
{
    public class StatisticsPageController : MonoBehaviour
    {
#pragma warning disable CS0649
        [SerializeField] private TMP_Text _deathsText;
        [SerializeField] private TMP_Text _itemsText;
#pragma warning restore CS0649

        private void Start()
        {
            var statistics = JsonConverter.GetStatistics();

            _deathsText.text = $"Deaths: {statistics.Deaths.ToString()}";
            _itemsText.text = $"Melons: {statistics.ItemsCollected.ToString()}";
        }
    }
}
