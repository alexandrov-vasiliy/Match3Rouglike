using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Match3;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.VFX;
using Random = UnityEngine.Random;

namespace Match3
{
    [DefaultExecutionOrder(-9999)]
    public class Board : MonoBehaviour
    {
        // Доска хранит список BoardAction, которые обрабатываются в Update. Удобно для бонусов с таймерами и т.п.
        public interface IBoardAction
        {
            // true — продолжать тикать; false — действие завершено
            bool Tick();
        }

        public class PossibleSwap
        {
            public Vector3Int StartPosition;
            public Vector3Int Direction;
        }

        private static Board s_Instance;

        public List<Vector3Int> SpawnerPosition = new();
        public Dictionary<Vector3Int, BoardCell> CellContent = new();

        public Gem[] ExistingGems;

        public VisualEffect GemHoldPrefab;
        public VisualEffect HoldTrailPrefab;

        public BoundsInt Bounds => m_BoundsInt;
        public Grid Grid => m_Grid;

        private bool m_BoardWasInit = false;
        private bool m_InputEnabled = true;
        private bool m_FinalStretch = false; // цель достигнута или ходов не осталось; при стабилизации доски — конец уровня
        
        private Grid m_Grid;
        private BoundsInt m_BoundsInt;

        private Dictionary<int, Gem> m_GemLookup;

        private VisualSetting m_VisualSettingReference;

        private List<Vector3Int> m_TickingCells = new();
        private List<Vector3Int> m_NewTickingCells = new();
        private List<Match> m_TickingMatch = new();
        private List<Vector3Int> m_CellToMatchCheck = new();

        private bool m_BoardChanged = false;
        private List<PossibleSwap> m_PossibleSwaps = new();
        private GameObject m_HintIndicator;
        private int m_PickedSwap;
        private float m_SinceLastHint = 0.0f;

        private const int MaxBoardReshuffleAttempts = 64;
        private const int MaxNoMatchReshuffleBuildAttempts = 96;

        private bool m_ReshuffleAnimationInProgress = false;

        private readonly HashSet<int> m_ScratchGemTypesForReshuffle = new HashSet<int>();

        private const float ReshuffleStaggerSeconds = 0.028f;
        private const float ReshuffleLiftPhaseSeconds = 0.2f;
        private const float ReshuffleFlyPhaseSeconds = 0.38f;
        private const float ReshuffleArcHeightMin = 0.38f;
        private const float ReshuffleArcHeightFactor = 0.32f;
        private const float ReshuffleArcHeightMax = 1.05f;
        private const float ReshuffleScalePulse = 1.07f;

        // Некоторые бонусы (например ракета) блокируют движение гемов в/из зоны пути. После окончания эффекта —
        // разблокировка. Плюс при каждой блокировке, минус при снятии; так можно вкладывать несколько блокировок.
        private int m_FreezeMoveLock = 0;

        private List<Vector3Int> m_EmptyCells = new();

        private Dictionary<Vector3Int, Action> m_CellsCallbacks = new();
        private Dictionary<Vector3Int, Action> m_MatchedCallback = new();

        private List<IBoardAction> m_BoardActions = new();

        private bool m_SwipeQueued;
        private Vector3Int m_StartSwipe;
        private Vector3Int m_EndSwipe;
        // private bool m_IsHoldingTouch;

        private float m_LastClickTime = 0.0f;

        private BonusItem m_ActivatedBonus;

        private VisualEffect m_GemHoldVFXInstance;
        private VisualEffect m_HoldTrailInstance;

        private AudioSource m_FallingSoundSource;

        private enum SwapStage
        {
            None,
            Forward,
            Return
        }

        private SwapStage m_SwapStage = SwapStage.None;
        private (Vector3Int, Vector3Int) m_SwappingCells;

        // ---- ввод
        public Vector3 m_StartClickPosition;

        // Awake вызывается до первого кадра
        void Awake()
        {
            s_Instance = this;
            GetReference();
        }

        private void Start()
        {
#if !UNITY_EDITOR
            // В билде данные тайлмапа не обновляются сами — остаются как в редакторе. Нужно принудительно обновить,
            // иначе останется спрайт превью из режима редактирования.
        var tilemaps = m_Grid.GetComponentsInChildren<Tilemap>();
        foreach (var tilemap in tilemaps)
        {
            tilemap.RefreshAllTiles();
        }
#endif
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
            // При выходе из игры менеджер может уничтожиться раньше — проверяем, что приложение не завершается,
            // иначе в редакторе обращение к GameManager.Instance создаст новый экземпляр.
            if(!GameManager.IsShuttingDown()) GameManager.Instance.PoolSystem.Clean();
        }

        void GetReference()
        {
            m_Grid = GetComponent<Grid>();
        }

        public void ToggleInput(bool input)
        {
            m_InputEnabled = input;
        }

        public void TriggerFinalStretch()
        {
            m_FinalStretch = true;
        }

        public void LockMovement()
        {
            m_FreezeMoveLock += 1;
        }

        public void UnlockMovement()
        {
            m_FreezeMoveLock -= 1;
        }

        public void Init()
        {
            m_VisualSettingReference = GameManager.Instance.Settings.VisualSettings;
            m_LastClickTime = Time.time;

            UIHandler.Instance.Init();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (var bonus in GameManager.Instance.Settings.BonusSettings.Bonuses)
            {
                UIHandler.Instance.RegisterGemToDebug(bonus);
            }
        
            foreach (var gem in GameManager.Instance.Board.ExistingGems)
            {
                UIHandler.Instance.RegisterGemToDebug(gem);
            }
#endif
            
            // словарь: тип гема → префаб гема
            m_GemLookup = new Dictionary<int, Gem>();
            foreach (var gem in ExistingGems)
            {
                m_GemLookup.Add(gem.GemType, gem);
            }

            GenerateBoard();
            StartCoroutine(InitAfterGenerateRoutine());
        }

        IEnumerator InitAfterGenerateRoutine()
        {
            yield return RunReshufflePasses(forceReshuffleEvenIfMovesExist: false);

            m_HintIndicator = Instantiate(GameManager.Instance.Settings.VisualSettings.HintPrefab);
            m_HintIndicator.SetActive(false);

            m_BoardWasInit = true;

            if (GemHoldPrefab != null)
            {
                m_GemHoldVFXInstance = Instantiate(GemHoldPrefab);
                m_GemHoldVFXInstance.gameObject.SetActive(false);
            }

            if (HoldTrailPrefab != null)
            {
                m_HoldTrailInstance = Instantiate(HoldTrailPrefab);
                m_HoldTrailInstance.gameObject.SetActive(false);
            }

            ToggleInput(false);
            // пара кадров до FadeIn, чтобы UI успел инициализироваться и анимация проигралась
            StartCoroutine(WaitToFadeIn());
        }

        IEnumerator WaitToFadeIn()
        {
            yield return null;
            yield return null;
            UIHandler.Instance.FadeIn(() => { ToggleInput(true); });
        }

        // Вызывается Gem Placer при создании ячейки размещения
        public static void RegisterCell(Vector3Int cellPosition, Gem startingGem = null)
        {
            // Костыль: Startup может выполниться раньше всех Awake...
            if (s_Instance == null)
            {
                s_Instance = GameObject.Find("Grid").GetComponent<Board>();
                s_Instance.GetReference();
            }

            if(!s_Instance.CellContent.ContainsKey(cellPosition))
                s_Instance.CellContent.Add(cellPosition, new BoardCell());

            if (startingGem != null)
            {
                s_Instance.NewGemAt(cellPosition, startingGem);
            }
        }

        public static void AddObstacle(Vector3Int cell, Obstacle obstacle)
        {
            RegisterCell(cell);

            obstacle.transform.position = s_Instance.m_Grid.GetCellCenterWorld(cell);
            s_Instance.CellContent[cell].Obstacle = obstacle;
        }

        public static void ChangeLock(Vector3Int cellPosition, bool lockState)
        {
            // Костыль: Startup может выполниться раньше всех Awake...
            if (s_Instance == null)
            {
                s_Instance = GameObject.Find("Grid").GetComponent<Board>();
                s_Instance.GetReference();
            }

            s_Instance.CellContent[cellPosition].Locked = lockState;
        }

