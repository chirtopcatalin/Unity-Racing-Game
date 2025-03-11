using UnityEngine;

public class ChangeScene : MonoBehaviour
{
    public void ChangeToScene()
    {
        // scena trebuie sa fie in fisierul scenes
        UnityEngine.SceneManagement.SceneManager.LoadScene("Assets/facultate/playScene.unity");
    }
}
