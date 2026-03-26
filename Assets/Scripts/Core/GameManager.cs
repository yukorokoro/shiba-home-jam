using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

namespace ShibaHomeJam.Core
{
    public enum GameState
    {
        Loading,
        Playing,
        Paused,
        Clear,
        GameOver
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private GameObject shibaPrefab;
        [SerializeField] private GameObject obstaclePrefab;
        [SerializeField] private GameObject catPrefab;
        [SerializeField] private GameObject thiefPrefab;
        [SerializeField] private GameObject goalPrefab;

        public GameState State { get; private set; } = GameState.Loading;
        public int CurrentLevel { get; private set; } = 1;
        public int TurnCount { get; private set; }

        public event Action<GameState> OnStateChanged;
        public event Action<int> OnTurnAdvanced;

        private ShibaController shiba;
        private List<ObstacleController> obstacles = new List<ObstacleController>();
        private List<EnemyController> enemies = new List<EnemyController>();
        private List<GameObject> spawnedObjects = new List<GameObject>();
        private LevelData currentLevelData;

        // 入力用
        private Vector2 touchStart;
        private ObstacleController selectedObstacle;
        private bool pressing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            LoadLevel(CurrentLevel);
        }

        private void Update()
        {
            if (State != GameState.Playing) return;
            HandleInput();
        }

        public void LoadLevel(int levelNumber)
        {
            ClearLevel();
            CurrentLevel = levelNumber;
            TurnCount = 0;

            currentLevelData = LevelLoader.LoadLevel(levelNumber);
            if (currentLevelData == null)
            {
                Debug.LogError($"Failed to load level {levelNumber}");
                return;
            }

            GridManager.Instance.InitializeGrid(currentLevelData.width, currentLevelData.height);
            SpawnGridFloor();
            SpawnEntities();
            FitCamera();
            SetState(GameState.Playing);
        }

        public void RestartLevel()
        {
            LoadLevel(CurrentLevel);
        }

        public void NextLevel()
        {
            LoadLevel(CurrentLevel + 1);
        }

        private void SpawnEntities()
        {
            // ゴール配置 (Blue cube + roof)
            var goalPos = currentLevelData.goal.ToVector2Int();
            if (goalPrefab != null)
            {
                var goalObj = Instantiate(goalPrefab, GridManager.Instance.GridToWorld(goalPos), Quaternion.identity);
                GridManager.Instance.SetCell(goalPos, CellType.Goal, goalObj);
            }
            else
            {
                var goalObj = CreateGoalPlaceholder(goalPos);
                GridManager.Instance.SetCell(goalPos, CellType.Goal, goalObj);
            }

            // 柴犬配置 (Yellow sphere)
            var shibaPos = currentLevelData.shiba.ToVector2Int();
            GameObject shibaObj;
            if (shibaPrefab != null)
            {
                shibaObj = Instantiate(shibaPrefab, GridManager.Instance.GridToWorld(shibaPos), Quaternion.identity);
            }
            else
            {
                shibaObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shibaObj.name = "Shiba";
                shibaObj.transform.position = GridManager.Instance.GridToWorld(shibaPos);
                shibaObj.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
                shibaObj.GetComponent<Renderer>().material.color = new Color(1f, 0.85f, 0.2f); // Yellow
                // 柴犬のColliderを除去（Raycastで障害物だけを拾うため）
                Destroy(shibaObj.GetComponent<Collider>());
            }
            shiba = shibaObj.GetComponent<ShibaController>();
            if (shiba == null) shiba = shibaObj.AddComponent<ShibaController>();
            shiba.Initialize(shibaPos);
            shiba.OnReachedGoal += HandleLevelClear;
            shiba.OnCaught += HandleGameOver;

            // 障害物配置 (Gray cube)
            foreach (var obsData in currentLevelData.obstacles)
            {
                var pos = obsData.ToVector2Int();
                GameObject obj;
                if (obstaclePrefab != null)
                {
                    obj = Instantiate(obstaclePrefab, GridManager.Instance.GridToWorld(pos), Quaternion.identity);
                }
                else
                {
                    obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.name = "Obstacle";
                    obj.transform.position = GridManager.Instance.GridToWorld(pos);
                    obj.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
                    obj.GetComponent<Renderer>().material.color = new Color(0.6f, 0.6f, 0.6f); // Gray
                    // BoxColliderはそのまま残す（Raycast用）
                }
                var obs = obj.GetComponent<ObstacleController>();
                if (obs == null) obs = obj.AddComponent<ObstacleController>();
                obs.Initialize(pos);
                obstacles.Add(obs);
            }

            // 敵配置
            foreach (var enemyData in currentLevelData.enemies)
            {
                var pos = enemyData.position.ToVector2Int();
                GameObject obj;
                bool isThief = enemyData.type == "thief";

                if ((isThief ? thiefPrefab : catPrefab) != null)
                {
                    obj = Instantiate(isThief ? thiefPrefab : catPrefab,
                        GridManager.Instance.GridToWorld(pos), Quaternion.identity);
                }
                else
                {
                    obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.name = isThief ? "Enemy_thief" : "Enemy_cat";
                    obj.transform.position = GridManager.Instance.GridToWorld(pos);
                    obj.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
                    obj.GetComponent<Renderer>().material.color = isThief
                        ? new Color(0.6f, 0.2f, 0.8f)   // Purple for thief
                        : new Color(1f, 0.5f, 0.1f);     // Orange for cat
                    // 敵のColliderを除去（Raycastで障害物だけを拾うため）
                    Destroy(obj.GetComponent<Collider>());
                }
                var enemy = obj.GetComponent<EnemyController>();
                if (enemy == null) enemy = obj.AddComponent<EnemyController>();
                enemy.Initialize(pos, enemyData.ToEnemyType());
                enemies.Add(enemy);
            }
        }

