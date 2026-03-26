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
        Home
    }

    /// <summary>
    /// Pure data grid. No visuals, no spawning. Just tracks what is in each cell.
    /// Coordinate system: (col, row) where (0,0) is top-left.
    /// World mapping: col → +X, row → -Z (so row 0 is at top of screen).
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        public int Cols { get; private set; }
        public int Rows { get; private set; }

        private CellType[,] grid; // [col, row]

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Init(int cols, int rows)
        {
            Cols = cols;
            Rows = rows;
            grid = new CellType[cols, rows];
        }

        // ---- query ----

        public bool InBounds(int col, int row)
        {
            return col >= 0 && col < Cols && row >= 0 && row < Rows;
        }

        public CellType Get(int col, int row)
        {
            if (!InBounds(col, row)) return CellType.Obstacle; // out of bounds acts as wall
            return grid[col, row];
        }

        public bool IsWalkable(int col, int row)
        {
            if (!InBounds(col, row)) return false;
            var c = grid[col, row];
            return c == CellType.Empty || c == CellType.Home;
        }

        // ---- mutate ----

        public void Set(int col, int row, CellType type)
        {
            if (InBounds(col, row)) grid[col, row] = type;
        }

        public void Clear(int col, int row)
        {
            Set(col, row, CellType.Empty);
        }

        // ---- coordinate conversion ----
        // World: X = col, Y = 0, Z = -(row)  so row 0 is at Z=0 (top of screen with top-down cam)

        public Vector3 ToWorld(int col, int row)
        {
            return new Vector3(col, 0f, -row);
        }

        public (int col, int row) ToGrid(Vector3 world)
        {
            return (Mathf.RoundToInt(world.x), Mathf.RoundToInt(-world.z));
        }

        // ---- pathfinding (BFS, used by Shiba) ----

        public List<Vector2Int> FindPath(int fromCol, int fromRow, int toCol, int toRow)
        {
            if (fromCol == toCol && fromRow == toRow) return new List<Vector2Int>();

            var visited = new bool[Cols, Rows];
            var parent = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();

            var start = new Vector2Int(fromCol, fromRow);
            var goal = new Vector2Int(toCol, toRow);

            queue.Enqueue(start);
            visited[fromCol, fromRow] = true;

            Vector2Int[] dirs = {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var d in dirs)
                {
                    var next = current + d;
                    if (!InBounds(next.x, next.y)) continue;
                    if (visited[next.x, next.y]) continue;

                    var cell = grid[next.x, next.y];
                    // Can walk through Empty or Home; blocked by Obstacle and Enemy
                    if (cell == CellType.Obstacle || cell == CellType.Enemy) continue;

                    visited[next.x, next.y] = true;
                    parent[next] = current;
                    queue.Enqueue(next);

                    if (next == goal)
                    {
                        // reconstruct
                        var path = new List<Vector2Int>();
                        var node = goal;
                        while (node != start)
                        {
                            path.Add(node);
                            node = parent[node];
                        }
                        path.Reverse();
                        return path;
                    }
                }
            }

            return null; // no path
        }
    }
}