        public static void RegisterDeletedCallback(Vector3Int cellPosition, System.Action callback)
        {
            if (!s_Instance.m_CellsCallbacks.ContainsKey(cellPosition))
            {
                s_Instance.m_CellsCallbacks[cellPosition] = callback;
            }
            else
            {
                s_Instance.m_CellsCallbacks[cellPosition] += callback;
            }
        }

        public static void UnregisterDeletedCallback(Vector3Int cellPosition, System.Action callback)
        {
            if(!s_Instance.m_CellsCallbacks.ContainsKey(cellPosition))
                return;
        
            s_Instance.m_CellsCallbacks[cellPosition] -= callback;
            if (s_Instance.m_CellsCallbacks[cellPosition] == null)
                s_Instance.m_CellsCallbacks.Remove(cellPosition);
        }
        
        public static void RegisterMatchedCallback(Vector3Int cellPosition, System.Action callback)
        {
            if (!s_Instance.m_MatchedCallback.ContainsKey(cellPosition))
            {
                s_Instance.m_MatchedCallback[cellPosition] = callback;
            }
            else
            {
                s_Instance.m_MatchedCallback[cellPosition] += callback;
            }
        }

        public static void UnregisterMatchedCallback(Vector3Int cellPosition, System.Action callback)
        {
            if(!s_Instance.m_MatchedCallback.ContainsKey(cellPosition))
                return;
        
            s_Instance.m_MatchedCallback[cellPosition] -= callback;
            if (s_Instance.m_MatchedCallback[cellPosition] == null)
                s_Instance.m_MatchedCallback.Remove(cellPosition);
        }

        public static void RegisterSpawner(Vector3Int cell)
        {
            // Костыль: Startup может выполниться раньше всех Awake...
            if (s_Instance == null)
            {
                s_Instance = GameObject.Find("Grid").GetComponent<Board>();
                s_Instance.GetReference();
            }

            s_Instance.SpawnerPosition.Add(cell);
        }

        // Создаёт гем в каждой пустой ячейке так, чтобы не было готового матча
        void GenerateBoard()
        {
            m_BoundsInt = new BoundsInt();
            var listOfCells = CellContent.Keys.ToList();

            m_BoundsInt.xMin = listOfCells[0].x;
            m_BoundsInt.xMax = m_BoundsInt.xMin;
        
            m_BoundsInt.yMin = listOfCells[0].y;
            m_BoundsInt.yMax = m_BoundsInt.yMin;

            foreach (var content in listOfCells)
            {
                if (content.x > m_BoundsInt.xMax)
                    m_BoundsInt.xMax = content.x;
                else if (content.x < m_BoundsInt.xMin)
                    m_BoundsInt.xMin = content.x;

                if (content.y > m_BoundsInt.yMax)
                    m_BoundsInt.yMax = content.y;
                else if (content.y < m_BoundsInt.yMin)
                    m_BoundsInt.yMin = content.y;
            }

            for (int y = m_BoundsInt.yMin; y <= m_BoundsInt.yMax; ++y)
            {
                for (int x = m_BoundsInt.xMin; x <= m_BoundsInt.xMax; ++x)
                {
                    var idx = new Vector3Int(x, y, 0);
                
                    if(!CellContent.TryGetValue(idx, out var current) || current.ContainingGem != null)
                        continue;
                
                    var availableGems = m_GemLookup.Keys.ToList();
                    RemoveGemTypesThatWouldCreateImmediateMatchAt(idx, availableGems,
                        cellPosition => CellContent.TryGetValue(cellPosition, out var boardCell) ? boardCell.ContainingGem : null);

                    var chosenGem = availableGems[Random.Range(0, availableGems.Count)];
                    NewGemAt(idx, m_GemLookup[chosenGem]);
                }
            }
        }

        /// <summary>
        /// Убирает из <paramref name="availableGemTypes"/> типы, постановка которых в <paramref name="cellIndex"/>
        /// сразу дала бы готовый матч (как при генерации доски).
        /// </summary>
        void RemoveGemTypesThatWouldCreateImmediateMatchAt(Vector3Int cellIndex, List<int> availableGemTypes,
            Func<Vector3Int, Gem> getGemAtCell)
        {
            Gem GemAt(Vector3Int worldCell) => getGemAtCell(worldCell);

            int leftGemType = -1;
            int bottomGemType = -1;
            int rightGemType = -1;
            int topGemType = -1;

            Gem leftGem = GemAt(cellIndex + new Vector3Int(-1, 0, 0));
            if (leftGem != null)
            {
                leftGemType = leftGem.GemType;
                Gem leftLeftGem = GemAt(cellIndex + new Vector3Int(-2, 0, 0));
                if (leftLeftGem != null && leftGemType == leftLeftGem.GemType)
                    availableGemTypes.Remove(leftGemType);
            }

            Gem bottomGem = GemAt(cellIndex + new Vector3Int(0, -1, 0));
            if (bottomGem != null)
            {
                bottomGemType = bottomGem.GemType;
                Gem bottomBottomGem = GemAt(cellIndex + new Vector3Int(0, -2, 0));
                if (bottomBottomGem != null && bottomGemType == bottomBottomGem.GemType)
                    availableGemTypes.Remove(bottomGemType);

                if (leftGemType != -1 && leftGemType == bottomGemType)
                {
                    Gem bottomLeftGem = GemAt(cellIndex + new Vector3Int(-1, -1, 0));
                    if (bottomLeftGem != null && bottomGemType == leftGemType)
                        availableGemTypes.Remove(leftGemType);
                }
            }

            Gem rightGem = GemAt(cellIndex + new Vector3Int(1, 0, 0));
            if (rightGem != null)
            {
                rightGemType = rightGem.GemType;
                if (rightGemType != -1 && leftGemType == rightGemType)
                    availableGemTypes.Remove(rightGemType);

                Gem rightRightGem = GemAt(cellIndex + new Vector3Int(2, 0, 0));
                if (rightRightGem != null && rightGemType == rightRightGem.GemType)
                    availableGemTypes.Remove(rightGemType);

                if (rightGemType != -1 && rightGemType == bottomGemType)
                {
                    Gem bottomRightGem = GemAt(cellIndex + new Vector3Int(1, -1, 0));
                    if (bottomRightGem != null && bottomRightGem.GemType == rightGemType)
                        availableGemTypes.Remove(rightGemType);
                }
            }

            Gem topGem = GemAt(cellIndex + new Vector3Int(0, 1, 0));
            if (topGem != null)
            {
                topGemType = topGem.GemType;
                if (topGemType != -1 && topGemType == bottomGemType)
                    availableGemTypes.Remove(topGemType);

                Gem topTopGem = GemAt(cellIndex + new Vector3Int(0, 2, 0));
                if (topTopGem != null && topGemType == topTopGem.GemType)
                    availableGemTypes.Remove(topGemType);

                if (topGemType != -1 && topGemType == rightGemType)
                {
                    Gem topRightGem = GemAt(cellIndex + new Vector3Int(1, 1, 0));
                    if (topRightGem != null && topRightGem.GemType == topGemType)
                        availableGemTypes.Remove(topGemType);
                }

                if (topGemType != -1 && topGemType == leftGemType)
                {
                    Gem topLeftGem = GemAt(cellIndex + new Vector3Int(-1, 1, 0));
                    if (topLeftGem != null && topLeftGem.GemType == topGemType)
                        availableGemTypes.Remove(topGemType);
                }
            }
        }

