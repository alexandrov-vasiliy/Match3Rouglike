using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// UIHandler — совместимость с GameManager, Board; затемнение экрана между сегментами боя.
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

        [Serializable]
        public class ScreenFadeSettings
        {
            public float FadeOutSeconds = 0.35f;
            public float FadeInSeconds = 0.35f;
            public AnimationCurve FadeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            public Color FadeColor = Color.black;
        }

        public static UIHandler Instance { get; protected set; }

        [SerializeField] private CanvasGroup fadeCanvasGroup;
        [SerializeField] private Image fadeImage;
        [SerializeField] private ScreenFadeSettings screenFadeSettings = new();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugMenuOpen => false;
        public Gem SelectedDebugGem => null;
#endif

        private Coroutine activeFadeRoutine;

        private void Awake()
        {
            Instance = this;

            if (fadeCanvasGroup != null)
                fadeCanvasGroup.alpha = 0f;

            if (fadeImage != null && screenFadeSettings != null)
                fadeImage.color = screenFadeSettings.FadeColor;
        }

        public void Init()
        {
        }

        public void Display(bool displayed)
        {
        }

        public void ShowEnd()
        {
            BattleProcessor battleProcessor = BattleFlowCoordinator.Instance != null
                ? BattleFlowCoordinator.Instance.BattleProcessor
                : null;

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
            if (fadeCanvasGroup == null)
            {
                onFadeFinished?.Invoke();
                return;
            }

            if (activeFadeRoutine != null)
                StopCoroutine(activeFadeRoutine);

            float duration = screenFadeSettings != null ? screenFadeSettings.FadeInSeconds : 0.35f;
            activeFadeRoutine = StartCoroutine(FadeAlphaRoutine(0f, duration, onFadeFinished));
        }

        public void FadeOut(Action onFadeFinished)
        {
            if (fadeCanvasGroup == null)
            {
                onFadeFinished?.Invoke();
                return;
            }

            if (activeFadeRoutine != null)
                StopCoroutine(activeFadeRoutine);

            if (fadeImage != null && screenFadeSettings != null)
                fadeImage.color = screenFadeSettings.FadeColor;

            float duration = screenFadeSettings != null ? screenFadeSettings.FadeOutSeconds : 0.35f;
            activeFadeRoutine = StartCoroutine(FadeAlphaRoutine(1f, duration, onFadeFinished));
        }

        private IEnumerator FadeAlphaRoutine(float targetAlpha, float duration, Action onFadeFinished)
        {
            float startAlpha = fadeCanvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                float curve = screenFadeSettings != null && screenFadeSettings.FadeCurve != null
                    ? screenFadeSettings.FadeCurve.Evaluate(normalizedTime)
                    : normalizedTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, curve);
                yield return null;
            }

            fadeCanvasGroup.alpha = targetAlpha;
            activeFadeRoutine = null;
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
