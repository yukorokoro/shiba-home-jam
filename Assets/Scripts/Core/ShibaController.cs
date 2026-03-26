using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace ShibaHomeJam.Core
{
    public class ShibaController : MonoBehaviour
    {
        public int Col { get; private set; }
        public int Row { get; private set; }
        public bool Alive { get; private set; } = true;
        public bool Arrived { get; private set; }

        public event Action OnReachedHome;
        public event Action OnCaught;

        private float moveInterval = 2f;
        private float moveTimer;
        private float animSpeed = 5f;
        private bool animating;

        public void Init(int col, int row)
        {
            Col = col;
            Row = row;
            Alive = true;
            Arrived = false;
            moveTimer = moveInterval;
            transform.position = GridManager.Instance.ToWorld(col, row);
            GridManager.Instance.Set(col, row, CellType.Shiba);
        }

        private void Update()
        {
            if (!Alive || Arrived || animating) return;

            moveTimer -= Time.deltaTime;
            if (moveTimer <= 0f)
            {
                moveTimer = moveInterval;
                TryStep();
            }
        }

        public void RecalculatePath()
        {
            if (!Alive || Arrived || animating) return;
            Debug.Log("Shiba: path recalculated after obstacle move");
            moveTimer = moveInterval;
            TryStep();
        }

        private void TryStep()
        {
            var gm = GridManager.Instance;
            int homeC = GameManager.Instance.HomeCol;
            int homeR = GameManager.Instance.HomeRow;

            // Find enemy position
            int enemyCol = -1, enemyRow = -1;
            for (int c = 0; c < gm.Cols; c++)
                for (int r = 0; r < gm.Rows; r++)
                    if (gm.Get(c, r) == CellType.Enemy) { enemyCol = c; enemyRow = r; }

            int enemyDist = (enemyCol >= 0)
                ? GridManager.Manhattan(Col, Row, enemyCol, enemyRow) : 999;

            // Priority 1: Safe path to Home (avoids obstacles and enemy)
            var safePath = gm.FindPath(Col, Row, homeC, homeR,
                CellType.Obstacle, CellType.Enemy);

            if (safePath != null && safePath.Count > 0)
            {
                var next = safePath[0];
                Debug.Log($"Shiba: path found, moving to ({next.x},{next.y})");
                MoveTo(next.x, next.y, homeC, homeR);
                return;
            }

            // Priority 2: Enemy within 2 cells — escape (move away from enemy)
            if (enemyDist <= 2 && enemyCol >= 0)
            {
                var escapeCell = FindEscapeCell(enemyCol, enemyRow);
                if (escapeCell.HasValue)
                {
                    Debug.Log($"Shiba: escaping enemy, moving to ({escapeCell.Value.x},{escapeCell.Value.y})");
                    MoveTo(escapeCell.Value.x, escapeCell.Value.y, homeC, homeR);
                    return;
                }
            }

            // Priority 3: Path to Home ignoring enemy (risky but better than stuck)
            var riskyPath = gm.FindPath(Col, Row, homeC, homeR, CellType.Obstacle);
            if (riskyPath != null && riskyPath.Count > 0)
            {
                var next = riskyPath[0];
                // Don't step directly onto enemy
                if (gm.Get(next.x, next.y) == CellType.Enemy)
                {
                    Debug.Log("Shiba: path blocked by enemy, waiting");
                    return;
                }
                Debug.Log($"Shiba: risky path, moving to ({next.x},{next.y})");
                MoveTo(next.x, next.y, homeC, homeR);
                return;
            }

            // Completely stuck
            Debug.Log("Shiba: completely surrounded, waiting");
        }

        /// <summary>
        /// Escape: find adjacent walkable cell that maximizes distance from enemy.
        /// </summary>
        private Vector2Int? FindEscapeCell(int enemyCol, int enemyRow)
        {
            var gm = GridManager.Instance;
            var neighbors = gm.GetWalkableNeighbors(Col, Row,
                CellType.Obstacle, CellType.Enemy);

            if (neighbors.Count == 0) return null;

            Vector2Int best = neighbors[0];
            int bestDist = GridManager.Manhattan(best.x, best.y, enemyCol, enemyRow);

            for (int i = 1; i < neighbors.Count; i++)
            {
                int dist = GridManager.Manhattan(neighbors[i].x, neighbors[i].y, enemyCol, enemyRow);
                if (dist > bestDist)
                {
                    bestDist = dist;
                    best = neighbors[i];
                }
            }

            // Only escape if it actually increases distance from enemy
            int currentDist = GridManager.Manhattan(Col, Row, enemyCol, enemyRow);
            if (bestDist <= currentDist) return null;

            return best;
        }

        private void MoveTo(int nc, int nr, int homeC, int homeR)
        {
            var gm = GridManager.Instance;
            gm.Clear(Col, Row);
            Col = nc;
            Row = nr;

            if (Col == homeC && Row == homeR)
            {
                Arrived = true;
                Debug.Log("LEVEL CLEAR");
                OnReachedHome?.Invoke();
            }
            else
            {
                gm.Set(Col, Row, CellType.Shiba);
            }

            StartCoroutine(AnimateTo(gm.ToWorld(Col, Row)));
        }

        public void Die()
        {
            Alive = false;
            Debug.Log("GAME OVER");
            OnCaught?.Invoke();
        }

        private IEnumerator AnimateTo(Vector3 target)
        {
            animating = true;
            while (Vector3.Distance(transform.position, target) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, animSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = target;
            animating = false;
        }
    }
}
