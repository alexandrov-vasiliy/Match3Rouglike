#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Match3;

namespace Match3.Editor
{
    /// <summary>
    /// Editor utility для создания UI-префабов боя. Меню: Tools / Battle UI / Create Prefabs
    /// </summary>
    public static class BattleUIPrefabBuilder
    {
        private const string PlayerUIPath = "Assets/_Game/Player/UI";
        private const string EnemiesUIPath = "Assets/_Game/Enemies/UI";

        [MenuItem("Tools/Battle UI/Create All Prefabs")]
        public static void CreateAllPrefabs()
        {
            CreatePlayerHpBarPrefab();
            CreatePlayerArmorDisplayPrefab();
            CreatePlayerBloodDisplayPrefab();
            CreatePlayerCoinsDisplayPrefab();
            CreateEnemyHpBarPrefab();
            CreateEnemyIntentDisplayPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Battle UI prefabs created.");
        }

        private static GameObject CreateCanvasRoot(string name)
        {
            var root = new GameObject(name);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200, 30);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return root;
        }

        private static GameObject CreateTextChild(GameObject parent, string defaultText)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = go.AddComponent<Text>();
            text.text = defaultText;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            return go;
        }

        private static GameObject CreateSliderLayout(GameObject parent)
        {
            var background = new GameObject("Background");
            background.transform.SetParent(parent.transform, false);
            var bgRect = background.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.5f);
            bgRect.anchorMax = new Vector2(1, 0.5f);
            bgRect.sizeDelta = new Vector2(0, 6);
            bgRect.anchoredPosition = Vector2.zero;
            var bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(parent.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-5, 0);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.8f, 0.2f, 1f);

            var slider = parent.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.value = 100;
            slider.interactable = false;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(parent.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 0);
            textRect.pivot = new Vector2(0.5f, 0);
            textRect.anchoredPosition = new Vector2(0, -12);
            textRect.sizeDelta = new Vector2(0, 20);
            var text = textGo.AddComponent<Text>();
            text.text = "100/100";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            return parent;
        }

        private static void CreatePlayerHpBarPrefab()
        {
            var root = CreateCanvasRoot("PlayerHpBar");
            root.AddComponent<PlayerHpBarView>();
            CreateSliderLayout(root);
            EnsureDirectory(PlayerUIPath);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PlayerUIPath}/PlayerHpBar.prefab");
            Object.DestroyImmediate(root);
        }

        private static void CreatePlayerArmorDisplayPrefab()
        {
            var root = CreateCanvasRoot("PlayerArmorDisplay");
            root.AddComponent<PlayerArmorView>();
            CreateTextChild(root, "0");
            EnsureDirectory(PlayerUIPath);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PlayerUIPath}/PlayerArmorDisplay.prefab");
            Object.DestroyImmediate(root);
        }

        private static void CreatePlayerBloodDisplayPrefab()
        {
            var root = CreateCanvasRoot("PlayerBloodDisplay");
            root.AddComponent<PlayerBloodView>();
            CreateTextChild(root, "0");
            EnsureDirectory(PlayerUIPath);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PlayerUIPath}/PlayerBloodDisplay.prefab");
            Object.DestroyImmediate(root);
        }

        private static void CreatePlayerCoinsDisplayPrefab()
        {
            var root = CreateCanvasRoot("PlayerCoinsDisplay");
            root.AddComponent<PlayerCoinsView>();
            CreateTextChild(root, "0");
            EnsureDirectory(PlayerUIPath);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PlayerUIPath}/PlayerCoinsDisplay.prefab");
            Object.DestroyImmediate(root);
        }

        private static void CreateEnemyHpBarPrefab()
        {
            var root = CreateCanvasRoot("EnemyHpBar");
            root.AddComponent<EnemyHpBarView>();
            CreateSliderLayout(root);
            EnsureDirectory(EnemiesUIPath);
            PrefabUtility.SaveAsPrefabAsset(root, $"{EnemiesUIPath}/EnemyHpBar.prefab");
            Object.DestroyImmediate(root);
        }

        private static void CreateEnemyIntentDisplayPrefab()
        {
            var root = CreateCanvasRoot("EnemyIntentDisplay");
            root.AddComponent<EnemyIntentView>();
            CreateTextChild(root, "?");
            EnsureDirectory(EnemiesUIPath);
            PrefabUtility.SaveAsPrefabAsset(root, $"{EnemiesUIPath}/EnemyIntentDisplay.prefab");
            Object.DestroyImmediate(root);
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game"))
                AssetDatabase.CreateFolder("Assets", "_Game");
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Player"))
                AssetDatabase.CreateFolder("Assets/_Game", "Player");
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Player/UI"))
                AssetDatabase.CreateFolder("Assets/_Game/Player", "UI");
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Enemies"))
                AssetDatabase.CreateFolder("Assets/_Game", "Enemies");
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Enemies/UI"))
                AssetDatabase.CreateFolder("Assets/_Game/Enemies", "UI");
        }

        [MenuItem("Tools/Battle UI/Setup Player Prefab")]
        public static void SetupPlayerPrefab()
        {
            const string playerPath = "Assets/_Game/Player/Hero Knight/Player.prefab";
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
            if (playerPrefab == null)
            {
                Debug.LogError($"Player prefab not found at {playerPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(playerPath);
            if (root.GetComponentInChildren<PlayerStatsView>() != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.Log("Player prefab already has PlayerStatsView.");
                return;
            }

            var canvasGo = new GameObject("PlayerStatsCanvas");
            canvasGo.transform.SetParent(root.transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.anchorMin = new Vector2(0, 1);
            canvasRect.anchorMax = new Vector2(0, 1);
            canvasRect.pivot = new Vector2(0, 1);
            canvasRect.anchoredPosition = new Vector2(20, -20);
            canvasRect.sizeDelta = new Vector2(250, 120);

            var presenter = canvasGo.AddComponent<PlayerStatsView>();

            var hpBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlayerUIPath}/PlayerHpBar.prefab");
            var armorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlayerUIPath}/PlayerArmorDisplay.prefab");
            var bloodPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlayerUIPath}/PlayerBloodDisplay.prefab");
            var coinsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlayerUIPath}/PlayerCoinsDisplay.prefab");

            if (hpBarPrefab == null || armorPrefab == null || bloodPrefab == null || coinsPrefab == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogError("Run 'Tools/Battle UI/Create All Prefabs' first.");
                return;
            }

            var hpBar = (GameObject)PrefabUtility.InstantiatePrefab(hpBarPrefab, canvasGo.transform);
            hpBar.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);
            var armor = (GameObject)PrefabUtility.InstantiatePrefab(armorPrefab, canvasGo.transform);
            armor.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -35);
            var blood = (GameObject)PrefabUtility.InstantiatePrefab(bloodPrefab, canvasGo.transform);
            blood.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -70);
            var coins = (GameObject)PrefabUtility.InstantiatePrefab(coinsPrefab, canvasGo.transform);
            coins.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -105);

            PrefabUtility.SaveAsPrefabAsset(root, playerPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("Player prefab updated with Battle UI.");
        }

        [MenuItem("Tools/Battle UI/Setup Enemy Prefab")]
        public static void SetupEnemyPrefab()
        {
            const string enemyPath = "Assets/_Game/Enemies/Fire Worm/EnemyFireWorm.prefab";
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(enemyPath);
            if (enemyPrefab == null)
            {
                Debug.LogError($"Enemy prefab not found at {enemyPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(enemyPath);
            if (root.GetComponentInChildren<EnemyStatsView>() != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.Log("Enemy prefab already has EnemyStatsView.");
                return;
            }

            var canvasGo = new GameObject("EnemyStatsCanvas");
            canvasGo.transform.SetParent(root.transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(150, 50);
            canvasRect.localPosition = new Vector3(0, 0.8f, 0);
            canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            var hpBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemiesUIPath}/EnemyHpBar.prefab");
            var intentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemiesUIPath}/EnemyIntentDisplay.prefab");

            if (hpBarPrefab == null || intentPrefab == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogError("Run 'Tools/Battle UI/Create All Prefabs' first.");
                return;
            }

            var presenter = canvasGo.AddComponent<EnemyStatsView>();

            var hpBar = (GameObject)PrefabUtility.InstantiatePrefab(hpBarPrefab, canvasGo.transform);
            hpBar.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 15);
            var intent = (GameObject)PrefabUtility.InstantiatePrefab(intentPrefab, canvasGo.transform);
            intent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -10);

            PrefabUtility.SaveAsPrefabAsset(root, enemyPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("Enemy prefab updated with Battle UI.");
        }
    }
}
#endif
