using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// Passive View — только отрисовка крови (энергии).
    /// </summary>
    public class PlayerBloodView : MonoBehaviour
    {
        [SerializeField] private Text textLabel;

        private void Awake()
        {
            if (textLabel == null) textLabel = GetComponentInChildren<Text>();
        }

        public void SetBlood(int value)
        {
            if (textLabel != null)
                textLabel.text = $"Blood {value}";
        }
    }
}
