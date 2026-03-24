using System;
using UnityEngine;

namespace Match3
{
    /// <summary>
    /// Заглушка UIHandler — сохраняет публичный API для совместимости с GameManager, Board и т.д.
    /// Без UI Toolkit; позже можно заменить на Canvas/UGUI.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    public class UIHandler : MonoBehaviour
    {
        public enum CharacterAnimation
        {
            Match,
            Win,
            LowMove,
            Lose
        }

        public static UIHandler Instance { get; protected set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugMenuOpen => false;
        public Gem SelectedDebugGem => null;
#endif

        private void Awake()
        {
            Instance = this;
        }

        public void Init()
        {
        }

        public void Display(bool displayed)
        {
        }

        public void ShowEnd()
        {
            BattleProcessor battleProcessor = LevelData.Instance.BattleProcessor;
            if (battleProcessor != null)
            {
                BattleResult result = battleProcessor.CheckBattleEnd();
                if (result == BattleResult.Win)
                {
                    GameManager.Instance.WinTriggered();
                    return;
                }

                if (result == BattleResult.Lose)
                {
                    GameManager.Instance.LooseTriggered();
                    return;
                }
            }

            GameManager.Instance.LooseTriggered();
        }

        public void FadeIn(Action onFadeFinished)
        {
            onFadeFinished?.Invoke();
        }

        public void FadeOut(Action onFadeFinished)
        {
            onFadeFinished?.Invoke();
        }

        public void AddMatchEffect(Gem gem)
        {
        }

        public void AddCoin(Vector3 startPoint)
        {
        }

        public void UpdateTopBarData()
        {
        }

        public void CreateBottomBar()
        {
        }

        public void UpdateBottomBar()
        {
        }

        public void DeselectBonusItem()
        {
        }

        public void ShowShop(bool opened)
        {
        }

        public void ToggleSettingMenu(bool display)
        {
        }

        public void UpdateShopEntry()
        {
        }

        public void TriggerCharacterAnimation(CharacterAnimation animation)
        {
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void RegisterGemToDebug(Gem gem)
        {
        }

        public void ToggleDebugMenu()
        {
        }
#endif
    }
}
