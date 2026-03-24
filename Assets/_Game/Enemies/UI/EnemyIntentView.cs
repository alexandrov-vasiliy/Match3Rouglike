using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// Passive View — только отрисовка намерения врага. Заглушка (полная реализация позже).
    /// </summary>
    public class EnemyIntentView : MonoBehaviour
    {
        [SerializeField] private Text textLabel;

        private void Awake()
        {
            if (textLabel == null) textLabel = GetComponentInChildren<Text>();
        }

        public void SetIntent(string text)
        {
            if (textLabel != null)
                textLabel.text = string.IsNullOrEmpty(text) ? "?" : text;
        }
    }
}
