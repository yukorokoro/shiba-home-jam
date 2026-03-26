using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

namespace ShibaHomeJam.Editor
{
    public static class SceneSetup
    {
        [MenuItem("ShibaHomeJam/Setup All Scenes")]
        public static void SetupAllScenes()
        {
            CreateBootScene();
            CreateHomeScene();
            CreateGameScene();
            SetupBuildSettings();
            Debug.Log("All scenes created and build settings configured!");
        }

        private static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Main Camera
            var cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            var cam = cameraObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            cameraObj.AddComponent<AudioListener>();

            // BootInitializer
            var bootObj = new GameObject("BootInitializer");
            bootObj.AddComponent<Core.BootInitializer>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Boot.unity");
            Debug.Log("Boot.unity created");
        }

        private static void CreateHomeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Main Camera
            var cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            var cam = cameraObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.95f, 0.85f, 0.6f); // 暖かい柴犬カラー
            cameraObj.AddComponent<AudioListener>();

            // Canvas
            var canvasObj = new GameObject("Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            canvasObj.AddComponent<GraphicRaycaster>();

            // HomeUI
            var homeUI = canvasObj.AddComponent<UI.HomeUI>();

            // TitleText
            var titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(canvasObj.transform, false);
            var titleText = titleObj.AddComponent<Text>();
            titleText.text = "Shiba Home Jam";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 72;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.4f, 0.25f, 0.1f);
            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.6f);
            titleRect.anchorMax = new Vector2(1, 0.85f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            // StartButton
            var buttonObj = new GameObject("StartButton");
            buttonObj.transform.SetParent(canvasObj.transform, false);
            var buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.9f, 0.5f, 0.2f);
            var button = buttonObj.AddComponent<Button>();
            var buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.25f, 0.3f);
            buttonRect.anchorMax = new Vector2(0.75f, 0.4f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            var buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(buttonObj.transform, false);
            var buttonText = buttonTextObj.AddComponent<Text>();
            buttonText.text = "Start";
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.fontSize = 48;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.color = Color.white;
            var buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.offsetMin = Vector2.zero;
            buttonTextRect.offsetMax = Vector2.zero;

            // Serialize HomeUI reference
            var so = new SerializedObject(homeUI);
            so.FindProperty("startButton").objectReferenceValue = button;
            so.ApplyModifiedProperties();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Home.unity");
            Debug.Log("Home.unity created");
        }

        private static void CreateGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Main Camera (Orthographic, top-down)
            var cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            var cam = cameraObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.85f, 0.9f, 0.75f); // 草原カラー
            cameraObj.transform.position = new Vector3(0, 5, -10);
            cameraObj.transform.eulerAngles = new Vector3(60, 0, 0);
            cameraObj.AddComponent<AudioListener>();

            // Directional Light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.2f;
            lightObj.transform.eulerAngles = new Vector3(50, -30, 0);

            // GridManager
            var gridObj = new GameObject("GridManager");
            gridObj.AddComponent<Core.GridManager>();

            // GameManager
            var gmObj = new GameObject("GameManager");
            var gm = gmObj.AddComponent<Core.GameManager>();

            // Canvas
            var canvasObj = new GameObject("Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            canvasObj.AddComponent<GraphicRaycaster>();

            // GameUI
            var gameUI = canvasObj.AddComponent<UI.GameUI>();

            // LevelText
            var levelTextObj = new GameObject("LevelText");
            levelTextObj.transform.SetParent(canvasObj.transform, false);
            var levelText = levelTextObj.AddComponent<Text>();
            levelText.text = "Level 1";
            levelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            levelText.fontSize = 48;
            levelText.alignment = TextAnchor.MiddleCenter;
            levelText.color = new Color(0.2f, 0.2f, 0.2f);
            var levelRect = levelTextObj.GetComponent<RectTransform>();
            levelRect.anchorMin = new Vector2(0.2f, 0.9f);
            levelRect.anchorMax = new Vector2(0.8f, 0.98f);
            levelRect.offsetMin = Vector2.zero;
            levelRect.offsetMax = Vector2.zero;

            // GameOverPanel
            var gameOverPanel = CreatePanel(canvasObj.transform, "GameOverPanel", "Game Over",
                "RetryButton", "Retry", new Color(0.8f, 0.2f, 0.2f));
            gameOverPanel.SetActive(false);

            // ClearPanel
            var clearPanel = CreatePanel(canvasObj.transform, "ClearPanel", "Level Clear!",
                "NextButton", "Next", new Color(0.2f, 0.7f, 0.3f));
            clearPanel.SetActive(false);

            // Serialize GameUI references
            var so = new SerializedObject(gameUI);
            so.FindProperty("levelText").objectReferenceValue = levelText;
            so.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
            so.FindProperty("clearPanel").objectReferenceValue = clearPanel;
            so.FindProperty("retryButton").objectReferenceValue = gameOverPanel.transform.Find("RetryButton").GetComponent<Button>();
            so.FindProperty("nextButton").objectReferenceValue = clearPanel.transform.Find("NextButton").GetComponent<Button>();
            so.ApplyModifiedProperties();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Game.unity");
            Debug.Log("Game.unity created");
        }

        private static GameObject CreatePanel(Transform parent, string panelName, string message,
            string buttonName, string buttonLabel, Color buttonColor)
        {
            // Panel background
            var panel = new GameObject(panelName);
            panel.transform.SetParent(parent, false);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.7f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Message text
            var textObj = new GameObject("MessageText");
            textObj.transform.SetParent(panel.transform, false);
            var text = textObj.AddComponent<Text>();
            text.text = message;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 64;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.1f, 0.55f);
            textRect.anchorMax = new Vector2(0.9f, 0.75f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            // Button
            var buttonObj = new GameObject(buttonName);
            buttonObj.transform.SetParent(panel.transform, false);
            var btnImage = buttonObj.AddComponent<Image>();
            btnImage.color = buttonColor;
            buttonObj.AddComponent<Button>();
            var btnRect = buttonObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.3f, 0.35f);
            btnRect.anchorMax = new Vector2(0.7f, 0.45f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            var btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(buttonObj.transform, false);
            var btnText = btnTextObj.AddComponent<Text>();
            btnText.text = buttonLabel;
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnText.fontSize = 42;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            var btnTextRect = btnTextObj.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            return panel;
        }

        private static void SetupBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene("Assets/Scenes/Boot.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Home.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Game.unity", true)
            };
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("Build settings configured: Boot(0), Home(1), Game(2)");
        }
    }
}
