using UnityEngine;
using System;
using System.Collections;

namespace ShibaHomeJam.Core
{
    /// <summary>
    /// Shiba auto-moves toward Home every moveInterval seconds via BFS shortest path.
    /// The player does NOT control Shiba.
    /// </summary>
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
            moveTimer = moveInterval; // first move after full interval
            transform.position = GridManager.Instance.ToWorld(col, row);
            GridManager.Instance.Set(col, row, CellType.Shiba);
        }

        private void Update()
        {
            if (!Alive || Arrived) return;

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
            var home = GameManager.Instance.HomeCol;
            var homeR = GameManager.Instance.HomeRow;

            var path = gm.FindPath(Col, Row, home, homeR);
            if (path == null || path.Count == 0) return; // no path, wait

            var next = path[0];
            int nc = next.x, nr = next.y;

            // Check if enemy is there → caught
            if (gm.Get(nc, nr) == CellType.Enemy)
            {
                Die();
                return;
            }

            gm.Clear(Col, Row);
            Col = nc;
            Row = nr;

            // Check if we reached Home
            if (Col == home && Row == homeR)
            {
                Arrived = true;
                Debug.Log("LEVEL CLEAR");
                OnReachedHome?.Invoke();
            }
            else
            {
                gm.Set(Col, Row, CellType.Shiba);
            }

            Debug.Log($"Shiba moved to ({Col},{Row})");
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
