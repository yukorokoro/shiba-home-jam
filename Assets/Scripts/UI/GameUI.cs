using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ShibaHomeJam.Core;

namespace ShibaHomeJam.UI
{
    public class GameUI : MonoBehaviour
    {
        [SerializeField] private Text levelText;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject clearPanel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button nextButton;

        private void Awake()
        {
            if (retryButton != null)
                retryButton.onClick.AddListener(OnRetryClicked);
            if (nextButton != null)
                nextButton.onClick.AddListener(OnNextClicked);
        }

        private void Start()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (clearPanel != null) clearPanel.SetActive(false);
            if (levelText != null) levelText.text = "Level 1";

            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged += OnGameStateChanged;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.GameOver:
                    if (gameOverPanel != null) gameOverPanel.SetActive(true);
                    break;
                case GameState.Clear:
                    if (clearPanel != null) clearPanel.SetActive(true);
                    break;
                case GameState.Playing:
                    if (gameOverPanel != null) gameOverPanel.SetActive(false);
                    if (clearPanel != null) clearPanel.SetActive(false);
                    break;
            }
        }

        private void OnRetryClicked()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnNextClicked()
        {
            // For now just reload same scene
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