        private void Update()
        {
            if(!m_BoardWasInit)
                return;
            
            GameManager.Instance.PoolSystem.Update();

            for (int i = 0; i < m_BoardActions.Count; ++i)
            {
                if (!m_BoardActions[i].Tick())
                {
                    m_BoardActions.RemoveAt(i);
                    i--;
                }
            }
        
            CheckInput();

            if (m_SwapStage != SwapStage.None)
            {
                TickSwap();
                // return;
            }

            // станет false при любом событии на доске
            // увеличивается только когда доска неподвижна и ничего не происходит
            // изначально true, если нет активного бонусного предмета (с бонусом таймер подсказки не тикает)
            bool incrementHintTimer = m_ActivatedBonus == null;

            if (m_TickingCells.Count > 0)
            {
                MoveGems();
                
                // чтобы не наслаивать звуки — падение играем только если другой звук падения не играет
                if (m_TickingCells.Count == 0 && (m_FallingSoundSource == null || !m_FallingSoundSource.isPlaying))
                {
                    m_FallingSoundSource = GameManager.Instance.PlaySFX(GameManager.Instance.Settings.SoundSettings.FallSound);
                }

                incrementHintTimer = false;
                m_BoardChanged = true;
            }
            
            if (m_CellToMatchCheck.Count > 0)
            {
                DoMatchCheck();
                
                incrementHintTimer = false;
                m_BoardChanged = true;
            }
            
            if (m_TickingMatch.Count > 0)
            {
                MatchTicking();
                
                incrementHintTimer = false;
                m_BoardChanged = true;
            } 
            
            if (m_EmptyCells.Count > 0)
            {
                EmptyCheck();
                
                incrementHintTimer = false;
                m_BoardChanged = true;
            } 
            
            if (m_SwipeQueued)
            {
                CellContent[m_StartSwipe].IncomingGem = CellContent[m_EndSwipe].ContainingGem;
                CellContent[m_EndSwipe].IncomingGem = CellContent[m_StartSwipe].ContainingGem;

                CellContent[m_StartSwipe].ContainingGem = null;
                CellContent[m_EndSwipe].ContainingGem = null;

                m_SwapStage = SwapStage.Forward;
                m_SwappingCells = (m_StartSwipe, m_EndSwipe);
                
                GameManager.Instance.PlaySFX(GameManager.Instance.Settings.SoundSettings.SwipSound);

                m_SwipeQueued = false;
                incrementHintTimer = false;
            }

            if (m_NewTickingCells.Count > 0)
            {
                m_TickingCells.AddRange(m_NewTickingCells);
                m_NewTickingCells.Clear();
                incrementHintTimer = false;
            }
            
            if (incrementHintTimer)
            {
                if (m_FinalStretch)
                {
                    m_FinalStretch = false;
                    UIHandler.Instance.ShowEnd();
                    return;
                }

                if (m_BoardChanged && !m_ReshuffleAnimationInProgress)
                {
                    m_BoardChanged = false;
                    StartCoroutine(BoardSettledReshuffleRoutine());
                }
            
                if (m_PossibleSwaps.Count > 0)
                {
                    var match = m_PossibleSwaps[m_PickedSwap];
                    if (m_HintIndicator.activeSelf)
                    {
                        var startPos = m_Grid.GetCellCenterWorld(match.StartPosition);
                        var endPos = m_Grid.GetCellCenterWorld(match.StartPosition + match.Direction);

                        var current = m_HintIndicator.transform.position;
                        current = Vector3.MoveTowards(current, endPos, 1.0f * Time.deltaTime);

                        m_HintIndicator.transform.position = current == endPos ? startPos : current;
                    }
                    else
                    {
                        m_SinceLastHint += Time.deltaTime;
                        if (m_SinceLastHint >= GameManager.Instance.Settings.InactivityBeforeHint && m_InputEnabled)
                        {
                            m_HintIndicator.transform.position = m_Grid.GetCellCenterWorld(match.StartPosition);
                            m_HintIndicator.SetActive(true);
                        }
                    }
                }
                else
                {
                    m_HintIndicator.SetActive(false);
                    m_SinceLastHint = 0.0f;
                }
            }
            else
            {
                m_HintIndicator.SetActive(false);
                m_SinceLastHint = 0.0f;
            }
        }

        void MoveGems()
        {
            // сортировка снизу-слева к верху-справа: меньше гонок (верхний гем может «упасть» в ячейку,
            // которая ещё занята, но освободится, когда уйдёт нижний)
            m_TickingCells.Sort((a, b) =>
            {
                int yCmp = a.y.CompareTo(b.y);
                if (yCmp == 0)
                {
                    return a.x.CompareTo(b.x);
                }

                return yCmp;
            });

            for (int i = 0; i < m_TickingCells.Count; i++)
            {
                var cellIdx = m_TickingCells[i];

                var currentCell = CellContent[cellIdx];
                var targetPosition = m_Grid.GetCellCenterWorld(cellIdx);
                
                if (currentCell.IncomingGem != null && currentCell.ContainingGem != null)
                {
                    Debug.LogError(
                        $"A ticking cell at {cellIdx} have incoming gems {currentCell.IncomingGem} containing gem {currentCell.ContainingGem}");
                    continue;
                }
                
                // обновление позиции или состояния
                if (currentCell.IncomingGem?.CurrentState == Gem.State.Falling)
                {
                    var gem = currentCell.IncomingGem;
                    gem.TickMoveTimer(Time.deltaTime);

                    var maxDistance = m_VisualSettingReference.FallAccelerationCurve.Evaluate(gem.FallTime) *
                                      Time.deltaTime * m_VisualSettingReference.FallSpeed * gem.SpeedMultiplier;
                    
                    gem.transform.position = Vector3.MoveTowards(gem.transform.position, targetPosition,
                        maxDistance);

                    if (gem.transform.position == targetPosition)
                    {
                        m_TickingCells.RemoveAt(i);
                        i--;

                        currentCell.IncomingGem = null;
                        currentCell.ContainingGem = gem;
                        gem.MoveTo(cellIdx);
                        
                        // достигли целевой ячейки — продолжаем падение или завершаем
                        if (m_EmptyCells.Contains(cellIdx + Vector3Int.down) && CellContent.TryGetValue(cellIdx + Vector3Int.down, out var belowCell))
                        {
                            // входящий гем переходит в ячейку снизу
                            currentCell.ContainingGem = null;
                            belowCell.IncomingGem = gem;

                            gem.SpeedMultiplier = 1.0f;

                            var target = cellIdx + Vector3Int.down;
                            m_NewTickingCells.Add(target);
                            
                            m_EmptyCells.Remove(target);
                            m_EmptyCells.Add(cellIdx);

                            // ячейка остаётся пустой; гем сверху упадёт сам; над спавнером — создаём новый гем
                            if (SpawnerPosition.Contains(cellIdx + Vector3Int.up))
                            {
                                ActivateSpawnerAt(cellIdx);
                            }
                        }
                        else if ((!CellContent.TryGetValue(cellIdx + Vector3Int.left, out var leftCell) ||
                                  leftCell.BlockFall) && 
                                 m_EmptyCells.Contains(cellIdx + Vector3Int.down + Vector3Int.left) &&
                                 CellContent.TryGetValue(cellIdx + Vector3Int.down + Vector3Int.left, out var belowLeftCell))
                        {
                            // слева нет ячейки или блок падения — можно упасть по диагонали вниз-влево
                            currentCell.ContainingGem = null;
                            belowLeftCell.IncomingGem = gem;

                            gem.SpeedMultiplier = 1.41421356237f;

                            var target = cellIdx + Vector3Int.down + Vector3Int.left;
                            m_NewTickingCells.Add(target);
                            
                            // убрать цель из списка пустых, текущую ячейку добавить как пустую
                            m_EmptyCells.Remove(target);
                            m_EmptyCells.Add(cellIdx);

                            // ячейка остаётся пустой; гем сверху упадёт сам; над спавнером — создаём новый гем
                            if (SpawnerPosition.Contains(cellIdx + Vector3Int.up))
                            {
                                ActivateSpawnerAt(cellIdx);
                            }
                        }
                        else if ((!CellContent.TryGetValue(cellIdx + Vector3Int.right, out var rightCell) ||
                                  rightCell.BlockFall) &&
                                 m_EmptyCells.Contains(cellIdx + Vector3Int.down + Vector3Int.right) &&
                                 CellContent.TryGetValue(cellIdx + Vector3Int.down + Vector3Int.right, out var belowRightCell))
                        {
                            // прямо вниз нельзя — диагональ вниз-вправо
                            // входящий гем переходит в ячейку снизу
                            currentCell.ContainingGem = null;
                            belowRightCell.IncomingGem = gem;

                            gem.SpeedMultiplier = 1.41421356237f;

                            var target = cellIdx + Vector3Int.down + Vector3Int.right;
                            m_NewTickingCells.Add(target);
                            
                            // убрать цель из списка пустых, текущую ячейку добавить как пустую
                            m_EmptyCells.Remove(target);
                            m_EmptyCells.Add(cellIdx);

                            // ячейка остаётся пустой; гем сверху упадёт сам; над спавнером — создаём новый гем
                            if (SpawnerPosition.Contains(cellIdx + Vector3Int.up))
                            {
                                ActivateSpawnerAt(cellIdx);
                            }
                        }
                        else
                        {
                            // снова в список тикающих, но уже отскок, не падение
                            m_NewTickingCells.Add(cellIdx);
                            gem.StopFalling();
                        }
                    }
                }
                else if (currentCell.ContainingGem?.CurrentState == Gem.State.Bouncing)
                {
                    var gem = currentCell.ContainingGem;
                    gem.TickMoveTimer(Time.deltaTime);
                    Vector3 center = m_Grid.GetCellCenterWorld(cellIdx);

                    float maxTime = m_VisualSettingReference.BounceCurve
                        .keys[m_VisualSettingReference.BounceCurve.length - 1].time;
                    
                    if (gem.FallTime >= maxTime)
                    {
                        gem.transform.position = center;
                        gem.transform.localScale = Vector3.one;
                        gem.StopBouncing();

                        m_TickingCells.RemoveAt(i);
                        i--;
                        m_CellToMatchCheck.Add(cellIdx);
                    }
                    else
                    {
                        gem.transform.position =
                            center + Vector3.up * m_VisualSettingReference.BounceCurve.Evaluate(gem.FallTime);
                        gem.transform.localScale =
                            new Vector3(1, m_VisualSettingReference.SquishCurve.Evaluate(gem.FallTime), 1);
                    }
                }
                else if(currentCell.ContainingGem?.CurrentState == Gem.State.Still)
                {
                    // в тикающих должны быть только падающие или отскакивающие — иначе убираем из списка
                    m_TickingCells.RemoveAt(i);
                    i--;
                }

            }
        }

