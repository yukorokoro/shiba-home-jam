using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace ShibaHomeJam.Core
{
    /// <summary>
    /// Shiba moves along a pre-defined route with optional branch points.
    /// At a branch point, Shiba tries the primary branch first.
    /// If blocked, stays on main route (doesn't take dead end).
    /// </summary>
    public class ShibaController : MonoBehaviour
    {
        public int Col { get; private set; }
        public int Row { get; private set; }
        public bool Alive { get; private set; } = true;
        public bool Arrived { get; private set; }

        public event Action OnReachedHome;

        private Vector2Int[] route;
        private int routeIndex;

        // Branch data: branchAt cell → (primaryRoute, deadEndRoute)
        private Dictionary<Vector2Int, BranchInfo> branches = new Dictionary<Vector2Int, BranchInfo>();
        private Vector2Int[] activeBranch; // if currently on a branch sub-route
        private int branchIndex;

        private float moveInterval = 1.5f;
        private float moveTimer;
        private float animSpeed = 5f;
        private bool animating;

        private struct BranchInfo
        {
            public Vector2Int[] primary;
            public Vector2Int[] deadEnd;
        }

        public void Init(int col, int row, Vector2Int[] routeCells, BranchData[] branchData = null)
        {
            Col = col;
            Row = row;
            Alive = true;
            Arrived = false;
            route = routeCells;
            routeIndex = 1;
            activeBranch = null;
            moveTimer = moveInterval;
            transform.position = GridManager.Instance.ToWorld(col, row);
            GridManager.Instance.Set(col, row, CellType.Shiba);

            // Parse branches
            branches.Clear();
            if (branchData != null)
            {
                foreach (var b in branchData)
                {
                    var key = new Vector2Int(b.branchAt.x, b.branchAt.y);
                    var info = new BranchInfo();
                    info.primary = new Vector2Int[b.primaryRoute.Length];
                    for (int i = 0; i < b.primaryRoute.Length; i++)
                        info.primary[i] = new Vector2Int(b.primaryRoute[i].x, b.primaryRoute[i].y);
                    info.deadEnd = new Vector2Int[b.deadEndRoute.Length];
                    for (int i = 0; i < b.deadEndRoute.Length; i++)
                        info.deadEnd[i] = new Vector2Int(b.deadEndRoute[i].x, b.deadEndRoute[i].y);
                    branches[key] = info;
                    Debug.Log($"Shiba: branch at ({key.x},{key.y}) — primary {info.primary.Length} cells, dead end {info.deadEnd.Length} cells");
                }
            }

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

        public void RecalculatePath()
        {
            if (!Alive || Arrived || animating) return;
            Debug.Log("Shiba: obstacle moved, checking route");
            moveTimer = moveInterval;
            TryStep();
        }

        private void TryStep()
        {
            // If on a branch sub-route, continue it
            if (activeBranch != null)
            {
                StepAlongArray(activeBranch, ref branchIndex, true);
                return;
            }

            // Main route
            if (routeIndex >= route.Length) return;

            var currentPos = new Vector2Int(Col, Row);

            // Check if current cell is a branch point
            if (branches.TryGetValue(currentPos, out var branchInfo))
            {
                // Try primary branch first
                var firstCell = branchInfo.primary[0];
                var cell = GridManager.Instance.Get(firstCell.x, firstCell.y);

                if (cell != CellType.Obstacle)
                {
                    // Primary branch is open — take it
                    Debug.Log($"Shiba: taking primary branch from ({Col},{Row})");
                    activeBranch = branchInfo.primary;
                    branchIndex = 0;
                    StepAlongArray(activeBranch, ref branchIndex, true);
                    return;
                }

                // Primary blocked — log and wait (dead end is permanently blocked by fixed obstacle)
                Debug.Log($"Shiba: primary branch blocked at ({firstCell.x},{firstCell.y}), waiting for player");
                return;
            }

            // Normal route step
            StepAlongArray(route, ref routeIndex, false);
        }

        private void StepAlongArray(Vector2Int[] path, ref int index, bool isBranch)
        {
            if (index >= path.Length)
            {
                if (isBranch)
                {
                    // Branch complete — resume main route from after the branch point
                    activeBranch = null;
                    Debug.Log("Shiba: branch complete, resuming main route");
                    // Find where we are on the main route and advance past it
                    AdvanceMainRouteToCurrentPos();
                }
                return;
            }

            var gm = GridManager.Instance;
            var next = path[index];
            var cell = gm.Get(next.x, next.y);

            if (cell == CellType.Obstacle)
            {
                Debug.Log($"Shiba: route blocked at ({next.x},{next.y}), waiting");
                return;
            }

            gm.Clear(Col, Row);
            Col = next.x;
            Row = next.y;
            index++;

            string label = isBranch ? "branch" : "route";
            Debug.Log($"Shiba: {label} moving to ({Col},{Row}) [{index}/{path.Length}]");

            // Check if reached Home
            if (Col == GameManager.Instance.HomeCol && Row == GameManager.Instance.HomeRow)
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

        private void AdvanceMainRouteToCurrentPos()
        {
            // After completing a branch, find our position in the main route
            // and skip past cells we've already covered via the branch
            var currentPos = new Vector2Int(Col, Row);
            for (int i = routeIndex; i < route.Length; i++)
            {
                if (route[i] == currentPos)
                {
                    routeIndex = i + 1;
                    Debug.Log($"Shiba: main route resumed at index {routeIndex}");
                    return;
                }
            }
            // If branch endpoint isn't on main route, just continue from where we left off
            routeIndex++;
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
