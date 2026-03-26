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
            // ゴール配置
            if (goalPrefab != null)
            {
                var goalPos = currentLevelData.goal.ToVector2Int();
                var goalObj = Instantiate(goalPrefab, GridManager.Instance.GridToWorld(goalPos), Quaternion.identity);
                GridManager.Instance.SetCell(goalPos, CellType.Goal, goalObj);
            }
            else
            {
                GridManager.Instance.SetCell(currentLevelData.goal.ToVector2Int(), CellType.Goal);
            }

            // 柴犬配置
            var shibaPos = currentLevelData.shiba.ToVector2Int();
            var shibaObj = shibaPrefab != null
                ? Instantiate(shibaPrefab, GridManager.Instance.GridToWorld(shibaPos), Quaternion.identity)
                : CreatePlaceholder("Shiba", Color.yellow, shibaPos);
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
                    : CreatePlaceholder("Obstacle", Color.gray, pos);
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
                var color = enemyData.type == "thief" ? Color.blue : Color.red;
                var obj = prefab != null
                    ? Instantiate(prefab, GridManager.Instance.GridToWorld(pos), Quaternion.identity)
                    : CreatePlaceholder($"Enemy_{enemyData.type}", color, pos);
                var enemy = obj.GetComponent<EnemyController>();
                if (enemy == null) enemy = obj.AddComponent<EnemyController>();
                enemy.Initialize(pos, enemyData.ToEnemyType());
                enemies.Add(enemy);
            }
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
            obstacles.Clear();
            enemies.Clear();

            // ゴールオブジェクト削除
            if (currentLevelData != null)
            {
                var goalObj = GridManager.Instance.GetOccupant(currentLevelData.goal.ToVector2Int());
                if (goalObj != null) Destroy(goalObj);
            }
        }

        private GameObject CreatePlaceholder(string name, Color color, Vector2Int pos)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.position = GridManager.Instance.GridToWorld(pos);
            var renderer = obj.GetComponent<Renderer>();
            renderer.material.color = color;
            return obj;
        }
    }
}