        void MatchTicking()
        {
            for (int i = 0; i < m_TickingMatch.Count; ++i)
            {
                var match = m_TickingMatch[i];

                Debug.Assert(match.MatchingGem.Count == match.MatchingGem.Distinct().Count(),
                    "There is duplicate gems in the matching lists");

                const float deletionSpeed = 1.0f / 0.3f;
                match.DeletionTimer += Time.deltaTime * deletionSpeed;
                
                for(int j = 0; j < match.MatchingGem.Count; j++)
                {
                    var gemIdx = match.MatchingGem[j];
                    var gem = CellContent[gemIdx].ContainingGem;

                    if (gem == null)
                    {
                        match.MatchingGem.RemoveAt(j);
                        j--;
                        continue;
                    }

                    if (gem.CurrentState == Gem.State.Bouncing)
                    {
                        // останавливаем отскок, гем уничтожается
                        // проверяем и текущий, и новый список тикающих: в первом кадре отскока гем может быть только в m_NewTickingCells
                        if(m_TickingCells.Contains(gemIdx)) m_TickingCells.Remove(gemIdx);
                        if(m_NewTickingCells.Contains(gemIdx)) m_NewTickingCells.Remove(gemIdx);
                        
                        gem.transform.position = m_Grid.GetCellCenterWorld(gemIdx);
                        gem.transform.localScale = Vector3.one;
                        gem.StopBouncing();
                    }

                    // принудительное удаление не ждёт таймер
                    if (match.ForcedDeletion || match.DeletionTimer > 1.0f)
                    {
                        Destroy(CellContent[gemIdx].ContainingGem.gameObject);
                        CellContent[gemIdx].ContainingGem = null;
                    
                        if (match.ForcedDeletion && CellContent[gemIdx].Obstacle != null)
                        {
                            CellContent[gemIdx].Obstacle.Clear();
                        }

                        // колбэк только для матча со свайпа, не от бонуса и т.п.
                        if (!match.ForcedDeletion && m_CellsCallbacks.TryGetValue(gemIdx, out var clbk))
                        {
                            clbk.Invoke();
                        }
                    
                        match.MatchingGem.RemoveAt(j);
                        j--;

                        match.DeletedCount += 1;
                        // монеты только за обычный матч (не принудительный)
                        if (match.DeletedCount >= 4 && !match.ForcedDeletion)
                        {
                            GameManager.Instance.ChangeCoins(1);
                            GameManager.Instance.PoolSystem.PlayInstanceAt(GameManager.Instance.Settings.VisualSettings.CoinVFX,
                                gem.transform.position);
                        }
                    
                        if (match.SpawnedBonus != null && match.OriginPoint == gemIdx)
                        {
                            NewGemAt(match.OriginPoint, match.SpawnedBonus);
                        }
                        else
                        {
                            m_EmptyCells.Add(gemIdx);
                        }

                        // эффекты матча и скрытие гема
                        if (gem.CurrentState != Gem.State.Disappearing)
                        {
                            BattleFlowCoordinator.Instance.GemMatched(gem);
                            
                            foreach (var matchEffectPrefab in gem.MatchEffectPrefabs)
                            {
                                GameManager.Instance.PoolSystem.PlayInstanceAt(matchEffectPrefab, m_Grid.GetCellCenterWorld(gem.CurrentIndex));
                            }

                            gem.gameObject.SetActive(false);

                            gem.Destroyed();
                        }
                    }
                    else if(gem.CurrentState != Gem.State.Disappearing)
                    {
                        BattleFlowCoordinator.Instance.GemMatched(gem);
                        
                        foreach (var matchEffectPrefab in gem.MatchEffectPrefabs)
                        {
                            GameManager.Instance.PoolSystem.PlayInstanceAt(matchEffectPrefab, m_Grid.GetCellCenterWorld(gem.CurrentIndex));
                        }

                        gem.gameObject.SetActive(false);

                        gem.Destroyed();
                    }
                }

                if (match.MatchingGem.Count == 0)
                {
                    m_TickingMatch.RemoveAt(i);
                    i--;
                }
            }
        }

        public void DestroyGem(Vector3Int cell, bool forcedDeletion = false)
        {
            if(CellContent[cell].ContainingGem?.CurrentMatch != null)
                return;

            var match = new Match()
            {
                DeletionTimer = 0.0f,
                MatchingGem = new List<Vector3Int> { cell },
                OriginPoint = cell,
                SpawnedBonus = null,
                ForcedDeletion = forcedDeletion
            };

            CellContent[cell].ContainingGem.CurrentMatch = match;
        
            m_TickingMatch.Add(match);
        }

        public Vector3 GetCellCenter(Vector3Int cell)
        {
            return m_Grid.GetCellCenterWorld(cell);
        }

        public Vector3Int WorldToCell(Vector3 pos)
        {
            return m_Grid.WorldToCell(pos);
        }

        public void AddNewBoardAction(IBoardAction action)
        {
            m_BoardActions.Add(action);
        }

        // удобно для бонусов: создаёт новый матч, в который можно добавить что угодно
        public Match CreateCustomMatch(Vector3Int newCell)
        {
            var newMatch = new Match()
            {
                DeletionTimer = 0.0f,
                MatchingGem = new(),
                OriginPoint = newCell,
                SpawnedBonus = null
            };
        
            m_TickingMatch.Add(newMatch);

            return newMatch;
        }

