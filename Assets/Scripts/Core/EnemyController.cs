using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace ShibaHomeJam.Core
{
    /// <summary>
    /// Enemy auto-moves toward Shiba every moveInterval seconds, one step (Manhattan).
    /// If it lands on Shiba's cell → game over.
    /// </summary>
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

        private void TryStep()
        {
            var gm = GridManager.Instance;
            var shiba = GameManager.Instance.Shiba;
            if (shiba == null || !shiba.Alive) return;

            int targetCol = shiba.Col;
            int targetRow = shiba.Row;

            // Pick best one-step move toward Shiba (Manhattan)
            int bestCol = Col, bestRow = Row;
            int bestDist = Mathf.Abs(Col - targetCol) + Mathf.Abs(Row - targetRow);

            Vector2Int[] dirs = {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            foreach (var d in dirs)
            {
                int nc = Col + d.x, nr = Row + d.y;
                if (!gm.InBounds(nc, nr)) continue;

                var cell = gm.Get(nc, nr);
                // Can move into Empty, Home, or Shiba
                if (cell == CellType.Obstacle || cell == CellType.Enemy) continue;

                int dist = Mathf.Abs(nc - targetCol) + Mathf.Abs(nr - targetRow);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestCol = nc;
                    bestRow = nr;
                }
            }

            if (bestCol == Col && bestRow == Row)
            {
                // Log which obstacles are blocking
                foreach (var d in dirs)
                {
                    int nc = Col + d.x, nr = Row + d.y;
                    if (gm.InBounds(nc, nr) && gm.Get(nc, nr) == CellType.Obstacle)
                        Debug.Log($"Enemy blocked by obstacle at ({nc},{nr})");
                }
                return;
            }

            gm.Clear(Col, Row);
            Col = bestCol;
            Row = bestRow;

            Debug.Log($"Enemy moved to ({Col},{Row})");

            // Check if landed on Shiba
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
