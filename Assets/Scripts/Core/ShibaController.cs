using UnityEngine;
using System;
using System.Collections;

namespace ShibaHomeJam.Core
{
    public enum ShibaState
    {
        Idle,
        Moving,
        Arrived,
        Caught
    }

    public class ShibaController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;

        public Vector2Int GridPosition { get; private set; }
        public ShibaState State { get; private set; } = ShibaState.Idle;

        public event Action OnReachedGoal;
        public event Action OnCaught;

        public void Initialize(Vector2Int startPos)
        {
            GridPosition = startPos;
            transform.position = GridManager.Instance.GridToWorld(startPos);
            GridManager.Instance.SetCell(startPos, CellType.Shiba, gameObject);
            State = ShibaState.Idle;
        }

        public bool TryMove(Vector2Int direction)
        {
            if (State != ShibaState.Idle) return false;

            var target = GridPosition + direction;

            if (!GridManager.Instance.IsInBounds(target)) return false;

            var cellType = GridManager.Instance.GetCell(target);
            if (cellType == CellType.Obstacle || cellType == CellType.Wall) return false;

            if (cellType == CellType.Goal)
            {
                GridManager.Instance.SetCell(GridPosition, CellType.Empty);
                GridPosition = target;
                StartCoroutine(AnimateMove(target, () =>
                {
                    State = ShibaState.Arrived;
                    OnReachedGoal?.Invoke();
                }));
                return true;
            }

            if (cellType == CellType.Enemy)
            {
                State = ShibaState.Caught;
                OnCaught?.Invoke();
                return false;
            }

            GridManager.Instance.MoveOccupant(GridPosition, target);
            GridPosition = target;
            StartCoroutine(AnimateMove(target, () => State = ShibaState.Idle));
            return true;
        }

        public void AutoMove()
        {
            if (State != ShibaState.Idle) return;

            // 柴犬は基本的に右（ゴール方向）に進もうとする
            Vector2Int[] priorities = {
                Vector2Int.right,
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left
            };

            foreach (var dir in priorities)
            {
                if (TryMove(dir)) return;
            }
        }

        public void SetCaught()
        {
            State = ShibaState.Caught;
            OnCaught?.Invoke();
        }

        private IEnumerator AnimateMove(Vector2Int target, Action onComplete)
        {
            State = ShibaState.Moving;
            var targetWorld = GridManager.Instance.GridToWorld(target);

            while (Vector3.Distance(transform.position, targetWorld) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, targetWorld, moveSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = targetWorld;
            onComplete?.Invoke();
        }
    }
}
