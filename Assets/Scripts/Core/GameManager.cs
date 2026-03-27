using UnityEngine;
using UnityEngine.InputSystem;
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

        public event System.Action<GameState> OnStateChanged;
        public event System.Action<float> OnTimerUpdated;

        private List<ObstacleController> obstacles = new List<ObstacleController>();
        private List<GameObject> allSpawned = new List<GameObject>();

        // Timer
        private float levelTime = 30f;
        private float timerLogInterval = 5f;
        private float timerLogTimer;

        // Timer UI (created at runtime)
        private Text timerText;
        private Canvas timerCanvas;
        private Coroutine pulseCoroutine;

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
            LoadLevel();
        }

        private void Update()
        {
            if (State != GameState.Playing) return;

            // Countdown timer
            TimeRemaining -= Time.deltaTime;
            OnTimerUpdated?.Invoke(TimeRemaining);
            UpdateTimerUI();

            // Log timer every 5 seconds
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
                Shiba.Stop();
                SetState(GameState.GameOver);
                return;
            }

            HandleInput();
        }

        // ===================== Level Setup =====================

        private void LoadLevel()
        {
            ClearAll();

            int cols = 6, rows = 6;
            GridManager.Instance.Init(cols, rows);

            // Home at (5,2)
            HomeCol = 5;
            HomeRow = 2;
            GridManager.Instance.Set(HomeCol, HomeRow, CellType.Home);
            SpawnHome(HomeCol, HomeRow);

            SpawnFloor(cols, rows);

            // Define Shiba's fixed route: straight line on row 2
            var route = new Vector2Int[] {
                new Vector2Int(0, 2),
                new Vector2Int(1, 2),
                new Vector2Int(2, 2),
                new Vector2Int(3, 2),
                new Vector2Int(4, 2),
                new Vector2Int(5, 2)
            };

            // Draw route visuals (before spawning Shiba/obstacles so they render on top)
            SpawnRouteMarkers(route);

            // Shiba at (0,2) with fixed route
            Shiba = SpawnShiba(0, 2, route);
            Shiba.OnReachedHome += () => SetState(GameState.Clear);

            // Movable box at (2,2) — blocks the route
            SpawnObstacle(2, 2, true);

            // Timer
            TimeRemaining = levelTime;
            timerLogTimer = 0f;
            CreateTimerUI();

            FitCamera(cols, rows);
            SetState(GameState.Playing);

            Debug.Log($"Level 1: Route (0,2)→(5,2), Box at (2,2). Timer: {levelTime}s.");
        }

        // ===================== Timer UI =====================

        private void CreateTimerUI()
        {
            // Create a canvas for the timer
            var canvasObj = new GameObject("TimerCanvas");
            timerCanvas = canvasObj.AddComponent<Canvas>();
            timerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            timerCanvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            allSpawned.Add(canvasObj);

            // Timer text — top center, large
            var textObj = new GameObject("TimerText");
            textObj.transform.SetParent(canvasObj.transform, false);
            timerText = textObj.AddComponent<Text>();
            timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            timerText.fontSize = 64;
            timerText.alignment = TextAnchor.MiddleCenter;
            timerText.color = Color.white;
            timerText.text = $"{Mathf.CeilToInt(levelTime)}s";

            // Add outline for readability
            var outline = textObj.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, -2);

            var rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.3f, 0.88f);
            rect.anchorMax = new Vector2(0.7f, 0.98f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void UpdateTimerUI()
        {
            if (timerText == null) return;

            int seconds = Mathf.CeilToInt(TimeRemaining);
            timerText.text = $"{seconds}s";

            if (TimeRemaining <= 5f)
            {
                // Pulse animation when 5 seconds left
                timerText.color = Color.red;
                float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 8f);
                timerText.transform.localScale = Vector3.one * pulse;
            }
            else if (TimeRemaining <= 10f)
            {
                // Red when 10 seconds left
                timerText.color = Color.red;
                timerText.transform.localScale = Vector3.one;
            }
            else
            {
                timerText.color = Color.white;
                timerText.transform.localScale = Vector3.one;
            }
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
                        Debug.Log($"Swipe direction: dc={dc} dr={dr}");
                        if (selectedObs.TrySlide(dc, dr))
                        {
                            selectedObs.OnSlideComplete += OnObstacleSlideComplete;
                        }
                    }
                    else
                    {
                        Debug.Log("Swipe too short, try dragging further");
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

        // ===================== State =====================

        private void SetState(GameState s)
        {
            State = s;
            OnStateChanged?.Invoke(s);
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

                // Small dot marker on the route
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"Route_{i}";
                marker.transform.position = gm.ToWorld(pos.x, pos.y) + Vector3.down * 0.45f;
                marker.transform.localScale = new Vector3(0.3f, 0.02f, 0.3f);
                marker.GetComponent<Renderer>().material.color = new Color(1f, 0.75f, 0.3f, 0.8f); // warm orange dot
                Destroy(marker.GetComponent<Collider>());
                allSpawned.Add(marker);

                // Arrow between cells (small elongated quad pointing to next cell)
                if (i < route.Length - 1)
                {
                    var next = route[i + 1];
                    var arrow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    arrow.name = $"Arrow_{i}";

                    float mx = (pos.x + next.x) * 0.5f;
                    float mz = -(pos.y + next.y) * 0.5f;
                    arrow.transform.position = new Vector3(mx, -0.44f, mz);
                    arrow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                    // Stretch in the direction of movement
                    int dx = next.x - pos.x;
                    int dy = next.y - pos.y;
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
