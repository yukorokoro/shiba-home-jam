using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace ShibaHomeJam.Core
{
    public class EnemyController : MonoBehaviour
    {
        public int Col { get; private set; }
        public int Row { get; private set; }

        public event Action OnCaughtShiba;

        private float moveInterval = 2.5f;
        private float moveTimer;
        private float animSpeed = 5f;

        public void Init(int col, int row)
        {
            Col = col;
            Row = row;
            moveTimer = moveInterval;
            transform.position = GridManager.Instance.ToWorld(col, row);
            GridManager.Instance.Set(col, row, CellType.Enemy);
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.State != GameState.Playing) return;

            moveTimer -= Time.deltaTime;
            if (moveTimer <= 0f)
            {
                moveTimer = moveInterval;
                TryStep();
            }
        }

        /// <summary>
        /// Called by GameManager when obstacle moves. Resets timer and recalculates.
        /// </summary>
        public void RecalculatePath()
        {
            // Don't move immediately, just reset timer so next move uses new grid state
            moveTimer = moveInterval;
            Debug.Log("Enemy: path recalculated after obstacle move");
        }

        private void TryStep()
        {
            var gm = GridManager.Instance;
            var shiba = GameManager.Instance.Shiba;
            if (shiba == null || !shiba.Alive) return;

            int targetCol = shiba.Col;
            int targetRow = shiba.Row;

            // BFS toward Shiba. Blocked by: Obstacle (both fixed and movable).
            // Can walk through: Empty, Home, Shiba (to catch it).
            var path = gm.FindPath(Col, Row, targetCol, targetRow, CellType.Obstacle);

            if (path == null || path.Count == 0)
            {
                // Stuck — log blocking obstacles
                var neighbors = gm.GetWalkableNeighbors(Col, Row);
                if (neighbors.Count == 0)
                {
                    Vector2Int[] dirs = {
                        new Vector2Int(1, 0), new Vector2Int(-1, 0),
                        new Vector2Int(0, 1), new Vector2Int(0, -1)
                    };
                    foreach (var d in dirs)
                    {
                        int nc = Col + d.x, nr = Row + d.y;
                        if (gm.InBounds(nc, nr) && gm.Get(nc, nr) == CellType.Obstacle)
                            Debug.Log($"Enemy blocked by obstacle at ({nc},{nr})");
                    }
                }
                return;
            }

            var next = path[0];
            int nc2 = next.x, nr2 = next.y;

            gm.Clear(Col, Row);
            Col = nc2;
            Row = nr2;

            Debug.Log($"Enemy moved to ({Col},{Row})");

            // Check if caught Shiba
            if (Col == shiba.Col && Row == shiba.Row)
            {
                gm.Set(Col, Row, CellType.Enemy);
                shiba.Die();
                OnCaughtShiba?.Invoke();
                StartCoroutine(AnimateTo(gm.ToWorld(Col, Row)));
                return;
            }

            gm.Set(Col, Row, CellType.Enemy);
            StartCoroutine(AnimateTo(gm.ToWorld(Col, Row)));
        }

        private IEnumerator AnimateTo(Vector3 target)
        {
            while (Vector3.Distance(transform.position, target) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, animSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = target;
        }
    }
}
