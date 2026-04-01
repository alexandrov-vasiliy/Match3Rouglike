using TMPro;
using UnityEngine;

namespace Match3
{
    public class PlayerEnergyView : MonoBehaviour
    {
        public TMP_Text energy;

        public void Update()
        {
            energy.text = $"{LevelData.Instance.BattleState.Energy} / {LevelData.Instance.BattleState.MaxEnergy}";
        }

    }
}