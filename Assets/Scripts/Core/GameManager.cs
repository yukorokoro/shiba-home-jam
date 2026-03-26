using UnityEngine;
using UnityEngine.InputSystem;
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

        public event System.Action<GameState> OnStateChanged;

        private List<ObstacleController> obstacles = new List<ObstacleController>();
        private List<EnemyController> enemies = new List<EnemyController>();
        private List<GameObject> allSpawned = new List<GameObject>();

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
            HandleInput();
        }

        // ===================== Level Setup =====================

        private void LoadLevel()
        {
            ClearAll();

            int cols = 6, rows = 6;
            GridManager.Instance.Init(cols, rows);

            // Home at (5,2) — right middle
            HomeCol = 5;
            HomeRow = 2;
            GridManager.Instance.Set(HomeCol, HomeRow, CellType.Home);
            SpawnHome(HomeCol, HomeRow);

            // Floor
            SpawnFloor(cols, rows);

            // Shiba at (0,2) — left middle, same row as Home
            Shiba = SpawnShiba(0, 2);
            Shiba.OnReachedHome += () => SetState(GameState.Clear);
            Shiba.OnCaught += () => SetState(GameState.GameOver);

            // Movable box at (2,2) — directly between Shiba and Home on row 2
            SpawnObstacle(2, 2, true);

            // Enemy at (5,0) — top right, approaches from above
            SpawnEnemy(5, 0);

            FitCamera(cols, rows);
            SetState(GameState.Playing);

            // Verify pathfinding at start
            var gm = GridManager.Instance;
            var path = gm.FindPath(0, 2, HomeCol, HomeRow);
            if (path != null)
            {
                var steps = new System.Text.StringBuilder("Shiba path at start: (0,2)→");
                foreach (var p in path) steps.Append($"({p.x},{p.y})→");
                var pathStr = steps.ToString().TrimEnd('→');
                Debug.Log(pathStr);
                Debug.Log($"Path length: {path.Count} steps (direct would be 5). Box at (2,2) forces detour.");
            }
            else
            {
                Debug.Log("Shiba: no path found at start, waiting for player");
            }

            Debug.Log("Level 1: Shiba(0,2) Home(5,2) Box(2,2) Enemy(5,0)");
            Debug.Log("Slide box DOWN to open row 2, then RIGHT to block enemy!");
        }

        // ===================== Input =====================

        private void HandleInput()
        {
            // Read pointer state from either mouse or touch
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

            // Touch overrides mouse if active
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
                            dr = delta.y > 0 ? -1 : 1; // screen up = row decrease
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

        /// <summary>
        /// Convert screen position to grid cell and find movable obstacle there.
        /// Much more reliable than Physics.Raycast for top-down orthographic cameras.
        /// </summary>
        private ObstacleController FindObstacleAtScreen(Vector2 screenPos)
        {
            var cam = Camera.main;
            if (cam == null) return null;

            // Orthographic camera: ScreenToWorldPoint gives world XZ directly
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

            // Shiba recalculates path after obstacle moves
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

        private ShibaController SpawnShiba(int col, int row)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = "Shiba";
            obj.transform.localScale = Vector3.one * 0.7f;
            obj.GetComponent<Renderer>().material.color = new Color(1f, 0.9f, 0.2f);
            Destroy(obj.GetComponent<Collider>());
            var sc = obj.AddComponent<ShibaController>();
            sc.Init(col, row);
            allSpawned.Add(obj);
            return sc;
        }

        private void SpawnObstacle(int col, int row, bool movable)
        {
            if (movable)
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = $"Box({col},{row})";
                obj.transform.localScale = Vector3.one * 0.8f;
                obj.GetComponent<Renderer>().material.color = new Color(0.6f, 0.55f, 0.4f);
                Destroy(obj.GetComponent<Collider>()); // not using physics raycast anymore
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

        private void SpawnEnemy(int col, int row)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = $"Enemy({col},{row})";
            obj.transform.localScale = Vector3.one * 0.7f;
            obj.GetComponent<Renderer>().material.color = new Color(1f, 0.5f, 0.1f);
            Destroy(obj.GetComponent<Collider>());
            var ec = obj.AddComponent<EnemyController>();
            ec.Init(col, row);
            ec.OnCaughtShiba += () => SetState(GameState.GameOver);
            enemies.Add(ec);
            allSpawned.Add(obj);
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
            enemies.Clear();
            Shiba = null;
        }
    }
}
