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

        // Distance logging
        private float distLogTimer;
        private const float distLogInterval = 1f;

        // Escape limit: Shiba can only escape 2 times, then must head toward Home
        private int escapeCount;
        private const int maxEscapes = 2;

        public void Init(int col, int row)
        {
            Col = col;
            Row = row;
            Alive = true;
            Arrived = false;
            escapeCount = 0;
            moveTimer = moveInterval;
            distLogTimer = distLogInterval;
            transform.position = GridManager.Instance.ToWorld(col, row);
            GridManager.Instance.Set(col, row, CellType.Shiba);
        }

        private void Update()
        {
            if (!Alive || Arrived) return;

            // Log distance every second
            if (!animating)
            {
                distLogTimer -= Time.deltaTime;
                if (distLogTimer <= 0f)
                {
                    distLogTimer = distLogInterval;
                    LogDistance();
                }
            }

            if (animating) return;

            moveTimer -= Time.deltaTime;
            if (moveTimer <= 0f)
            {
                moveTimer = moveInterval;
                TryStep();
            }
        }

        private void LogDistance()
        {
            var gm = GridManager.Instance;
            int enemyCol = -1, enemyRow = -1;
            for (int c = 0; c < gm.Cols; c++)
                for (int r = 0; r < gm.Rows; r++)
                    if (gm.Get(c, r) == CellType.Enemy) { enemyCol = c; enemyRow = r; }

            if (enemyCol >= 0)
            {
                int dist = GridManager.Manhattan(Col, Row, enemyCol, enemyRow);
                Debug.Log($"Distance Shiba-Enemy: {dist} cells");
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

            // Priority 2: Escape (limited to maxEscapes times total)
            if (enemyDist <= 2 && enemyCol >= 0 && escapeCount < maxEscapes)
            {
                var escapeCell = FindEscapeCell(enemyCol, enemyRow);
                if (escapeCell.HasValue)
                {
                    escapeCount++;
                    Debug.Log($"Shiba: escaping enemy ({escapeCount}/{maxEscapes}), moving to ({escapeCell.Value.x},{escapeCell.Value.y})");
                    MoveTo(escapeCell.Value.x, escapeCell.Value.y, homeC, homeR);
                    return;
                }
            }

            // Priority 3: Path to Home ignoring enemy (risky but must keep moving)
            var riskyPath = gm.FindPath(Col, Row, homeC, homeR, CellType.Obstacle);
            if (riskyPath != null && riskyPath.Count > 0)
            {
                var next = riskyPath[0];
                if (gm.Get(next.x, next.y) == CellType.Enemy)
                {
                    Debug.Log("Shiba: path blocked by enemy, waiting");
                    return;
                }
                Debug.Log($"Shiba: risky path, moving to ({next.x},{next.y})");
                MoveTo(next.x, next.y, homeC, homeR);
                return;
            }

            Debug.Log("Shiba: completely surrounded, waiting");
        }

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
