using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShibaHomeJam.UI
{
    public class HomeUI : MonoBehaviour
    {
        [SerializeField] private Button startButton;

        private void Awake()
        {
            if (startButton != null)
                startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnStartClicked()
        {
            SceneManager.LoadScene("Game");
        }
    }
}
