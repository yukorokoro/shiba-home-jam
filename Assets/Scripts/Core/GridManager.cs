using UnityEngine;
using System.Collections.Generic;

namespace ShibaHomeJam.Core
{
    public enum CellType
    {
        Empty,
        Obstacle,
        Shiba,
        Enemy,
        Goal,
        Wall
    }

    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        [SerializeField] private int width = 5;
        [SerializeField] private int height = 5;

        private CellType[,] grid;
        private Dictionary<Vector2Int, GameObject> occupants = new Dictionary<Vector2Int, GameObject>();

        public int Width => width;
        public int Height => height;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void InitializeGrid(int w, int h)
        {
            width = Mathf.Clamp(w, 5, 8);
            height = Mathf.Clamp(h, 5, 8);
            grid = new CellType[width, height];
            occupants.Clear();

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    grid[x, y] = CellType.Empty;
        }

        public bool IsInBounds(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
        }

        public bool IsEmpty(Vector2Int pos)
        {
            return IsInBounds(pos) && grid[pos.x, pos.y] == CellType.Empty;
        }

        public CellType GetCell(Vector2Int pos)
        {
            if (!IsInBounds(pos)) return CellType.Wall;
            return grid[pos.x, pos.y];
        }

        public void SetCell(Vector2Int pos, CellType type, GameObject occupant = null)
        {
            if (!IsInBounds(pos)) return;
            grid[pos.x, pos.y] = type;

            if (occupant != null)
                occupants[pos] = occupant;
            else
                occupants.Remove(pos);
        }

        public GameObject GetOccupant(Vector2Int pos)
        {
            occupants.TryGetValue(pos, out var obj);
            return obj;
        }

        public void MoveOccupant(Vector2Int from, Vector2Int to)
        {
            if (!IsInBounds(from) || !IsInBounds(to)) return;

            var type = grid[from.x, from.y];
            occupants.TryGetValue(from, out var obj);

            grid[from.x, from.y] = CellType.Empty;
            grid[to.x, to.y] = type;

            occupants.Remove(from);
            if (obj != null)
                occupants[to] = obj;
        }

        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            return new Vector3(gridPos.x, 0f, gridPos.y);
        }

        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.z));
        }
    }
}
