using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

namespace Match3
{
    /// <summary>
    /// The GameManager is the interface between all the system in the game. It is either instantiated by the Loading scene
    /// which is the first at the start of the game, or Loaded from the Resource Folder dynamically at first access in editor
    /// so we can press play from any point of the game without having to add it to every scene and test if it already exist
    /// </summary>
    [DefaultExecutionOrder(-9999)]
    public class GameManager : MonoBehaviour
    {
        //This is set to true when the manager is deleted. This is useful as the manager can be deleted before other
        //objects 
        private static bool s_IsShuttingDown = false;
        
        public static GameManager Instance
        {
            get
            {
                
                // In Editor, the instance can be crated on the fly so we can play any scene without setup to do.
                // In a build, the first scene will Init all that so we are sure there will already be an instance.
#if UNITY_EDITOR
                if (s_Instance == null && !s_IsShuttingDown)
                {
                    var newInstance = Instantiate(Resources.Load<GameManager>("GameManager"));
                    newInstance.Awake();
                }
#endif
                return s_Instance;
            }

            private set => s_Instance = value;
        }

        public static bool IsShuttingDown()
        {
            return s_IsShuttingDown;
        }

        [Serializable]
        public class SoundData
        {
            public float MainVolume = 1.0f;
            public float MusicVolume = 1.0f;
            public float SFXVolume = 1.0f;
        }

        [System.Serializable]
        public class BonusItemEntry
        {
            public int Amount;
            public BonusItem Item;
        }
    
        private static GameManager s_Instance;

        public Board Board;
        public InputAction ClickAction;
        public InputAction ClickPosition;
        public GameSettings Settings;

        public int Coins { get; private set; } = 0;
        public int Stars { get; private set; }
        public int Lives { get; private set; } = 5;

        public SoundData Volumes => m_SoundData;

        public List<BonusItemEntry> BonusItems = new();

        public VFXPoolSystem PoolSystem { get; private set; } = new();

        [SerializeField] private Vector2 boardCameraOffset = new Vector2(0.0f, 0.75f);

        //we use two sources so we can crossfade
        private AudioSource MusicSourceActive;
        private AudioSource MusicSourceBackground;
        private Queue<AudioSource> m_SFXSourceQueue = new();

        private GameObject m_BonusModePrefab;
    
        private VisualEffect m_WinEffect;
        private VisualEffect m_LoseEffect;
        
        private SoundData m_SoundData = new();

