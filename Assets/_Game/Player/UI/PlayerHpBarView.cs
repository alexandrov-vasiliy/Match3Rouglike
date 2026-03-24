using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// Passive View — только отрисовка HP. Не подписывается на события, не знает о Health.
    /// </summary>
    public class PlayerHpBarView : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private Text textLabel;

        private void Awake()
        {
            if (slider == null) slider = GetComponentInChildren<Slider>();
            if (textLabel == null) textLabel = GetComponentInChildren<Text>();
        }

        public void SetHp(int current, int max)
        {
            if (slider != null)
            {
                slider.minValue = 0;
                slider.maxValue = max;
                slider.value = current;
            }

            if (textLabel != null)
                textLabel.text = $"{current}/{max}";
        }
    }
}
