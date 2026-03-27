using UnityEngine;
using System;
using System.Collections;

namespace ShibaHomeJam.Core
{
    public class ShibaController : MonoBehaviour
    {
        public int Col { get; private set; }
        public int Row { get; private set; }
        public bool Alive { get; private set; } = true;
        public bool Arrived { get; private set; }

        public event Action OnReachedHome;

        private float moveInterval = 1.5f;
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

            var path = gm.FindPath(Col, Row, homeC, homeR, CellType.Obstacle);

            if (path == null || path.Count == 0)
            {
                Debug.Log("Shiba: path blocked, waiting");
                return;
            }

            var next = path[0];
            Debug.Log($"Shiba: path found, moving to ({next.x},{next.y})");
            MoveTo(next.x, next.y, homeC, homeR);
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
