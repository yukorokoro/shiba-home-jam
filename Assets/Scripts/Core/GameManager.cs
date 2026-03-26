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
            SpawnEntities();
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
            // グリッド床面を生成
            SpawnGridFloor();

            // ゴール配置
            var goalPos = currentLevelData.goal.ToVector2Int();
            if (goalPrefab != null)
            {
                var goalObj = Instantiate(goalPrefab, GridManager.Instance.GridToWorld(goalPos), Quaternion.identity);
                GridManager.Instance.SetCell(goalPos, CellType.Goal, goalObj);
            }
            else
            {
                var goalObj = CreatePlaceholder("Goal", new Color(0.2f, 0.8f, 0.2f), goalPos);
                goalObj.transform.localScale = new Vector3(0.9f, 0.1f, 0.9f);
                goalObj.transform.position += Vector3.up * (-0.4f);
                GridManager.Instance.SetCell(goalPos, CellType.Goal, goalObj);
            }

            // 柴犬配置
            var shibaPos = currentLevelData.shiba.ToVector2Int();
            var shibaObj = shibaPrefab != null
                ? Instantiate(shibaPrefab, GridManager.Instance.GridToWorld(shibaPos), Quaternion.identity)
                : CreatePlaceholder("Shiba", new Color(0.9f, 0.7f, 0.3f), shibaPos);
            shiba = shibaObj.GetComponent<ShibaController>();
            if (shiba == null) shiba = shibaObj.AddComponent<ShibaController>();
            shiba.Initialize(shibaPos);
            shiba.OnReachedGoal += HandleLevelClear;
            shiba.OnCaught += HandleGameOver;

            // 障害物配置
            foreach (var obsData in currentLevelData.obstacles)
            {
                var pos = obsData.ToVector2Int();
                var obj = obstaclePrefab != null
                    ? Instantiate(obstaclePrefab, GridManager.Instance.GridToWorld(pos), Quaternion.identity)
                    : CreatePlaceholder("Obstacle", new Color(0.45f, 0.35f, 0.25f), pos);
                var obs = obj.GetComponent<ObstacleController>();
                if (obs == null) obs = obj.AddComponent<ObstacleController>();
                obs.Initialize(pos);
                obstacles.Add(obs);
            }

            // 敵配置
            foreach (var enemyData in currentLevelData.enemies)
            {
                var pos = enemyData.position.ToVector2Int();
                var prefab = enemyData.type == "thief" ? thiefPrefab : catPrefab;
                var color = enemyData.type == "thief" ? new Color(0.3f, 0.3f, 0.7f) : new Color(0.8f, 0.2f, 0.2f);
                var obj = prefab != null
                    ? Instantiate(prefab, GridManager.Instance.GridToWorld(pos), Quaternion.identity)
                    : CreatePlaceholder($"Enemy_{enemyData.type}", color, pos);
                var enemy = obj.GetComponent<EnemyController>();
                if (enemy == null) enemy = obj.AddComponent<EnemyController>();
                enemy.Initialize(pos, enemyData.ToEnemyType());
                enemies.Add(enemy);
            }

            // カメラをグリッド中心に合わせる
            FitCamera();
        }

        private void HandleInput()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;

            var press = pointer.press;
            var position = pointer.position.ReadValue();

            if (press.wasPressedThisFrame)
            {
                pressing = true;
                touchStart = position;
                selectedObstacle = RaycastObstacle(position);
            }
            else if (press.wasReleasedThisFrame && pressing && selectedObstacle != null)
            {
                pressing = false;
                var dir = ObstacleController.DetectSwipeDirection(touchStart, position);
                if (dir.HasValue && selectedObstacle.TrySlide(dir.Value))
                {
                    AdvanceTurn();
                }
                selectedObstacle = null;
            }
        }

        private ObstacleController RaycastObstacle(Vector2 screenPos)
        {
            var ray = Camera.main.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit))
                return hit.collider.GetComponent<ObstacleController>();
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

        private void FitCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var gm = GridManager.Instance;
            // グリッド中心を計算
            float cx = (gm.Width - 1) * 0.5f;
            float cz = (gm.Height - 1) * 0.5f;

            // 真上から見下ろし（少し斜め）
            cam.transform.position = new Vector3(cx, 10f, cz - 3f);
            cam.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(gm.Width, gm.Height) * 0.7f;
        }

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
                        ? new Color(0.75f, 0.85f, 0.65f)
                        : new Color(0.82f, 0.9f, 0.72f);

                    // タイルにColliderは不要（Raycastの邪魔になる）
                    Destroy(tile.GetComponent<Collider>());

                    spawnedObjects.Add(tile);
                }
            }
        }

        private GameObject CreatePlaceholder(string name, Color color, Vector2Int pos)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.position = GridManager.Instance.GridToWorld(pos);
            obj.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            var renderer = obj.GetComponent<Renderer>();
            renderer.material.color = color;
            return obj;
        }
    }
}
