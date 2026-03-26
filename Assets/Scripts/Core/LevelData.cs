using UnityEngine;
using System;
using System.Collections.Generic;

namespace ShibaHomeJam.Core
{
    [Serializable]
    public class LevelData
    {
        public int level;
        public int width;
        public int height;
        public Position shiba;
        public Position goal;
        public Position[] obstacles;
        public EnemyData[] enemies;
        public int turnLimit;
    }

    [Serializable]
    public class Position
    {
        public int x;
        public int y;

        public Vector2Int ToVector2Int() => new Vector2Int(x, y);
    }

    [Serializable]
    public class EnemyData
    {
        public string type; // "cat" or "thief"
        public Position position;
    }

    [Serializable]
    public class LevelCollection
    {
        public LevelData[] levels;
    }

    public static class LevelLoader
    {
        public static LevelCollection LoadLevels()
        {
            var json = Resources.Load<TextAsset>("Levels/levels");
            if (json == null)
            {
                Debug.LogError("levels.json not found in Resources/Levels/");
                return null;
            }
            return JsonUtility.FromJson<LevelCollection>(json.text);
        }

        public static LevelData LoadLevel(int levelNumber)
        {
            var collection = LoadLevels();
            if (collection == null) return null;

            foreach (var level in collection.levels)
            {
                if (level.level == levelNumber)
                    return level;
            }

            Debug.LogError($"Level {levelNumber} not found");
            return null;
        }
    }
}
