using UnityEngine;

namespace Match3
{
    /// <summary>
    /// Фиксированные точки спавна врагов на боевой сцене (общие для всех уровней рана).
    /// </summary>
    public class BattleSpawnLayout : MonoBehaviour
    {
        public Transform[] SpawnPoints;
    }
}