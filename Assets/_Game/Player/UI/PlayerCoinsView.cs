using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// Passive View — только отрисовка монет.
    /// </summary>
    public class PlayerCoinsView : MonoBehaviour
    {
        [SerializeField] private Text textLabel;

        private void Awake()
        {
            if (textLabel == null) textLabel = GetComponentInChildren<Text>();
        }

        public void SetCoins(int value)
        {
            if (textLabel != null)
                textLabel.text = $"🪙 {value}";
        }
    }
}
