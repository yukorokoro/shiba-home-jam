using UnityEngine;
using System.Collections;

namespace ShibaHomeJam.Core
{
    public class ObstacleController : MonoBehaviour
    {
        [SerializeField] private float slideSpeed = 8f;

        public Vector2Int GridPosition { get; private set; }
        public bool IsSliding { get; private set; }

        private Camera mainCamera;

        public void Initialize(Vector2Int pos)
        {
            GridPosition = pos;
            transform.position = GridManager.Instance.GridToWorld(pos);
            GridManager.Instance.SetCell(pos, CellType.Obstacle, gameObject);
            mainCamera = Camera.main;
        }

        public bool TrySlide(Vector2Int direction)
        {
            if (IsSliding) return false;

            var target = GridPosition + direction;

            // 障害物は空きマスにだけスライドできる
            if (!GridManager.Instance.IsEmpty(target)) return false;

            // スライド先を探索（壁や他のオブジェクトにぶつかるまで滑る）
            var slideTarget = GridPosition;
            var next = GridPosition + direction;

            while (GridManager.Instance.IsEmpty(next))
            {
                slideTarget = next;
                next += direction;
            }

            if (slideTarget == GridPosition) return false;

            GridManager.Instance.MoveOccupant(GridPosition, slideTarget);
            GridPosition = slideTarget;
            StartCoroutine(AnimateSlide(slideTarget));
            return true;
        }

        public static Vector2Int? DetectSwipeDirection(Vector2 startScreen, Vector2 endScreen, float minDistance = 30f)
        {
            var delta = endScreen - startScreen;
            if (delta.magnitude < minDistance) return null;

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                return delta.x > 0 ? Vector2Int.right : Vector2Int.left;
            else
                return delta.y > 0 ? Vector2Int.up : Vector2Int.down;
        }

        public ObstacleController RaycastObstacle(Vector2 screenPos)
        {
            if (mainCamera == null) mainCamera = Camera.main;

            var ray = mainCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit))
                return hit.collider.GetComponent<ObstacleController>();

            return null;
        }

        private IEnumerator AnimateSlide(Vector2Int target)
        {
            IsSliding = true;
            var targetWorld = GridManager.Instance.GridToWorld(target);

            while (Vector3.Distance(transform.position, targetWorld) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, targetWorld, slideSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = targetWorld;
            IsSliding = false;
        }
    }
}
