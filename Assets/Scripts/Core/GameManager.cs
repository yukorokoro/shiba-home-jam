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

    /// <summary>
    /// Owns the game loop: spawns level, handles input, game state.
    /// Level data is hardcoded for now (Level 1).
    /// </summary>
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
        private bool dragging;

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

            // Level 1: 6x6
            int cols = 6, rows = 6;
            GridManager.Instance.Init(cols, rows);

            // Home at (5,5)
            HomeCol = 5;
            HomeRow = 5;
            GridManager.Instance.Set(HomeCol, HomeRow, CellType.Home);
            SpawnHome(HomeCol, HomeRow);

            // Floor tiles
            SpawnFloor(cols, rows);

            // Shiba at (0,0)
            Shiba = SpawnShiba(0, 0);
            Shiba.OnReachedHome += () => SetState(GameState.Clear);
            Shiba.OnCaught += () => SetState(GameState.GameOver);

            // Obstacles
            SpawnObstacle(2, 1);
            SpawnObstacle(1, 3);
            SpawnObstacle(3, 2);

            // Enemy at (5,0)
            SpawnEnemy(5, 0);

            // Camera
            FitCamera(cols, rows);

            SetState(GameState.Playing);
            Debug.Log("Level loaded. Shiba at (0,0), Home at (5,5), Enemy at (5,0)");
        }

        // ===================== Input =====================

        private void HandleInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pointerStart = mouse.position.ReadValue();
                selectedObs = Raycast(pointerStart);
                dragging = selectedObs != null;
            }
            else if (mouse.leftButton.wasReleasedThisFrame && dragging)
            {
                dragging = false;
                if (selectedObs != null && !selectedObs.Sliding)
                {
                    var pointerEnd = mouse.position.ReadValue();
                    var delta = pointerEnd - pointerStart;

                    int dc = 0, dr = 0;

                    if (delta.magnitude < 20f)
                    {
                        // Simple click with no swipe: do nothing, wait for a swipe
                    }
                    else if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                    {
                        dc = delta.x > 0 ? 1 : -1; // left/right → col
                    }
                    else
                    {
                        dr = delta.y > 0 ? -1 : 1; // screen up → row decreases (top-left origin)
                    }

                    if (dc != 0 || dr != 0)
                    {
                        SlideObstacle(selectedObs, dc, dr);
                    }
                }
                selectedObs = null;
            }

            // Touch support
            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                {
                    pointerStart = touch.position.ReadValue();
                    selectedObs = Raycast(pointerStart);
                    dragging = selectedObs != null;
                }
                else if (touch.press.wasReleasedThisFrame && dragging)
                {
                    dragging = false;
                    if (selectedObs != null && !selectedObs.Sliding)
                    {
                        var pointerEnd = touch.position.ReadValue();
                        var delta = pointerEnd - pointerStart;

                        int dc = 0, dr = 0;
                        if (delta.magnitude >= 20f)
                        {
                            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                                dc = delta.x > 0 ? 1 : -1;
                            else
                                dr = delta.y > 0 ? -1 : 1;
                        }

                        if (dc != 0 || dr != 0)
                            SlideObstacle(selectedObs, dc, dr);
                    }
                    selectedObs = null;
                }
            }
        }

        private void SlideObstacle(ObstacleController obs, int dc, int dr)
        {
            if (obs.TrySlide(dc, dr))
            {
                // After slide animation finishes, tell Shiba to recalculate
                obs.OnSlideComplete += OnObstacleSlideComplete;
            }
        }

        private void OnObstacleSlideComplete()
        {
            // Unsubscribe from all obstacles to avoid double-fire
            foreach (var obs in obstacles)
                obs.OnSlideComplete -= OnObstacleSlideComplete;

            if (Shiba != null && Shiba.Alive && !Shiba.Arrived)
                Shiba.RecalculatePath();
        }

        private ObstacleController Raycast(Vector2 screenPos)
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var ray = cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit, 100f))
                return hit.collider.GetComponent<ObstacleController>();
            return null;
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
            obj.GetComponent<Renderer>().material.color = new Color(1f, 0.9f, 0.2f); // bright yellow
            Destroy(obj.GetComponent<Collider>()); // no raycast on shiba
            var sc = obj.AddComponent<ShibaController>();
            sc.Init(col, row);
            allSpawned.Add(obj);
            return sc;
        }

        private void SpawnObstacle(int col, int row)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = $"Obstacle({col},{row})";
            obj.transform.localScale = Vector3.one * 0.85f;
            obj.GetComponent<Renderer>().material.color = new Color(0.55f, 0.55f, 0.55f); // gray
            // Keep BoxCollider for raycast!
            var oc = obj.AddComponent<ObstacleController>();
            oc.Init(col, row);
            obstacles.Add(oc);
            allSpawned.Add(obj);
        }

        private void SpawnEnemy(int col, int row)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = $"Enemy({col},{row})";
            obj.transform.localScale = Vector3.one * 0.7f;
            obj.GetComponent<Renderer>().material.color = new Color(1f, 0.5f, 0.1f); // orange
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

            // Base (blue cube)
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.transform.SetParent(parent.transform);
            baseObj.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            baseObj.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
            baseObj.GetComponent<Renderer>().material.color = new Color(0.3f, 0.5f, 0.95f); // blue
            Destroy(baseObj.GetComponent<Collider>());

            // Roof (rotated cube to look like pyramid from above)
            var roofObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roofObj.transform.SetParent(parent.transform);
            roofObj.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            roofObj.transform.localScale = new Vector3(0.6f, 0.35f, 0.6f);
            roofObj.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            roofObj.GetComponent<Renderer>().material.color = new Color(0.15f, 0.3f, 0.7f); // dark blue
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

                    Destroy(tile.GetComponent<Collider>()); // no raycast
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
