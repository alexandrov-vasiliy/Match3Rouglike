using System;
using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    /// <summary>
    /// Данные уровня: боевая конфигурация (HP игрока, префабы врагов), визуал, музыка.
    /// При Awake() уведомляет GameManager о начале уровня.
    /// </summary>

    [Serializable]
    public class EnemyLevelPoint
    {
        public Transform point;
        public EnemyDefinition EnemyDefinition;
    }
    
    [DefaultExecutionOrder(12000)]
    public class LevelData : MonoBehaviour
    {
        public static LevelData Instance { get; private set; }

        public string LevelName = "Level";

        [Header("Battle")]
        public int PlayerMaxHealth = 100;
        public EnemyLevelPoint[] EnemyLevelPoints;

        [Header("Visuals")]
        public float BorderMargin = 0.3f;
        public SpriteRenderer Background;

        [Header("Audio")]
        public AudioClip Music;

        public Action OnBattleWin;
        public Action OnBattleLose;
        public Action<BattleState> OnBattleStateChanged;

        public BattleState BattleState { get; private set; }
        public BattleProcessor BattleProcessor { get; private set; }

        private List<EnemyDefinition> spawnedEnemies = new();

        private int startingScreenWidth;
        private int startingScreenHeight;

        private void Awake()
        {
            Instance = this;
            InitBattle();
            GameManager.Instance.StartLevel();
        }

        private void Start()
        {
            startingScreenWidth = Screen.width;
            startingScreenHeight = Screen.height;

            if (Background != null)
                Background.gameObject.SetActive(false);
        }
        
        private void InitBattle()
        {
            BattleState = new BattleState
            {
                PlayerHealth = new Health(PlayerMaxHealth)
            };

            BattleState.Energy = BattleState.MaxEnergy;

            if (EnemyLevelPoints != null)
            {
                foreach (var enemyLevel in EnemyLevelPoints)
                {
                    var enemyInstance = Instantiate(enemyLevel.EnemyDefinition, enemyLevel.point);
                    enemyInstance.InitBattle();
                    BattleState.Enemies.Add(enemyInstance);
                    spawnedEnemies.Add(enemyInstance);
                }
            }

            BattleProcessor = new BattleProcessor(BattleState);

            InitPlayerBattleUI();
        }

        private void InitPlayerBattleUI()
        {
            var playerStatsView = FindFirstObjectByType<PlayerStatsView>();
            if (playerStatsView != null)
                playerStatsView.Init(BattleState.PlayerHealth, BattleState);
        }

        public void GemMatched(Gem gem)
        {
            if (gem.Effect != null)
            {
                BattleProcessor.ProcessGemEffect(gem.Effect, 1);
                OnBattleStateChanged?.Invoke(BattleState);
            }
        }

        public void PlayerMoved()
        {
            StartCoroutine(BattleProcessor.OnPlayerMoved());
        }

        public void OnBoardSettled()
        {
            OnBattleStateChanged?.Invoke(BattleState);

            var result = BattleProcessor.CheckBattleEnd();

            if (result == BattleResult.Win)
            {
                GameManager.Instance.Board.ToggleInput(false);
                OnBattleWin?.Invoke();
            }
            else if (result == BattleResult.Lose)
            {
                GameManager.Instance.Board.ToggleInput(false);
                OnBattleLose?.Invoke();
            }
        }

        public void DarkenBackground(bool darken)
        {
            if (Background == null)
                return;

            Background.gameObject.SetActive(darken);
        }

        private void OnDestroy()
        {
            foreach (var enemy in spawnedEnemies)
            {
                if (enemy != null)
                    Destroy(enemy.gameObject);
            }
        }
    }
}