        void EmptyCheck()
        {
            if (m_FreezeMoveLock > 0)
                return;
            
            // обход пустых ячеек
            for (int i = 0; i < m_EmptyCells.Count; ++i)
            {
                var emptyCell = m_EmptyCells[i];

                if (!CellContent[emptyCell].IsEmpty())
                {
                    m_EmptyCells.RemoveAt(i);
                    i--;
                    continue;
                }

                var aboveCellIdx = emptyCell + Vector3Int.up;
                bool aboveCellExist = CellContent.TryGetValue(aboveCellIdx, out var aboveCell);

                // над пустой ячейкой есть гем — заставляем его упасть
                if (aboveCellExist && aboveCell.ContainingGem != null && aboveCell.CanFall)
                {
                    var incomingGem = aboveCell.ContainingGem;
                    CellContent[emptyCell].IncomingGem = incomingGem;
                    aboveCell.ContainingGem = null;

                    incomingGem.StartMoveTimer();
                    incomingGem.SpeedMultiplier = 1.0f;

                    // добавляем целевую ячейку в тикающие, чтобы гем дошёл до неё
                    m_NewTickingCells.Add(emptyCell);

                    // верхняя ячейка пуста, текущая — занята входящим гемом
                    m_EmptyCells.Add(aboveCellIdx);
                    m_EmptyCells.Remove(emptyCell);
                }
                else if ((!aboveCellExist || aboveCell.BlockFall) &&
                         CellContent.TryGetValue(aboveCellIdx + Vector3Int.right, out var aboveRightCell) &&
                         aboveRightCell.ContainingGem != null && aboveRightCell.CanFall)
                {
                    var incomingGem = aboveRightCell.ContainingGem;
                    CellContent[emptyCell].IncomingGem = incomingGem;
                    aboveRightCell.ContainingGem = null;

                    incomingGem.StartMoveTimer();
                    incomingGem.SpeedMultiplier = 1.41421356237f;

                    // добавляем целевую ячейку в тикающие, чтобы гем дошёл до неё
                    m_NewTickingCells.Add(emptyCell);

                    // верхняя ячейка пуста, текущая — занята входящим гемом
                    m_EmptyCells.Add(aboveCellIdx + Vector3Int.right);
                    m_EmptyCells.Remove(emptyCell);
                }
                else if ((!aboveCellExist || aboveCell.BlockFall) &&
                         CellContent.TryGetValue(aboveCellIdx + Vector3Int.left, out var aboveLeftCell) &&
                         aboveLeftCell.ContainingGem != null && aboveLeftCell.CanFall)
                {
                    var incomingGem = aboveLeftCell.ContainingGem;
                    CellContent[emptyCell].IncomingGem = incomingGem;
                    aboveLeftCell.ContainingGem = null;

                    incomingGem.StartMoveTimer();
                    incomingGem.SpeedMultiplier = 1.41421356237f;

                    // добавляем целевую ячейку в тикающие, чтобы гем дошёл до неё
                    m_NewTickingCells.Add(emptyCell);

                    // верхняя ячейка пуста, текущая — занята входящим гемом
                    m_EmptyCells.Add(aboveCellIdx + Vector3Int.left);
                    m_EmptyCells.Remove(emptyCell);
                }
                else if (SpawnerPosition.Contains(aboveCellIdx))
                {
                    // создать новый гем
                    ActivateSpawnerAt(emptyCell);
                }
            }

            // пустые ячейки обрабатываются за один проход; список не очищаем здесь
            // m_EmptyCells.Clear();
        }

        void DoMatchCheck()
        {
            foreach (var cell in m_CellToMatchCheck)
            {
                DoCheck(cell);
            }

            m_CellToMatchCheck.Clear();
        }

        void DrawDebugCross(Vector3 center)
        {
            Debug.DrawLine(center + Vector3.left * 0.5f + Vector3.up * 0.5f,
                center + Vector3.right * 0.5f + Vector3.down * 0.5f);
            Debug.DrawLine(center + Vector3.left * 0.5f - Vector3.up * 0.5f,
                center + Vector3.right * 0.5f - Vector3.down * 0.5f);
        }

        // если gemPrefab == null, берётся случайный из ExistingGems
        Gem NewGemAt(Vector3Int cell, Gem gemPrefab)
        {
            if (gemPrefab == null)
                gemPrefab = ExistingGems[Random.Range(0, ExistingGems.Length)];

            if (gemPrefab.MatchEffectPrefabs.Length != 0)
            {
                foreach (var matchEffectPrefab in gemPrefab.MatchEffectPrefabs)
                {
                    GameManager.Instance.PoolSystem.AddNewInstance(matchEffectPrefab, 16);
                }
            }

            // NewGemAt может вызываться после Init (порядок Startup/Init не гарантирован)
            if (CellContent[cell].ContainingGem != null)
            {
                Destroy(CellContent[cell].ContainingGem.gameObject);
            }

            var gem = Instantiate(gemPrefab, m_Grid.GetCellCenterWorld(cell), Quaternion.identity);
            CellContent[cell].ContainingGem = gem;
            gem.Init(cell);
        
            return gem;
        }

        void ActivateSpawnerAt(Vector3Int cell)
        {
            var gem = Instantiate(ExistingGems[Random.Range(0, ExistingGems.Length)], m_Grid.GetCellCenterWorld(cell + Vector3Int.up), Quaternion.identity);
            CellContent[cell].IncomingGem = gem;
        
            gem.StartMoveTimer();
            gem.SpeedMultiplier = 1.0f; 
            m_NewTickingCells.Add(cell);

            if (m_EmptyCells.Contains(cell)) m_EmptyCells.Remove(cell);
        }

        void TickSwap()
        {
            var gemToStart = CellContent[m_SwappingCells.Item1].IncomingGem;
            var gemToEnd = CellContent[m_SwappingCells.Item2].IncomingGem;

            var startPosition = m_Grid.GetCellCenterWorld(m_SwappingCells.Item1);
            var endPosition = m_Grid.GetCellCenterWorld(m_SwappingCells.Item2);

            gemToStart.transform.position =
                Vector3.MoveTowards(gemToStart.transform.position, startPosition, Time.deltaTime * m_VisualSettingReference.FallSpeed);
            gemToEnd.transform.position =
                Vector3.MoveTowards(gemToEnd.transform.position, endPosition, Time.deltaTime * m_VisualSettingReference.FallSpeed);

            if (gemToStart.transform.position == startPosition)
            {
                // обмен завершён
                if (m_SwapStage == SwapStage.Forward)
                {
                    // временно снимаем блокировку, чтобы корректно удалить гемы при матче
                    CellContent[m_SwappingCells.Item1].Locked = false;
                    CellContent[m_SwappingCells.Item2].Locked = false;
                    
                    CellContent[m_SwappingCells.Item1].ContainingGem = CellContent[m_SwappingCells.Item1].IncomingGem;
                    CellContent[m_SwappingCells.Item2].ContainingGem = CellContent[m_SwappingCells.Item2].IncomingGem;
                
                    CellContent[m_SwappingCells.Item1].ContainingGem.MoveTo(m_SwappingCells.Item1);
                    CellContent[m_SwappingCells.Item2].ContainingGem.MoveTo(m_SwappingCells.Item2);

                    bool firstCheck = false;
                    bool secondCheck = false;

                    if (CellContent[m_SwappingCells.Item1].ContainingGem.Usable)
                    {
                        CellContent[m_SwappingCells.Item1].ContainingGem.Use(CellContent[m_SwappingCells.Item2].ContainingGem);
                        firstCheck = true;
                    }
                    else
                    {
                        firstCheck = DoCheck(m_SwappingCells.Item1);
                    }

                    if (CellContent[m_SwappingCells.Item2].ContainingGem.Usable)
                    {
                        CellContent[m_SwappingCells.Item2].ContainingGem.Use(CellContent[m_SwappingCells.Item1].ContainingGem);
                        secondCheck = true;
                    }
                    else
                    {
                        secondCheck =  DoCheck(m_SwappingCells.Item2);
                    }

                    if (firstCheck || secondCheck)
                    {
                        CellContent[m_SwappingCells.Item1].IncomingGem = null;
                        CellContent[m_SwappingCells.Item2].IncomingGem = null;

                        m_SwapStage = SwapStage.None;

                        // успешный обмен — засчитываем ход уровня
                        BattleFlowCoordinator.Instance.PlayerMoved();
                    }
                    else
                    {
                        // матча нет — откатываем обмен
                        (CellContent[m_SwappingCells.Item1].IncomingGem, CellContent[m_SwappingCells.Item2].IncomingGem) = (
                            CellContent[m_SwappingCells.Item2].IncomingGem, CellContent[m_SwappingCells.Item1].IncomingGem);
                        (m_SwappingCells.Item1, m_SwappingCells.Item2) = (m_SwappingCells.Item2, m_SwappingCells.Item1);
                        m_SwapStage = SwapStage.Return;
                        
                        // снова блокируем ячейки на время обратного обмена
                        CellContent[m_SwappingCells.Item1].Locked = true;
                        CellContent[m_SwappingCells.Item2].Locked = true;
                    }
                }
                else
                {
                    CellContent[m_SwappingCells.Item1].ContainingGem = CellContent[m_SwappingCells.Item1].IncomingGem;
                    CellContent[m_SwappingCells.Item2].ContainingGem = CellContent[m_SwappingCells.Item2].IncomingGem;
                
                    CellContent[m_SwappingCells.Item1].ContainingGem.MoveTo(m_SwappingCells.Item1);
                    CellContent[m_SwappingCells.Item2].ContainingGem.MoveTo(m_SwappingCells.Item2);

                    CellContent[m_SwappingCells.Item1].IncomingGem = null;
                    CellContent[m_SwappingCells.Item2].IncomingGem = null;
                    
                    // блокировка снята — снова можно падать и удаляться
                    CellContent[m_SwappingCells.Item1].Locked = false;
                    CellContent[m_SwappingCells.Item2].Locked = false;

                    m_SwapStage = SwapStage.None;
                }
            }
        }

