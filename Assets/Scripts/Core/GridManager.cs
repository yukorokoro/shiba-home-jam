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

    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        public int Cols { get; private set; }
        public int Rows { get; private set; }

        private CellType[,] grid;

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

        public bool InBounds(int col, int row)
        {
            return col >= 0 && col < Cols && row >= 0 && row < Rows;
        }

        public CellType Get(int col, int row)
        {
            if (!InBounds(col, row)) return CellType.Obstacle;
            return grid[col, row];
        }

        public void Set(int col, int row, CellType type)
        {
            if (InBounds(col, row)) grid[col, row] = type;
        }

        public void Clear(int col, int row)
        {
            Set(col, row, CellType.Empty);
        }

        public Vector3 ToWorld(int col, int row)
        {
            return new Vector3(col, 0f, -row);
        }

        public (int col, int row) ToGrid(Vector3 world)
        {
            return (Mathf.RoundToInt(world.x), Mathf.RoundToInt(-world.z));
        }

        private static readonly Vector2Int[] Dirs = {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        /// <summary>
        /// BFS pathfinding. blockedTypes specifies which CellTypes are impassable.
        /// </summary>
        public List<Vector2Int> FindPath(int fromCol, int fromRow, int toCol, int toRow,
            params CellType[] blockedTypes)
        {
            if (fromCol == toCol && fromRow == toRow) return new List<Vector2Int>();

            var blocked = new HashSet<CellType>(blockedTypes);
            var visited = new bool[Cols, Rows];
            var parent = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();

            var start = new Vector2Int(fromCol, fromRow);
            var goal = new Vector2Int(toCol, toRow);

            queue.Enqueue(start);
            visited[fromCol, fromRow] = true;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var d in Dirs)
                {
                    var next = current + d;
                    if (!InBounds(next.x, next.y)) continue;
                    if (visited[next.x, next.y]) continue;

                    var cell = grid[next.x, next.y];
                    if (blocked.Contains(cell)) continue;

                    visited[next.x, next.y] = true;
                    parent[next] = current;
                    queue.Enqueue(next);

                    if (next == goal)
                    {
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

            return null;
        }

        /// <summary>
        /// Get all walkable neighbor cells (not in blockedTypes).
        /// </summary>
        public List<Vector2Int> GetWalkableNeighbors(int col, int row, params CellType[] blockedTypes)
        {
            var blocked = new HashSet<CellType>(blockedTypes);
            var result = new List<Vector2Int>();

            foreach (var d in Dirs)
            {
                int nc = col + d.x, nr = row + d.y;
                if (!InBounds(nc, nr)) continue;
                if (blocked.Contains(grid[nc, nr])) continue;
                result.Add(new Vector2Int(nc, nr));
            }
            return result;
        }

        public static int Manhattan(int c1, int r1, int c2, int r2)
        {
            return Mathf.Abs(c1 - c2) + Mathf.Abs(r1 - r2);
        }
    }
}
