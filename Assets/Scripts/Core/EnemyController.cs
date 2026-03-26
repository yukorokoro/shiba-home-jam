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
        public event Action<int> OnProximityChanged; // fires with Manhattan distance to Shiba

        private float moveInterval = 2.5f;
        private float moveTimer;
        private float recalcInterval = 0.5f;
        private float recalcTimer;
        private float animSpeed = 5f;
        private bool moving;

        private List<Vector2Int> cachedPath;

        public void Init(int col, int row)
        {
            Col = col;
            Row = row;
            moveTimer = moveInterval;
            recalcTimer = 0f;
            transform.position = GridManager.Instance.ToWorld(col, row);
            GridManager.Instance.Set(col, row, CellType.Enemy);
        }

        public void SetSpeed(float interval)
        {
            moveInterval = interval;
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.State != GameState.Playing) return;
            if (moving) return;

            // Recalculate path every 0.5s regardless
            recalcTimer -= Time.deltaTime;
            if (recalcTimer <= 0f)
            {
                recalcTimer = recalcInterval;
                RecalcPathInternal();
            }

            // Move on fixed interval — NEVER skip, NEVER reset from outside
            moveTimer -= Time.deltaTime;
            if (moveTimer <= 0f)
            {
                moveTimer = moveInterval;
                TryStep();
            }
        }

        /// <summary>
        /// Called by GameManager when obstacle moves. Immediately recalculates path
        /// but does NOT reset the move timer (preventing the freeze bug).
        /// </summary>
        public void RecalculatePath()
        {
            RecalcPathInternal();
            Debug.Log("Enemy: path recalculated after obstacle move");
        }

        private void RecalcPathInternal()
        {
            var shiba = GameManager.Instance.Shiba;
            if (shiba == null || !shiba.Alive) return;

            var gm = GridManager.Instance;
            cachedPath = gm.FindPath(Col, Row, shiba.Col, shiba.Row, CellType.Obstacle);
        }

        private void TryStep()
        {
            var gm = GridManager.Instance;
            var shiba = GameManager.Instance.Shiba;
            if (shiba == null || !shiba.Alive) return;

            // Report proximity
            int distToShiba = GridManager.Manhattan(Col, Row, shiba.Col, shiba.Row);
            OnProximityChanged?.Invoke(distToShiba);

            // Strategy 1: Follow cached BFS path
            if (cachedPath != null && cachedPath.Count > 0)
            {
                var next = cachedPath[0];
                var cell = gm.Get(next.x, next.y);

                // Verify the cell is still passable (grid may have changed)
                if (cell != CellType.Obstacle)
                {
                    cachedPath.RemoveAt(0);
                    ExecuteMove(next.x, next.y, shiba);
                    Debug.Log($"Enemy: path recalculated, moving to ({next.x},{next.y})");
                    return;
                }

                // Path step is now blocked — recalculate
                Debug.Log("Enemy: blocked, trying alternate route");
                cachedPath = gm.FindPath(Col, Row, shiba.Col, shiba.Row, CellType.Obstacle);

                if (cachedPath != null && cachedPath.Count > 0)
                {
                    var alt = cachedPath[0];
                    cachedPath.RemoveAt(0);
                    ExecuteMove(alt.x, alt.y, shiba);
                    Debug.Log($"Enemy: path recalculated, moving to ({alt.x},{alt.y})");
                    return;
                }
            }
            else
            {
                // No cached path — try fresh BFS
                cachedPath = gm.FindPath(Col, Row, shiba.Col, shiba.Row, CellType.Obstacle);
                if (cachedPath != null && cachedPath.Count > 0)
                {
                    var next = cachedPath[0];
                    cachedPath.RemoveAt(0);
                    ExecuteMove(next.x, next.y, shiba);
                    Debug.Log($"Enemy: path recalculated, moving to ({next.x},{next.y})");
                    return;
                }
            }

            // Strategy 2: Manhattan greedy fallback (try any cell closer to Shiba)
            var neighbors = gm.GetWalkableNeighbors(Col, Row, CellType.Obstacle);
            int bestDist = GridManager.Manhattan(Col, Row, shiba.Col, shiba.Row);
            Vector2Int? bestCell = null;

            foreach (var n in neighbors)
            {
                int d = GridManager.Manhattan(n.x, n.y, shiba.Col, shiba.Row);
                if (d < bestDist) { bestDist = d; bestCell = n; }
            }

            if (bestCell.HasValue)
            {
                ExecuteMove(bestCell.Value.x, bestCell.Value.y, shiba);
                Debug.Log($"Enemy: greedy fallback, moving to ({bestCell.Value.x},{bestCell.Value.y})");
                return;
            }

            // Strategy 3: Move to ANY open neighbor (don't freeze)
            if (neighbors.Count > 0)
            {
                var any = neighbors[0];
                ExecuteMove(any.x, any.y, shiba);
                Debug.Log($"Enemy: wandering, moving to ({any.x},{any.y})");
                return;
            }

            // Truly stuck on all 4 sides
            Debug.Log("Enemy: no path found, waiting");
            StartCoroutine(ShakeAnim());
        }

        private void ExecuteMove(int nc, int nr, ShibaController shiba)
        {
            var gm = GridManager.Instance;
            gm.Clear(Col, Row);
            Col = nc;
            Row = nr;

            // Check if caught Shiba
            if (Col == shiba.Col && Row == shiba.Row)
            {
                gm.Set(Col, Row, CellType.Enemy);
                shiba.Die();
                OnCaughtShiba?.Invoke();
                StartCoroutine(MoveAndBounce(gm.ToWorld(Col, Row)));
                return;
            }

            gm.Set(Col, Row, CellType.Enemy);
            StartCoroutine(MoveAndBounce(gm.ToWorld(Col, Row)));
        }

        // Move to target then do a quick bounce (scale 1.0 → 1.2 → 1.0)
        private IEnumerator MoveAndBounce(Vector3 target)
        {
            moving = true;

            // Slide to position
            while (Vector3.Distance(transform.position, target) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, animSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = target;

            // Bounce: scale up then down over 0.2s
            float t = 0f;
            Vector3 baseScale = Vector3.one * 0.7f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                float progress = t / 0.2f;
                // Peak at 0.5 progress
                float scaleMult = 1f + 0.2f * Mathf.Sin(progress * Mathf.PI);
                transform.localScale = baseScale * scaleMult;
                yield return null;
            }
            transform.localScale = baseScale;

            moving = false;
        }

        // Small shake when blocked
        private IEnumerator ShakeAnim()
        {
            moving = true;
            Vector3 origin = transform.position;
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float offset = Mathf.Sin(t * 40f) * 0.05f;
                transform.position = origin + new Vector3(offset, 0, 0);
                yield return null;
            }
            transform.position = origin;
            moving = false;
        }
    }
}