        /// <summary>
        /// true, если найден матч. При createMatch == false только проверка без создания матча
        /// (поиск возможных ходов по свайпу).
        /// </summary>
        bool DoCheck(Vector3Int startCell, bool createMatch = true)
        {
            // вызов с пустой ячейкой не ожидается, но на всякий случай
            if (!CellContent.TryGetValue(startCell, out var centerGem) || centerGem.ContainingGem == null)
                return false;

            // гем уже в другом матче — пропускаем
            if (centerGem.ContainingGem.CurrentMatch != null)
                return false;

            Vector3Int[] offsets = new[]
            {
                Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left
            };

            // сначала все связные гемы того же типа
            List<Vector3Int> gemList = new List<Vector3Int>();
            List<Vector3Int> checkedCells = new();

            Queue<Vector3Int> toCheck = new();
            toCheck.Enqueue(startCell);

            while (toCheck.Count > 0)
            {
                var current = toCheck.Dequeue();

                gemList.Add(current);
                checkedCells.Add(current);

                foreach (var dir in offsets)
                {
                    var nextCell = current + dir;

                    if (checkedCells.Contains(nextCell))
                        continue;

                    if (CellContent.TryGetValue(current + dir, out var content)
                        && content.CanMatch()
                        && content.ContainingGem.CurrentMatch == null
                        && content.ContainingGem.GemType == centerGem.ContainingGem.GemType)
                    {
                        toCheck.Enqueue(nextCell);
                    }
                }
            }

            // подбираем бонусные формы
            List<Vector3Int> temporaryShapeMatch = new();
            MatchShape matchedShape = null;
            List<BonusGem> matchedBonusGem = new();
            foreach (var bonusGem in GameManager.Instance.Settings.BonusSettings.Bonuses)
            {
                foreach (var shape in bonusGem.Shapes)
                {
                    if (shape.FitIn(gemList, ref temporaryShapeMatch))
                    {
                        if (matchedShape == null || matchedShape.Cells.Count < shape.Cells.Count)
                        {
                            matchedShape = shape;
                            // новая форма больше — сбрасываем список кандидатов в бонусы
                            matchedBonusGem.Clear();
                            matchedBonusGem.Add(bonusGem);
                        }
                        else if (matchedShape.Cells.Count == shape.Cells.Count)
                        {
                            // тот же размер, что у текущего кандидата — ещё один вариант бонуса
                            matchedBonusGem.Add(bonusGem);
                        }
                    }
                }
            }

            // далее — все линии из 3+ гемов
            List<Vector3Int> lineList = new();

            foreach (var idx in gemList)
            {
                // для каждого направления: если в сторону нет гема, это может быть конец линии —
                // считаем в противоположную сторону
                foreach (var dir in offsets)
                {
                    if (!gemList.Contains(idx + dir))
                    {
                        var currentList = new List<Vector3Int>() { idx };
                        var next = idx - dir;
                        while (gemList.Contains(next))
                        {
                            currentList.Add(next);
                            next -= dir;
                        }

                        if (currentList.Count >= 3)
                        {
                            lineList = currentList;
                        }
                    }
                }
            }

            // нет линий и бонусной фигуры — матча нет
            if (lineList.Count == 0 && temporaryShapeMatch.Count == 0)
                return false;

            if (createMatch)
            {
                var finalMatch = CreateCustomMatch(startCell);
                finalMatch.SpawnedBonus = matchedBonusGem.Count == 0 ? null : matchedBonusGem[Random.Range(0, matchedBonusGem.Count)];

                foreach (var cell in lineList)
                {
                    if (m_MatchedCallback.TryGetValue(cell, out var clbk))
                        clbk.Invoke();

                    if(CellContent[cell].CanDelete())
                        finalMatch.AddGem(CellContent[cell].ContainingGem);
                }

                foreach (var cell in temporaryShapeMatch)
                {
                    if (m_MatchedCallback.TryGetValue(cell, out var clbk))
                        clbk.Invoke();
                    
                    if(CellContent[cell].CanDelete())
                        finalMatch.AddGem(CellContent[cell].ContainingGem);
                }

                UIHandler.Instance.TriggerCharacterAnimation(UIHandler.CharacterAnimation.Match);
            }

            return true;
        }