        private void HandleInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            var position = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressing = true;
                touchStart = position;
                selectedObstacle = RaycastObstacle(position);
            }
            else if (mouse.leftButton.wasReleasedThisFrame && pressing)
            {
                pressing = false;
                if (selectedObstacle != null)
                {
                    var delta = position - touchStart;
                    Vector2Int? dir;

                    // クリック（ほぼ動かさない）の場合は右にスライド
                    if (delta.magnitude < 15f)
                    {
                        dir = Vector2Int.right;
                    }
                    else
                    {
                        dir = ObstacleController.DetectSwipeDirection(touchStart, position, 15f);
                    }

                    if (dir.HasValue && selectedObstacle.TrySlide(dir.Value))
                    {
                        AdvanceTurn();
                    }
                    selectedObstacle = null;
                }
            }

            // タッチ入力対応
            if (Touchscreen.current != null)
            {
                var touchscreen = Touchscreen.current;
                if (touchscreen.primaryTouch.press.wasPressedThisFrame)
                {
                    var touchPos = touchscreen.primaryTouch.position.ReadValue();
                    pressing = true;
                    touchStart = touchPos;
                    selectedObstacle = RaycastObstacle(touchPos);
                }
                else if (touchscreen.primaryTouch.press.wasReleasedThisFrame && pressing)
                {
                    pressing = false;
                    if (selectedObstacle != null)
                    {
                        var touchPos = touchscreen.primaryTouch.position.ReadValue();
                        var dir = ObstacleController.DetectSwipeDirection(touchStart, touchPos, 15f);
                        if (dir.HasValue && selectedObstacle.TrySlide(dir.Value))
                        {
                            AdvanceTurn();
                        }
                        selectedObstacle = null;
                    }
                }
            }
        }

        private ObstacleController RaycastObstacle(Vector2 screenPos)
        {
            var cam = Camera.main;
            if (cam == null) return null;

            var ray = cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                return hit.collider.GetComponent<ObstacleController>();
            }
            return null;
        }

        private void AdvanceTurn()
        {
            TurnCount++;
            OnTurnAdvanced?.Invoke(TurnCount);

            // 柴犬の自動移動
            shiba.AutoMove();

            // 敵の移動
            foreach (var enemy in enemies)
            {
                bool caught = enemy.TryAdvance(shiba.GridPosition);
                if (caught)
                {
                    shiba.SetCaught();
                    HandleGameOver();
                    return;
                }
            }

            // ターン制限チェック
            if (currentLevelData.turnLimit > 0 && TurnCount >= currentLevelData.turnLimit)
            {
                HandleGameOver();
            }
        }

        private void HandleLevelClear()
        {
            SetState(GameState.Clear);
            Debug.Log($"Level {CurrentLevel} Clear! Turns: {TurnCount}");
        }

        private void HandleGameOver()
        {
            SetState(GameState.GameOver);
            Debug.Log($"Game Over at Level {CurrentLevel}, Turn {TurnCount}");
        }

        private void SetState(GameState newState)
        {
            State = newState;
            OnStateChanged?.Invoke(newState);
        }

        private void ClearLevel()
        {
            if (shiba != null)
            {
                shiba.OnReachedGoal -= HandleLevelClear;
                shiba.OnCaught -= HandleGameOver;
                Destroy(shiba.gameObject);
            }

            foreach (var obs in obstacles) if (obs != null) Destroy(obs.gameObject);
            foreach (var enemy in enemies) if (enemy != null) Destroy(enemy.gameObject);
            foreach (var obj in spawnedObjects) if (obj != null) Destroy(obj);
            obstacles.Clear();
            enemies.Clear();
            spawnedObjects.Clear();

            // ゴールオブジェクト削除
            if (currentLevelData != null)
            {
                var goalObj = GridManager.Instance.GetOccupant(currentLevelData.goal.ToVector2Int());
                if (goalObj != null) Destroy(goalObj);
            }
        }

        // ========== Camera ==========

        private void FitCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var gm = GridManager.Instance;
            float cx = (gm.Width - 1) * 0.5f;
            float cz = (gm.Height - 1) * 0.5f;

            // 真上から見下ろし
            cam.transform.position = new Vector3(cx, 10f, cz);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.orthographic = true;
            // グリッドが画面にフィットするサイズ (+1でパディング)
            cam.orthographicSize = Mathf.Max(gm.Width, gm.Height) * 0.5f + 1f;
        }

        // ========== Grid Floor ==========

        private void SpawnGridFloor()
        {
            var gm = GridManager.Instance;

            for (int x = 0; x < gm.Width; x++)
            {
                for (int y = 0; y < gm.Height; y++)
                {
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    tile.name = $"Tile_{x}_{y}";
                    tile.transform.position = new Vector3(x, -0.5f, y);
                    tile.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    tile.transform.localScale = new Vector3(0.95f, 0.95f, 1f);

                    var renderer = tile.GetComponent<Renderer>();
                    bool dark = (x + y) % 2 == 0;
                    renderer.material.color = dark
                        ? new Color(0.65f, 0.80f, 0.50f)  // Light green
                        : new Color(0.55f, 0.72f, 0.42f);  // Slightly darker green

                    // タイルのColliderを除去（Raycastの邪魔になる）
                    Destroy(tile.GetComponent<Collider>());

                    spawnedObjects.Add(tile);
                }
            }
        }

        // ========== Placeholder Factories ==========

        private GameObject CreateGoalPlaceholder(Vector2Int pos)
        {
            // 家: 青いベース + 三角屋根
            var parent = new GameObject("Goal");
            parent.transform.position = GridManager.Instance.GridToWorld(pos);

            // ベース（青い箱）
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.name = "GoalBase";
            baseObj.transform.SetParent(parent.transform);
            baseObj.transform.localPosition = Vector3.zero;
            baseObj.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
            baseObj.GetComponent<Renderer>().material.color = new Color(0.3f, 0.5f, 0.9f); // Blue
            Destroy(baseObj.GetComponent<Collider>());

            // 屋根（三角形をシミュレート: 薄くて回転したCube）
            var roofObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roofObj.name = "GoalRoof";
            roofObj.transform.SetParent(parent.transform);
            roofObj.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            roofObj.transform.localScale = new Vector3(0.9f, 0.4f, 0.9f);
            roofObj.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            roofObj.GetComponent<Renderer>().material.color = new Color(0.2f, 0.35f, 0.75f); // Darker blue
            Destroy(roofObj.GetComponent<Collider>());

            return parent;
        }
    }
}
