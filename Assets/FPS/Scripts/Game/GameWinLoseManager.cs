using UnityEngine;

public class GameWinLoseManager: MonoBehaviour
{
    public static GameObject loseScreen;

    private void Awake()
    {
        loseScreen = GameObject.FindWithTag("LoseScene");
        loseScreen.SetActive(false);
        Time.timeScale = 1f;
    }
    public static void LoseGame()
    {
        Debug.Log("Lost game");
        Time.timeScale = 0f;
        loseScreen.SetActive(true);
    }

    public static void WinGame()
    {
        Debug.Log("Won game");
        Time.timeScale = 0f;
        loseScreen.SetActive(true);
    }
}