        void CheckInput()
        {
            if (!m_InputEnabled)
                return;
            
            var mainCam = Camera.main;
        
            var pressedThisFrame = GameManager.Instance.ClickAction.WasPressedThisFrame();
            var releasedThisFrame = GameManager.Instance.ClickAction.WasReleasedThisFrame();
        
            var clickPos = GameManager.Instance.ClickPosition.ReadValue<Vector2>();
            var worldPos = mainCam.ScreenToWorldPoint(clickPos);
            worldPos.z = 0;
            
            if (m_HoldTrailInstance != null && m_HoldTrailInstance.gameObject.activeSelf)
            {
                m_HoldTrailInstance.transform.position = worldPos;
            }
        
            if (pressedThisFrame)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // открыто меню отладки — ставим выбранный гем в ячейку клика
                if (UIHandler.Instance.DebugMenuOpen)
                {
                    if (UIHandler.Instance.SelectedDebugGem != null)
                    {
                        var clickedCell = m_Grid.WorldToCell(Camera.main.ScreenToWorldPoint(clickPos));
                        if (CellContent.TryGetValue(clickedCell, out var cellContent))
                        {
                            if (cellContent.ContainingGem != null)
                            {
                                Destroy(cellContent.ContainingGem.gameObject);
                            }

                            NewGemAt(clickedCell, UIHandler.Instance.SelectedDebugGem);
                        }
                    }
                
                    return;
                }
#endif
                // активирован бонус — клик по полю применяет его
                if (m_ActivatedBonus != null)
                {
                    var clickedCell = m_Grid.WorldToCell(mainCam.ScreenToWorldPoint(clickPos));
                    if (CellContent.TryGetValue(clickedCell, out var content) && content.ContainingGem != null)
                    {
                        GameManager.Instance.UseBonusItem(m_ActivatedBonus, clickedCell);
                        m_ActivatedBonus = null;
                        return;
                    }
                }
            
                m_StartClickPosition = clickPos;
                
                var worldStart = mainCam.ScreenToWorldPoint(m_StartClickPosition);
                var startCell = m_Grid.WorldToCell(worldStart);

                if (CellContent.ContainsKey(startCell))
                {
                    if (m_GemHoldVFXInstance != null)
                    {
                        m_GemHoldVFXInstance.transform.position = m_Grid.GetCellCenterWorld(startCell);
                        m_GemHoldVFXInstance.gameObject.SetActive(true);
                    }

                    if (m_HoldTrailInstance)
                    {
                        m_HoldTrailInstance.transform.position = worldPos;
                        m_HoldTrailInstance.gameObject.SetActive(true);
                    }
                }
            }
            else if (releasedThisFrame)
            {
                // m_IsHoldingTouch = false;
                if(m_GemHoldVFXInstance != null) m_GemHoldVFXInstance.gameObject.SetActive(false);
                if(m_HoldTrailInstance != null) m_HoldTrailInstance.gameObject.SetActive(false);
                
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (UIHandler.Instance.DebugMenuOpen)
                {
                    return;
                }
#endif
                // уже есть очередь или идёт обмен — второй свайп не обрабатываем
                if (m_SwipeQueued || m_SwapStage != SwapStage.None)
                    return;
                
                float clickDelta = Time.time - m_LastClickTime;
                m_LastClickTime = Time.time;

                var worldStart = mainCam.ScreenToWorldPoint(m_StartClickPosition);
                var startCell = m_Grid.WorldToCell(worldStart);
                startCell.z = 0;
            
                // меньше 0,3 с с прошлого клика — двойной клик; если это гем с Usable — активировать
                if (clickDelta < 0.3f)
                {
                    if (CellContent.TryGetValue(startCell, out var content) 
                        && content.ContainingGem != null 
                        && content.ContainingGem.Usable
                        && content.ContainingGem.CurrentMatch == null)
                    {
                        content.ContainingGem.Use(null);
                        return;
                    }
                }

                var endWorldPos = mainCam.ScreenToWorldPoint(clickPos);
            
                // свайп в мировых координатах: смещение на 1 — расстояние между центрами соседних ячеек
                var swipe = endWorldPos - worldStart;
                if (swipe.sqrMagnitude < 0.5f * 0.5f)
                {
                    return;
                }

                // начальная ячейка не на доске — выход
                if (!CellContent.TryGetValue(startCell, out var startCellContent) 
                    || !startCellContent.CanBeMoved)
                {
                    return;
                }

                var endCell = startCell;
            
                if (Mathf.Abs(swipe.x) > Mathf.Abs(swipe.y))
                {
                    if (swipe.x < 0)
                    {
                        endCell += Vector3Int.left;
                    }
                    else
                    {
                        endCell += Vector3Int.right;
                    }
                }
                else
                {
                    if (swipe.y > 0)
                    {
                        endCell += Vector3Int.up;
                    }
                    else
                    {
                        endCell += Vector3Int.down;
                    }
                }

                // конечная ячейка не на доске — выход
                if (!CellContent.TryGetValue(endCell, out var endCellContent) || !endCellContent.CanBeMoved)
                    return;
                
                // обе ячейки валидны — блокируем до конца обмена
                startCellContent.Locked = true;
                endCellContent.Locked = true;
                
                // убираем ячейки из тикающих, если свайпнули падающий гем

                m_SwipeQueued = true;
                m_StartSwipe = startCell;
                m_EndSwipe = endCell;
            }
        }

        public void ActivateBonusItem(BonusItem item)
        {
            m_ActivatedBonus = item;
        }

        [Button("Решафл доски (дебаг)", EButtonEnableMode.Playmode)]
        private void DebugReshuffleBoard()
        {
            if (!m_BoardWasInit)
            {
                Debug.LogWarning("[Board] Доска ещё не инициализирована.");
                return;
            }

            if (m_SwapStage != SwapStage.None)
            {
                Debug.LogWarning("[Board] Дождитесь окончания обмена гемов.");
                return;
            }

            if (m_TickingCells.Count > 0 || m_NewTickingCells.Count > 0 || m_TickingMatch.Count > 0 ||
                m_CellToMatchCheck.Count > 0)
            {
                Debug.LogWarning("[Board] Дождитесь стабилизации доски (нет падений, матчей и отложенных проверок матча).");
                return;
            }

            if (m_ReshuffleAnimationInProgress)
            {
                Debug.LogWarning("[Board] Решафл уже выполняется.");
                return;
            }

            StartCoroutine(DebugReshuffleRoutine());
        }

        IEnumerator BoardSettledReshuffleRoutine()
        {
            m_ReshuffleAnimationInProgress = true;
            ToggleInput(false);
            yield return RunReshufflePasses(forceReshuffleEvenIfMovesExist: false);
            ToggleInput(true);
            m_ReshuffleAnimationInProgress = false;
            BattleFlowCoordinator.Instance.OnBoardSettled();
        }

        IEnumerator DebugReshuffleRoutine()
        {
            m_ReshuffleAnimationInProgress = true;
            ToggleInput(false);
            yield return RunReshufflePasses(forceReshuffleEvenIfMovesExist: true);
            ToggleInput(true);
            m_ReshuffleAnimationInProgress = false;
        }

        /// <summary>
        /// Пересчитывает возможные ходы; при отсутствии ходов — решафл с анимацией (до лимита попыток).
        /// При force — минимум один решафл даже если ходы уже есть (дебаг).
        /// </summary>
        IEnumerator RunReshufflePasses(bool forceReshuffleEvenIfMovesExist)
        {
            FindAllPossibleMatch();
            int reshuffleAttempts = 0;

            if (forceReshuffleEvenIfMovesExist && reshuffleAttempts < MaxBoardReshuffleAttempts)
            {
                if (TryPrepareReshuffleMovableGems(out List<Vector3Int> movableCellsForce, out List<Gem> shuffledGemsForce,
                        out Dictionary<Gem, Vector3> startWorldByGemForce))
                {
                    ApplyReshuffleAssignment(movableCellsForce, shuffledGemsForce, startWorldByGemForce);
                    Tween tweenForce = CreateReshuffleAnimationTween(movableCellsForce, shuffledGemsForce, startWorldByGemForce);
                    reshuffleAttempts++;
                    if (tweenForce != null && tweenForce.IsActive())
                        yield return tweenForce.WaitForCompletion(true);
                    FindAllPossibleMatch();
                }
            }

            while (m_PossibleSwaps.Count == 0 && reshuffleAttempts < MaxBoardReshuffleAttempts)
            {
                if (!TryPrepareReshuffleMovableGems(out List<Vector3Int> movableCells, out List<Gem> shuffledGems,
                        out Dictionary<Gem, Vector3> startWorldByGem))
                    break;

                ApplyReshuffleAssignment(movableCells, shuffledGems, startWorldByGem);
                Tween tween = CreateReshuffleAnimationTween(movableCells, shuffledGems, startWorldByGem);
                reshuffleAttempts++;
                if (tween != null && tween.IsActive())
                    yield return tween.WaitForCompletion(true);
                FindAllPossibleMatch();
            }
        }

