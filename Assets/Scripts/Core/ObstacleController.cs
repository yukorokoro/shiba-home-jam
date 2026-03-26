using UnityEngine;
using System;
using System.Collections;

namespace ShibaHomeJam.Core
{
    public class ObstacleController : MonoBehaviour
    {
        public int Col { get; private set; }
        public int Row { get; private set; }
        public bool Sliding { get; private set; }

        /// <summary>Fired after the slide animation finishes.</summary>
        public event Action OnSlideComplete;

        private float slideSpeed = 12f;

        public void Init(int col, int row)
        {
            Col = col;
            Row = row;
            transform.position = GridManager.Instance.ToWorld(col, row);
            GridManager.Instance.Set(col, row, CellType.Obstacle);
        }

        public bool TrySlide(int dc, int dr)
        {
            if (Sliding) return false;

            var gm = GridManager.Instance;
            int destCol = Col, destRow = Row;

            while (true)
            {
                int nc = destCol + dc, nr = destRow + dr;
                if (!gm.InBounds(nc, nr)) break;
                if (gm.Get(nc, nr) != CellType.Empty) break;
                destCol = nc;
                destRow = nr;
            }

            if (destCol == Col && destRow == Row) return false;

            gm.Clear(Col, Row);
            Col = destCol;
            Row = destRow;
            gm.Set(Col, Row, CellType.Obstacle);

            Debug.Log($"Obstacle moved to ({Col},{Row})");
            StartCoroutine(AnimateTo(gm.ToWorld(Col, Row)));
            return true;
        }

        private IEnumerator AnimateTo(Vector3 target)
        {
            Sliding = true;
            while (Vector3.Distance(transform.position, target) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, slideSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = target;
            Sliding = false;
            OnSlideComplete?.Invoke();
        }
    }
}
