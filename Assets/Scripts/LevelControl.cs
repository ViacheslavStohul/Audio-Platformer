using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts
{
    public class LevelControl : MonoBehaviour
    {
        public void NavigateToLevel(string level) => SceneManager.LoadScene(level);

        public void NavigateToNextLevel() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);

        public void ExitGame()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
            Application.Quit();
        }
    }
}