        private void Awake()
        {
            if (s_Instance == this)
            {
                return;
            }

            if (s_Instance == null)
            {
                s_Instance = this;
                DontDestroyOnLoad(gameObject);
                
                Application.targetFrameRate = 60;
            
                ClickAction.Enable();
                ClickPosition.Enable();

                MusicSourceActive = Instantiate(Settings.SoundSettings.MusicSourcePrefab, transform);
                MusicSourceBackground = Instantiate(Settings.SoundSettings.MusicSourcePrefab, transform);

                MusicSourceActive.volume = 1.0f;
                MusicSourceBackground.volume = 0.0f;

                for (int i = 0; i < 16; ++i)
                {
                    var sourceInst = Instantiate(Settings.SoundSettings.SFXSourcePrefab, transform);
                    m_SFXSourceQueue.Enqueue(sourceInst);
                }

                if (Settings.VisualSettings.BonusModePrefab != null)
                {
                    m_BonusModePrefab = Instantiate(Settings.VisualSettings.BonusModePrefab);
                    m_BonusModePrefab.SetActive(false);
                }

                m_WinEffect = Instantiate(Settings.VisualSettings.WinEffect, transform);
                m_LoseEffect = Instantiate(Settings.VisualSettings.LoseEffect, transform);

                LoadSoundData();
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_IsShuttingDown = true;
        }

        void GetReferences()
        {
            Board = FindFirstObjectByType<Board>();
        }

        /// <summary>
        /// Вызывается из BattleSceneBootstrap после инициализации первого боя на сцене.
        /// </summary>
        public void StartLevel()
        {
            GetReferences();
            UIHandler.Instance.Display(true);
            
            m_WinEffect.gameObject.SetActive(false);
            m_LoseEffect.gameObject.SetActive(false);

            BattleFlowCoordinator battleFlow = BattleFlowCoordinator.Instance;
            if (battleFlow != null)
            {
                battleFlow.OnPlayerDefeated -= HandleBattleDefeatOrRunVictory;
                battleFlow.OnRunVictory -= HandleBattleDefeatOrRunVictory;
                battleFlow.OnPlayerDefeated += HandleBattleDefeatOrRunVictory;
                battleFlow.OnRunVictory += HandleBattleDefeatOrRunVictory;
            }

            AudioClip levelMusic = battleFlow.GetMusicForCurrentLevel();
            if (levelMusic != null)
                SwitchBattleMusic(levelMusic);

            PoolSystem.AddNewInstance(Settings.VisualSettings.CoinVFX, 12);

            //we delay the board init to leave enough time for all the tile to init
            StartCoroutine(DelayedInit());
        }

        private void HandleBattleDefeatOrRunVictory()
        {
            UIHandler.Instance.ShowEnd();
        }

        IEnumerator DelayedInit()
        {
            yield return null;

            Board.Init();
        }

        public void ChangeCoins(int amount)
        {
            Coins += amount;
            if (Coins < 0)
                Coins = 0;
        
            UIHandler.Instance.UpdateTopBarData();
        }
        

        public void AddLive(int amount)
        {
            Lives += amount;
        }
        

        public void UpdateVolumes()
        {
            Settings.SoundSettings.Mixer.SetFloat("MainVolume", Mathf.Log10(Mathf.Max(0.0001f, m_SoundData.MainVolume)) * 30.0f);
            Settings.SoundSettings.Mixer.SetFloat("SFXVolume", Mathf.Log10(Mathf.Max(0.0001f, m_SoundData.SFXVolume)) * 30.0f);
            Settings.SoundSettings.Mixer.SetFloat("MusicVolume", Mathf.Log10(Mathf.Max(0.0001f, m_SoundData.MusicVolume)) * 30.0f);
        }

        public void SaveSoundData()
        {
            System.IO.File.WriteAllText(Application.persistentDataPath + "/sounds.json", JsonUtility.ToJson(m_SoundData));
        }

        void LoadSoundData()
        {
            if (System.IO.File.Exists(Application.persistentDataPath + "/sounds.json"))
            {
                JsonUtility.FromJsonOverwrite(System.IO.File.ReadAllText(Application.persistentDataPath+"/sounds.json"), m_SoundData);
            }
            
            UpdateVolumes();
        }

        public void AddBonusItem(BonusItem item)
        {
            var existingItem = BonusItems.Find(entry => entry.Item == item);

            if (existingItem != null)
            {
                existingItem.Amount += 1;
            }
            else
            {
                BonusItems.Add(new BonusItemEntry()
                {
                    Amount = 1,
                    Item = item
                });
            }
            
            UIHandler.Instance.UpdateBottomBar();
        }

        public void ActivateBonusItem(BonusItem item)
        {
            if (BattleFlowCoordinator.Instance != null)
                BattleFlowCoordinator.Instance.DarkenBackground(item != null);
            m_BonusModePrefab?.SetActive(item != null);
            Board.ActivateBonusItem(item);
        }

        public void UseBonusItem(BonusItem item, Vector3Int cell)
        {
            var existingItem = BonusItems.Find(entry => entry.Item == item);
            if(existingItem == null) return;
        
            existingItem.Item.Use(cell);
            existingItem.Amount -= 1;
            
            m_BonusModePrefab?.SetActive(false);
            UIHandler.Instance.UpdateBottomBar();
            UIHandler.Instance.DeselectBonusItem();
        }

        public AudioSource PlaySFX(AudioClip clip)
        {
            var source = m_SFXSourceQueue.Dequeue();
            m_SFXSourceQueue.Enqueue(source);

            source.clip = clip;
            source.Play();

            return source;
        }

        public void WinTriggered()
        {
            PlaySFX(Settings.SoundSettings.WinVoice);
            m_WinEffect.gameObject.SetActive(true);
        }

        public void LooseTriggered()
        {
            PlaySFX(Settings.SoundSettings.LooseVoice);
            m_LoseEffect.gameObject.SetActive(true);
        }

        public void SwitchBattleMusic(AudioClip music)
        {
            if (music == null)
                return;

            SwitchMusic(music);
        }

        void SwitchMusic(AudioClip music)
        {
            MusicSourceBackground.clip = music;
            MusicSourceBackground.Play();
            (MusicSourceActive, MusicSourceBackground) = (MusicSourceBackground, MusicSourceActive);
        }
    }
}