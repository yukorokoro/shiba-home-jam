using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShibaHomeJam.Core
{
    public class BootInitializer : MonoBehaviour
    {
        private void Start()
        {
            SceneManager.LoadScene("Home");
        }
    }
}
