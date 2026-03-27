using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace ShibaHomeJam.Core
{
    public enum GameState
    {
        Playing,
        Clear,
        GameOver
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; }
        public ShibaController Shiba { get; private set; }
        public int HomeCol { get; private set; }
        public int HomeRow { get; private set; }
        public float TimeRemaining { get; private set; }
        public int CurrentLevel { get; private set; } = 1;

        public event System.Action<GameState> OnStateChanged;

        private List<ObstacleController> obstacles = new List<ObstacleController>();
        private List<GameObject> allSpawned = new List<GameObject>();

        // Timer
        private float levelTime;
        private float timerLogInterval = 5f;
        private float timerLogTimer;

        // UI elements (runtime-created)
        private Canvas uiCanvas;
        private Text timerText;
        private Text levelText;
        private GameObject gameOverPanel;
        private GameObject clearPanel;
        private GameObject completePanel;
        private Button restartButton;

        // Input state
        private ObstacleController selectedObs;
        private Vector2 pointerStart;
        private bool pointerDown;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            CreateUI();
            LoadLevel(CurrentLevel);
        }

        private void Update()
        {
            if (State != GameState.Playing) return;

            TimeRemaining -= Time.deltaTime;
            UpdateTimerUI();

            timerLogTimer -= Time.deltaTime;
            if (timerLogTimer <= 0f)
            {
                timerLogTimer = timerLogInterval;
                Debug.Log($"Timer: {Mathf.CeilToInt(TimeRemaining)} seconds remaining");
            }

            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                Debug.Log("Time's Up! Game Over");
                if (Shiba != null) Shiba.Stop();
                SetState(GameState.GameOver);
                return;
            }

            HandleInput();
        }

        // ===================== Level Loading (data-driven) =====================

        public void LoadLevel(int levelNum)
        {
            ClearAll();
            CurrentLevel = levelNum;

            var data = LevelLoader.LoadLevel(levelNum);
            if (data == null)
            {
                Debug.LogError($"Level {levelNum} not found!");
                return;
            }

            GridManager.Instance.Init(data.width, data.height);

            // Home
            HomeCol = data.goal.x;
            HomeRow = data.goal.y;
            GridManager.Instance.Set(HomeCol, HomeRow, CellType.Home);
            SpawnHome(HomeCol, HomeRow);

            SpawnFloor(data.width, data.height);

            // Route
            var route = new Vector2Int[data.route.Length];
            for (int i = 0; i < data.route.Length; i++)
                route[i] = new Vector2Int(data.route[i].x, data.route[i].y);

            SpawnRouteMarkers(route);

            // Shiba
            Shiba = SpawnShiba(data.shiba.x, data.shiba.y, route);
            Shiba.OnReachedHome += OnLevelClear;

            // Obstacles
            foreach (var obs in data.obstacles)
                SpawnObstacle(obs.x, obs.y, obs.IsMovable);

            // Timer
            levelTime = data.timeLimit > 0 ? data.timeLimit : 30f;
            TimeRemaining = levelTime;
            timerLogTimer = 0f;

            FitCamera(data.width, data.height);
            HideAllPanels();
            UpdateLevelText();
            SetState(GameState.Playing);

            Debug.Log($"Level {levelNum} loaded. Timer: {levelTime}s. Route: {route.Length} cells.");
        }

        // ===================== Level Transitions =====================

        private void OnLevelClear()
        {
            SetState(GameState.Clear);

            // Check if next level exists
            var nextData = LevelLoader.LoadLevel(CurrentLevel + 1);
            if (nextData != null)
            {
                clearPanel.SetActive(true);
                StartCoroutine(AutoNextLevel());
            }
            else
            {
                completePanel.SetActive(true);
                Debug.Log("All Levels Complete!");
            }
        }

        private IEnumerator AutoNextLevel()
        {
            yield return new WaitForSeconds(2f);
            if (clearPanel != null) clearPanel.SetActive(false);
            LoadLevel(CurrentLevel + 1);
        }

        private void RetryLevel()
        {
            Debug.Log($"Retrying Level {CurrentLevel}");
            LoadLevel(CurrentLevel);
        }

        private void GoHome()
        {
            SceneManager.LoadScene("Home");
        }

        // ===================== UI Creation =====================

        private void CreateUI()
        {
            var canvasObj = new GameObject("GameUI_Canvas");
            uiCanvas = canvasObj.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiCanvas.sortingOrder = 100;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            canvasObj.AddComponent<GraphicRaycaster>();

            // Timer text — top center
            timerText = CreateText(canvasObj.transform, "TimerText", "30s", 64,
                new Vector2(0.3f, 0.9f), new Vector2(0.7f, 0.98f));
            timerText.gameObject.AddComponent<Outline>().effectColor = Color.black;

            // Level text — top left
            levelText = CreateText(canvasObj.transform, "LevelText", "Level 1", 36,
                new Vector2(0.02f, 0.9f), new Vector2(0.25f, 0.96f));
            levelText.alignment = TextAnchor.MiddleLeft;

            // Restart button — top left below level text
            restartButton = CreateButton(canvasObj.transform, "RestartBtn", "↺",
                new Vector2(0.02f, 0.83f), new Vector2(0.1f, 0.9f),
                new Color(0.5f, 0.5f, 0.5f), 36);
            restartButton.onClick.AddListener(RetryLevel);

            // Game Over panel
            gameOverPanel = CreateOverlayPanel(canvasObj.transform, "GameOverPanel",
                "Time's Up!", new Color(0.8f, 0.15f, 0.15f));
            var retryBtn = CreateButton(gameOverPanel.transform, "RetryBtn", "Retry",
                new Vector2(0.2f, 0.3f), new Vector2(0.48f, 0.4f),
                new Color(0.9f, 0.5f, 0.2f), 42);
            retryBtn.onClick.AddListener(RetryLevel);
            var homeBtn = CreateButton(gameOverPanel.transform, "HomeBtn", "Home",
                new Vector2(0.52f, 0.3f), new Vector2(0.8f, 0.4f),
                new Color(0.4f, 0.4f, 0.6f), 42);
            homeBtn.onClick.AddListener(GoHome);
            gameOverPanel.SetActive(false);

            // Clear panel
            clearPanel = CreateOverlayPanel(canvasObj.transform, "ClearPanel",
                "Level Clear!", new Color(0.2f, 0.7f, 0.3f));
            clearPanel.SetActive(false);

            // All Complete panel
            completePanel = CreateOverlayPanel(canvasObj.transform, "CompletePanel",
                "All Levels Complete!", new Color(1f, 0.8f, 0.2f));
            var completeHomeBtn = CreateButton(completePanel.transform, "HomeBtn", "Home",
                new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.4f),
                new Color(0.3f, 0.5f, 0.95f), 42);
            completeHomeBtn.onClick.AddListener(GoHome);
            completePanel.SetActive(false);
        }

        private Text CreateText(Transform parent, string name, string content, int fontSize,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = content;
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        private Button CreateButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color bgColor, int fontSize)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var img = obj.AddComponent<Image>();
            img.color = bgColor;
            var btn = obj.AddComponent<Button>();
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var txtObj = new GameObject("Text");
            txtObj.transform.SetParent(obj.transform, false);
            var txt = txtObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = label;
            var txtRect = txtObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            return btn;
        }

        private GameObject CreateOverlayPanel(Transform parent, string name,
            string message, Color messageColor)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0, 0, 0, 0.7f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var text = CreateText(panel.transform, "Message", message, 64,
                new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.7f));
            text.color = messageColor;

            return panel;
        }

        private void HideAllPanels()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (clearPanel != null) clearPanel.SetActive(false);
            if (completePanel != null) completePanel.SetActive(false);
        }

        private void UpdateLevelText()
        {
            if (levelText != null)
                levelText.text = $"Level {CurrentLevel}";
        }

        private void UpdateTimerUI()
        {
            if (timerText == null) return;

            int seconds = Mathf.CeilToInt(TimeRemaining);
            timerText.text = $"{seconds}s";

            if (TimeRemaining <= 5f)
            {
                timerText.color = Color.red;
                float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 8f);
                timerText.transform.localScale = Vector3.one * pulse;
            }
            else if (TimeRemaining <= 10f)
            {
                timerText.color = Color.red;
                timerText.transform.localScale = Vector3.one;
            }
            else
            {
                timerText.color = Color.white;
                timerText.transform.localScale = Vector3.one;
            }
        }

        // ===================== State =====================

        private void SetState(GameState s)
        {
            State = s;
            OnStateChanged?.Invoke(s);

            if (s == GameState.GameOver)
                gameOverPanel.SetActive(true);
        }

        // ===================== Input =====================

        private void HandleInput()
        {
            bool pressedThisFrame = false;
            bool releasedThisFrame = false;
            Vector2 pointerPos = Vector2.zero;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                pointerPos = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame) pressedThisFrame = true;
                if (mouse.leftButton.wasReleasedThisFrame) releasedThisFrame = true;
            }

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                pointerPos = touch.primaryTouch.position.ReadValue();
                if (touch.primaryTouch.press.wasPressedThisFrame) pressedThisFrame = true;
                if (touch.primaryTouch.press.wasReleasedThisFrame) releasedThisFrame = true;
            }

            if (pressedThisFrame)
            {
                pointerDown = true;
                pointerStart = pointerPos;
                selectedObs = FindObstacleAtScreen(pointerPos);
                if (selectedObs != null)
                    Debug.Log($"Selected box at ({selectedObs.Col},{selectedObs.Row})");
            }

            if (releasedThisFrame && pointerDown)
            {
                pointerDown = false;
                if (selectedObs != null && !selectedObs.Sliding)
                {
                    var delta = pointerPos - pointerStart;
                    int dc = 0, dr = 0;

                    if (delta.magnitude >= 15f)
                    {
                        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                            dc = delta.x > 0 ? 1 : -1;
                        else
                            dr = delta.y > 0 ? -1 : 1;
                    }

                    if (dc != 0 || dr != 0)
                    {
                        if (selectedObs.TrySlide(dc, dr))
                            selectedObs.OnSlideComplete += OnObstacleSlideComplete;
                    }
                }
                selectedObs = null;
            }
        }

        private ObstacleController FindObstacleAtScreen(Vector2 screenPos)
        {
            var cam = Camera.main;
            if (cam == null) return null;

            var worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
            var (col, row) = GridManager.Instance.ToGrid(worldPos);

            foreach (var obs in obstacles)
            {
                if (obs.Col == col && obs.Row == row && obs.IsMovable && !obs.Sliding)
                    return obs;
            }
            return null;
        }

        private void OnObstacleSlideComplete()
        {
            foreach (var obs in obstacles)
                obs.OnSlideComplete -= OnObstacleSlideComplete;

            if (Shiba != null && Shiba.Alive && !Shiba.Arrived)
                Shiba.RecalculatePath();
        }

        // ===================== Camera =====================

        private void FitCamera(int cols, int rows)
        {
            var cam = Camera.main;
            if (cam == null) return;

            float cx = (cols - 1) * 0.5f;
            float cz = -(rows - 1) * 0.5f;

            cam.transform.position = new Vector3(cx, 10f, cz);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(cols, rows) * 0.5f + 1.5f;
        }

        // ===================== Spawning =====================

        private ShibaController SpawnShiba(int col, int row, Vector2Int[] route)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = "Shiba";
            obj.transform.localScale = Vector3.one * 0.7f;
            obj.GetComponent<Renderer>().material.color = new Color(1f, 0.9f, 0.2f);
            Destroy(obj.GetComponent<Collider>());
            var sc = obj.AddComponent<ShibaController>();
            sc.Init(col, row, route);
            allSpawned.Add(obj);
            return sc;
        }

        private void SpawnRouteMarkers(Vector2Int[] route)
        {
            var gm = GridManager.Instance;
            for (int i = 0; i < route.Length; i++)
            {
                var pos = route[i];

                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"Route_{i}";
                marker.transform.position = gm.ToWorld(pos.x, pos.y) + Vector3.down * 0.45f;
                marker.transform.localScale = new Vector3(0.3f, 0.02f, 0.3f);
                marker.GetComponent<Renderer>().material.color = new Color(1f, 0.75f, 0.3f, 0.8f);
                Destroy(marker.GetComponent<Collider>());
                allSpawned.Add(marker);

                if (i < route.Length - 1)
                {
                    var next = route[i + 1];
                    var arrow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    arrow.name = $"Arrow_{i}";

                    float mx = (pos.x + next.x) * 0.5f;
                    float mz = -(pos.y + next.y) * 0.5f;
                    arrow.transform.position = new Vector3(mx, -0.44f, mz);
                    arrow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                    int dx = next.x - pos.x;
                    if (dx != 0)
                        arrow.transform.localScale = new Vector3(0.4f, 0.15f, 1f);
                    else
                        arrow.transform.localScale = new Vector3(0.15f, 0.4f, 1f);

                    arrow.GetComponent<Renderer>().material.color = new Color(1f, 0.75f, 0.3f, 0.6f);
                    Destroy(arrow.GetComponent<Collider>());
                    allSpawned.Add(arrow);
                }
            }
        }

        private void SpawnObstacle(int col, int row, bool movable)
        {
            if (movable)
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = $"Box({col},{row})";
                obj.transform.localScale = Vector3.one * 0.8f;
                obj.GetComponent<Renderer>().material.color = new Color(0.6f, 0.55f, 0.4f);
                Destroy(obj.GetComponent<Collider>());
                var oc = obj.AddComponent<ObstacleController>();
                oc.Init(col, row, true);
                obstacles.Add(oc);
                allSpawned.Add(obj);
            }
            else
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                obj.name = $"Tree({col},{row})";
                obj.transform.localScale = new Vector3(0.7f, 0.5f, 0.7f);
                obj.GetComponent<Renderer>().material.color = new Color(0.2f, 0.5f, 0.15f);
                Destroy(obj.GetComponent<Collider>());
                var oc = obj.AddComponent<ObstacleController>();
                oc.Init(col, row, false);
                obstacles.Add(oc);
                allSpawned.Add(obj);
            }
        }

        private void SpawnHome(int col, int row)
        {
            var parent = new GameObject("Home");
            parent.transform.position = GridManager.Instance.ToWorld(col, row);

            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.transform.SetParent(parent.transform);
            baseObj.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            baseObj.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
            baseObj.GetComponent<Renderer>().material.color = new Color(0.3f, 0.5f, 0.95f);
            Destroy(baseObj.GetComponent<Collider>());

            var roofObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roofObj.transform.SetParent(parent.transform);
            roofObj.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            roofObj.transform.localScale = new Vector3(0.6f, 0.35f, 0.6f);
            roofObj.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            roofObj.GetComponent<Renderer>().material.color = new Color(0.15f, 0.3f, 0.7f);
            Destroy(roofObj.GetComponent<Collider>());

            allSpawned.Add(parent);
        }

        private void SpawnFloor(int cols, int rows)
        {
            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    tile.name = $"Tile_{c}_{r}";
                    tile.transform.position = GridManager.Instance.ToWorld(c, r) + Vector3.down * 0.5f;
                    tile.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    tile.transform.localScale = new Vector3(0.95f, 0.95f, 1f);

                    bool dark = (c + r) % 2 == 0;
                    tile.GetComponent<Renderer>().material.color = dark
                        ? new Color(0.55f, 0.75f, 0.40f)
                        : new Color(0.62f, 0.82f, 0.48f);

                    Destroy(tile.GetComponent<Collider>());
                    allSpawned.Add(tile);
                }
            }
        }

        // ===================== Cleanup =====================

        private void ClearAll()
        {
            foreach (var obj in allSpawned)
                if (obj != null) Destroy(obj);
            allSpawned.Clear();
            obstacles.Clear();
            Shiba = null;
        }
    }
}
