using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace ShibaHomeJam.Core
{
    /// <summary>
    /// Shiba moves along a pre-defined route with optional branch points.
    /// At a branch: tries primary (shortest) first → if blocked by fixed obstacle,
    /// takes alternate route → if alternate blocked by movable obstacle, waits for player.
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

        private Dictionary<Vector2Int, BranchInfo> branches = new Dictionary<Vector2Int, BranchInfo>();
        private Vector2Int[] activeBranch;
        private int branchIndex;

        private float moveInterval = 1.5f;
        private float moveTimer;
        private float animSpeed = 5f;
        private bool animating;

        private struct BranchInfo
        {
            public Vector2Int[] primary;
            public Vector2Int[] alternate;
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

            branches.Clear();
            if (branchData != null)
            {
                foreach (var b in branchData)
                {
                    var key = new Vector2Int(b.branchAt.x, b.branchAt.y);
                    var info = new BranchInfo();
                    info.primary = ConvertPositions(b.primaryRoute);
                    info.alternate = ConvertPositions(b.alternateRoute);
                    branches[key] = info;
                    Debug.Log($"Shiba: branch at ({key.x},{key.y}) — primary {info.primary.Length} cells, alternate {info.alternate.Length} cells");
                }
            }

            var sb = new System.Text.StringBuilder("Shiba route: ");
            foreach (var r in route) sb.Append($"({r.x},{r.y})→");
            Debug.Log(sb.ToString().TrimEnd('→'));
        }

        private Vector2Int[] ConvertPositions(Position[] positions)
        {
            if (positions == null) return new Vector2Int[0];
            var result = new Vector2Int[positions.Length];
            for (int i = 0; i < positions.Length; i++)
                result[i] = new Vector2Int(positions[i].x, positions[i].y);
            return result;
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
            if (activeBranch != null)
            {
                StepAlongArray(activeBranch, ref branchIndex, true);
                return;
            }

            if (routeIndex >= route.Length) return;

            var currentPos = new Vector2Int(Col, Row);

            // Check branch point
            if (branches.TryGetValue(currentPos, out var branchInfo))
            {
                HandleBranch(branchInfo);
                return;
            }

            StepAlongArray(route, ref routeIndex, false);
        }

        private void HandleBranch(BranchInfo info)
        {
            var gm = GridManager.Instance;

            // 1. Try primary (shortest) route
            if (info.primary.Length > 0)
            {
                var firstPrimary = info.primary[0];
                if (gm.Get(firstPrimary.x, firstPrimary.y) != CellType.Obstacle)
                {
                    Debug.Log($"Shiba: primary path open, taking ({firstPrimary.x},{firstPrimary.y})");
                    activeBranch = info.primary;
                    branchIndex = 0;
                    StepAlongArray(activeBranch, ref branchIndex, true);
                    return;
                }
                Debug.Log($"Shiba: primary path blocked at ({firstPrimary.x},{firstPrimary.y}) — trying alternate");
            }

            // 2. Primary blocked → try alternate route
            if (info.alternate.Length > 0)
            {
                var firstAlt = info.alternate[0];
                if (gm.Get(firstAlt.x, firstAlt.y) != CellType.Obstacle)
                {
                    Debug.Log($"Shiba: taking alternate route via ({firstAlt.x},{firstAlt.y})");
                    activeBranch = info.alternate;
                    branchIndex = 0;
                    StepAlongArray(activeBranch, ref branchIndex, true);
                    return;
                }
                Debug.Log($"Shiba: alternate route blocked at ({firstAlt.x},{firstAlt.y}), waiting for player");
                return;
            }

            // 3. No alternate → stuck
            Debug.Log("Shiba: all branches blocked, waiting");
        }

        private void StepAlongArray(Vector2Int[] path, ref int index, bool isBranch)
        {
            if (index >= path.Length)
            {
                if (isBranch)
                {
                    activeBranch = null;
                    Debug.Log("Shiba: branch complete, resuming main route");
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
