using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// Passive View — только отрисовка брони.
    /// </summary>
    public class PlayerArmorView : MonoBehaviour
    {
        [SerializeField] private Text textLabel;

        private void Awake()
        {
            if (textLabel == null) textLabel = GetComponentInChildren<Text>();
        }

        public void SetArmor(int value)
        {
            if (textLabel != null)
                textLabel.text = value > 0 ? $"🛡 {value}" : "";
        }
    }
}
