using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public void PlayGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("playScene");
    }
    public void QuitGame()
    {
        Application.Quit();
    }
}
