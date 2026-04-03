using System;
using UnityEngine;

namespace Match3
{
    /// <summary>
    /// Данные одного уровня рана: музыка и префабы врагов по индексам точек спавна на сцене.
    /// </summary>
    [Serializable]
    public class LevelDefinition
    {
        public string DisplayName = "Level";

        public AudioClip Music;

        [Tooltip("По индексу соответствует BattleSpawnLayout.SpawnPoints; null пропускает слот.")]
        public EnemyDefinition[] EnemyPrefabsPerSpawnSlot;
    }
}