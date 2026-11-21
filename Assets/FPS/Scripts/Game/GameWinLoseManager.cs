using UnityEngine;

public class GameWinLoseManager: MonoBehaviour
{
    public static GameObject loseScreen;

    private void Awake()
    {
        loseScreen = GameObject.FindWithTag("LoseScene");
        loseScreen.SetActive(false);
    }
    public static void LoseGame()
    {
        Time.timeScale = 0f;
        loseScreen.SetActive(true);
    }

    public static void WinGame()
    {
        Time.timeScale = 0f;
        loseScreen.SetActive(true);
    }
}
