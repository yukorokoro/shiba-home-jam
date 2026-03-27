using UnityEngine;
using System;
using System.Collections;

namespace ShibaHomeJam.Core
{
    /// <summary>
    /// Shiba moves along a pre-defined route, one cell at a time.
    /// If the next cell is blocked by an obstacle, Shiba waits.
    /// Shiba CANNOT leave the route.
    /// </summary>
    public class ShibaController : MonoBehaviour
    {
        public int Col { get; private set; }
        public int Row { get; private set; }
        public bool Alive { get; private set; } = true;
        public bool Arrived { get; private set; }

        public event Action OnReachedHome;

        private Vector2Int[] route;   // the fixed route cells in order
        private int routeIndex;       // current position in route (next cell to move to)

        private float moveInterval = 1.5f;
        private float moveTimer;
        private float animSpeed = 5f;
        private bool animating;

        /// <summary>
        /// Initialize with starting position and the full route (including start).
        /// </summary>
        public void Init(int col, int row, Vector2Int[] routeCells)
        {
            Col = col;
            Row = row;
            Alive = true;
            Arrived = false;
            route = routeCells;
            routeIndex = 1; // index 0 is the starting cell, so next target is index 1
            moveTimer = moveInterval;
            transform.position = GridManager.Instance.ToWorld(col, row);
            GridManager.Instance.Set(col, row, CellType.Shiba);

            // Log the route
            var sb = new System.Text.StringBuilder("Shiba route: ");
            foreach (var r in route) sb.Append($"({r.x},{r.y})→");
            Debug.Log(sb.ToString().TrimEnd('→'));
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

        /// <summary>
        /// Called after an obstacle moves. Immediately tries to continue along route.
        /// </summary>
        public void RecalculatePath()
        {
            if (!Alive || Arrived || animating) return;
            Debug.Log("Shiba: obstacle moved, checking route");
            moveTimer = moveInterval;
            TryStep();
        }

        private void TryStep()
        {
            if (route == null || routeIndex >= route.Length)
            {
                Debug.Log("Shiba: route complete");
                return;
            }

            var gm = GridManager.Instance;
            var next = route[routeIndex];

            // Check if next route cell is blocked
            var cell = gm.Get(next.x, next.y);
            if (cell == CellType.Obstacle)
            {
                Debug.Log($"Shiba: route blocked at ({next.x},{next.y}), waiting");
                return;
            }

            // Move to next route cell
            gm.Clear(Col, Row);
            Col = next.x;
            Row = next.y;
            routeIndex++;

            Debug.Log($"Shiba: moving to ({Col},{Row}) [{routeIndex}/{route.Length}]");

            // Check if this is the last cell (Home)
            if (routeIndex >= route.Length)
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

        public void Stop()
        {
            Alive = false;
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