        bool TryPrepareReshuffleMovableGems(out List<Vector3Int> movableCells, out List<Gem> shuffledGems,
            out Dictionary<Gem, Vector3> startWorldByGem)
        {
            movableCells = new List<Vector3Int>();
            for (int y = m_BoundsInt.yMin; y <= m_BoundsInt.yMax; ++y)
            {
                for (int x = m_BoundsInt.xMin; x <= m_BoundsInt.xMax; ++x)
                {
                    var cellIndex = new Vector3Int(x, y, 0);
                    if (!CellContent.TryGetValue(cellIndex, out var cell))
                        continue;
                    if (!cell.CanBeMoved || cell.IncomingGem != null)
                        continue;
                    if (cell.ContainingGem == null || cell.ContainingGem.CurrentMatch != null)
                        continue;

                    movableCells.Add(cellIndex);
                }
            }

            if (movableCells.Count < 2)
            {
                shuffledGems = null;
                startWorldByGem = null;
                return false;
            }

            var gemsPool = new List<Gem>(movableCells.Count);
            startWorldByGem = new Dictionary<Gem, Vector3>();
            foreach (Vector3Int cellIndex in movableCells)
            {
                Gem gem = CellContent[cellIndex].ContainingGem;
                gemsPool.Add(gem);
                startWorldByGem[gem] = gem.transform.position;
            }

            var movableCellSet = new HashSet<Vector3Int>(movableCells);

            for (int attempt = 0; attempt < MaxNoMatchReshuffleBuildAttempts; attempt++)
            {
                var remainingGems = new List<Gem>(gemsPool);
                for (int shuffleIndex = remainingGems.Count - 1; shuffleIndex > 0; shuffleIndex--)
                {
                    int randomPick = Random.Range(0, shuffleIndex + 1);
                    (remainingGems[shuffleIndex], remainingGems[randomPick]) =
                        (remainingGems[randomPick], remainingGems[shuffleIndex]);
                }

                var simulationPlacements = new Dictionary<Vector3Int, Gem>();
                bool placementFailed = false;

                foreach (Vector3Int cellIndex in movableCells)
                {
                    Gem GemAccessorForReshuffleSimulation(Vector3Int worldCell)
                    {
                        if (simulationPlacements.TryGetValue(worldCell, out Gem placedGem))
                            return placedGem;
                        if (movableCellSet.Contains(worldCell))
                            return null;
                        return CellContent.TryGetValue(worldCell, out BoardCell boardCell) ? boardCell.ContainingGem : null;
                    }

                    m_ScratchGemTypesForReshuffle.Clear();
                    foreach (Gem gem in remainingGems)
                        m_ScratchGemTypesForReshuffle.Add(gem.GemType);

                    var allowedTypesForCell = new List<int>(m_ScratchGemTypesForReshuffle);
                    RemoveGemTypesThatWouldCreateImmediateMatchAt(cellIndex, allowedTypesForCell, GemAccessorForReshuffleSimulation);

                    var candidateGems = new List<Gem>();
                    foreach (Gem gem in remainingGems)
                    {
                        if (allowedTypesForCell.Contains(gem.GemType))
                            candidateGems.Add(gem);
                    }

                    if (candidateGems.Count == 0)
                    {
                        placementFailed = true;
                        break;
                    }

                    Gem chosenGem = candidateGems[Random.Range(0, candidateGems.Count)];
                    simulationPlacements[cellIndex] = chosenGem;
                    remainingGems.Remove(chosenGem);
                }

                if (placementFailed || remainingGems.Count > 0)
                    continue;

                shuffledGems = new List<Gem>(movableCells.Count);
                foreach (Vector3Int cellIndex in movableCells)
                    shuffledGems.Add(simulationPlacements[cellIndex]);

                return true;
            }

            shuffledGems = null;
            return false;
        }

        void ApplyReshuffleAssignment(List<Vector3Int> movableCells, List<Gem> shuffledGems, Dictionary<Gem, Vector3> startWorldByGem)
        {
            for (int i = 0; i < movableCells.Count; i++)
            {
                Vector3Int cellIndex = movableCells[i];
                Gem gem = shuffledGems[i];
                CellContent[cellIndex].ContainingGem = gem;
                gem.MoveTo(cellIndex);
                gem.transform.position = startWorldByGem[gem];
            }
        }

        Tween CreateReshuffleAnimationTween(List<Vector3Int> movableCells, List<Gem> shuffledGems, Dictionary<Gem, Vector3> startWorldByGem)
        {
            DOTween.Kill(this, false);

            Sequence masterSequence = DOTween.Sequence().SetId(this);
            bool anyMovement = false;

            for (int i = 0; i < movableCells.Count; i++)
            {
                Gem gem = shuffledGems[i];
                Transform gemTransform = gem.transform;
                Vector3 startWorld = startWorldByGem[gem];
                Vector3 endWorld = m_Grid.GetCellCenterWorld(movableCells[i]);

                if ((startWorld - endWorld).sqrMagnitude < 0.00002f)
                    continue;

                anyMovement = true;
                DOTween.Kill(gemTransform, false);

                float distance = Vector3.Distance(startWorld, endWorld);
                float arcLift = Mathf.Clamp(distance * ReshuffleArcHeightFactor, ReshuffleArcHeightMin, ReshuffleArcHeightMax);
                Vector3 midPoint = Vector3.Lerp(startWorld, endWorld, 0.5f) + Vector3.up * arcLift;

                Sequence gemSequence = DOTween.Sequence();
                gemSequence.Append(gemTransform.DOMove(midPoint, ReshuffleLiftPhaseSeconds).SetEase(Ease.OutQuad));
                gemSequence.Append(gemTransform.DOMove(endWorld, ReshuffleFlyPhaseSeconds).SetEase(Ease.OutBack, 1.1f));
                gemSequence.Join(gemTransform.DOScale(ReshuffleScalePulse, ReshuffleLiftPhaseSeconds * 0.55f)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetEase(Ease.OutQuad));
                gemSequence.OnComplete(() =>
                {
                    if (gem != null && gemTransform != null)
                        gemTransform.localScale = Vector3.one;
                });

                masterSequence.Insert(ReshuffleStaggerSeconds * i, gemSequence);
            }

            if (!anyMovement)
                return null;

            return masterSequence;
        }

        void FindAllPossibleMatch()
        {
            // TODO: вместо полного обхода — только сдвинутые гемы (оптимизация)
        
            m_PossibleSwaps.Clear();
        
            // двойной цикл по возрастанию x, y вместо произвольного обхода словаря
            // достаточно проверять обмен вверх и вправо: вниз и влево уже покрыты соседними ячейками

            for (int y = m_BoundsInt.yMin; y <= m_BoundsInt.yMax; ++y)
            {
                for (int x = m_BoundsInt.xMin; x <= m_BoundsInt.xMax; ++x)
                {
                    var idx = new Vector3Int(x, y, 0);
                    if (CellContent.TryGetValue(idx, out var cell) && cell.CanBeMoved)
                    {
                        var topIdx = idx + Vector3Int.up;
                        var rightIdx = idx + Vector3Int.right;
                    
                        if (CellContent.TryGetValue(topIdx, out var topCell) && topCell.CanBeMoved)
                        {
                            // временно меняем гемы местами
                            (CellContent[idx].ContainingGem, CellContent[topIdx].ContainingGem) = (
                                CellContent[topIdx].ContainingGem, CellContent[idx].ContainingGem);
                        
                            if (DoCheck(topIdx, false))
                            {
                                m_PossibleSwaps.Add(new PossibleSwap()
                                {
                                    StartPosition = idx,
                                    Direction = Vector3Int.up
                                });
                            }

                            if (DoCheck(idx, false))
                            {
                                m_PossibleSwaps.Add(new PossibleSwap()
                                {
                                    StartPosition = topIdx,
                                    Direction = Vector3Int.down
                                });
                            }
                        
                            // возвращаем как было
                            (CellContent[idx].ContainingGem, CellContent[topIdx].ContainingGem) = (
                                CellContent[topIdx].ContainingGem, CellContent[idx].ContainingGem);
                        }
                    
                        if (CellContent.TryGetValue(rightIdx, out var rightCell) && rightCell.CanBeMoved)
                        {
                            // временно меняем гемы местами
                            (CellContent[idx].ContainingGem, CellContent[rightIdx].ContainingGem) = (
                                CellContent[rightIdx].ContainingGem, CellContent[idx].ContainingGem);
                        
                            if (DoCheck(rightIdx, false))
                            {
                                m_PossibleSwaps.Add(new PossibleSwap()
                                {
                                    StartPosition = idx,
                                    Direction = Vector3Int.right
                                });
                            }

                            if (DoCheck(idx, false))
                            {
                                m_PossibleSwaps.Add(new PossibleSwap()
                                {
                                    StartPosition = rightIdx,
                                    Direction = Vector3Int.left
                                });
                            }
                        
                            // возвращаем как было
                            (CellContent[idx].ContainingGem, CellContent[rightIdx].ContainingGem) = (
                                CellContent[rightIdx].ContainingGem, CellContent[idx].ContainingGem);
                        }
                    }
                }
            }


            if (m_PossibleSwaps.Count > 0)
            {
                m_PickedSwap = Random.Range(0, m_PossibleSwaps.Count);
            }
        }
    }
}