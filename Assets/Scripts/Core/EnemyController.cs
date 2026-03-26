using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace ShibaHomeJam.Core
{
    public enum EnemyType
    {
        Cat,
        Thief
    }

    public class EnemyController : MonoBehaviour
    {
        [SerializeField] private EnemyType enemyType = EnemyType.Cat;
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private int moveInterval = 2; // ターンごとの移動間隔

        public Vector2Int GridPosition { get; private set; }
        public EnemyType Type => enemyType;
        public bool IsMoving { get; private set; }

        private int turnCounter;

        public void Initialize(Vector2Int pos, EnemyType type)
        {
            GridPosition = pos;
            enemyType = type;
            transform.position = GridManager.Instance.GridToWorld(pos);
            GridManager.Instance.SetCell(pos, CellType.Enemy, gameObject);
            turnCounter = 0;
        }

        public bool TryAdvance(Vector2Int shibaPos)
        {
            turnCounter++;
            if (turnCounter < moveInterval) return false;
            turnCounter = 0;

            var direction = ChooseDirection(shibaPos);
            if (!direction.HasValue) return false;

            var target = GridPosition + direction.Value;
            var cell = GridManager.Instance.GetCell(target);

            // 柴犬のマスに入ったら捕まえた
            if (cell == CellType.Shiba)
            {
                GridManager.Instance.SetCell(GridPosition, CellType.Empty);
                GridPosition = target;
                GridManager.Instance.SetCell(target, CellType.Enemy, gameObject);
                StartCoroutine(AnimateMove(target));
                return true; // 柴犬を捕まえた
            }

            if (cell != CellType.Empty) return false;

            GridManager.Instance.MoveOccupant(GridPosition, target);
            GridPosition = target;
            StartCoroutine(AnimateMove(target));
            return false;
        }

        private Vector2Int? ChooseDirection(Vector2Int shibaPos)
        {
            switch (enemyType)
            {
                case EnemyType.Cat:
                    return ChaseDirect(shibaPos);
                case EnemyType.Thief:
                    return ChaseFlank(shibaPos);
                default:
                    return ChaseDirect(shibaPos);
            }
        }

        // 猫: 柴犬に向かって直線的に追いかける
        private Vector2Int? ChaseDirect(Vector2Int shibaPos)
        {
            var diff = shibaPos - GridPosition;

            var candidates = new List<Vector2Int>();

            if (diff.x > 0) candidates.Add(Vector2Int.right);
            else if (diff.x < 0) candidates.Add(Vector2Int.left);

            if (diff.y > 0) candidates.Add(Vector2Int.up);
            else if (diff.y < 0) candidates.Add(Vector2Int.down);

            // 距離が大きい軸を優先
            if (candidates.Count == 2 && Mathf.Abs(diff.x) < Mathf.Abs(diff.y))
            {
                (candidates[0], candidates[1]) = (candidates[1], candidates[0]);
            }

            foreach (var dir in candidates)
            {
                var target = GridPosition + dir;
                var cell = GridManager.Instance.GetCell(target);
                if (cell == CellType.Empty || cell == CellType.Shiba)
                    return dir;
            }

            return null;
        }

        // 泥棒: 柴犬の前方を回り込んで塞ぐ
        private Vector2Int? ChaseFlank(Vector2Int shibaPos)
        {
            // 柴犬の右側（進行方向前方）を狙う
            var flankTarget = shibaPos + Vector2Int.right * 2;

            var diff = flankTarget - GridPosition;
            var candidates = new List<Vector2Int>();

            if (diff.x > 0) candidates.Add(Vector2Int.right);
            else if (diff.x < 0) candidates.Add(Vector2Int.left);

            if (diff.y > 0) candidates.Add(Vector2Int.up);
            else if (diff.y < 0) candidates.Add(Vector2Int.down);

            foreach (var dir in candidates)
            {
                var target = GridPosition + dir;
                var cell = GridManager.Instance.GetCell(target);
                if (cell == CellType.Empty || cell == CellType.Shiba)
                    return dir;
            }

            // 回り込めない場合は直接追いかける
            return ChaseDirect(shibaPos);
        }

        private IEnumerator AnimateMove(Vector2Int target)
        {
            IsMoving = true;
            var targetWorld = GridManager.Instance.GridToWorld(target);

            while (Vector3.Distance(transform.position, targetWorld) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, targetWorld, moveSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = targetWorld;
            IsMoving = false;
        }
    }
}
