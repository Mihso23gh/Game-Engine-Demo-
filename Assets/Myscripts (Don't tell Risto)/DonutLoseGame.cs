using Unity.FPS.AI;
using UnityEngine;

public class DonutLoseGame : MonoBehaviour
{
    private SphereCollider col;
    [SerializeField] private GameObject loseScreen;

    private void Awake()
    {
        col = GetComponent<SphereCollider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger entered" + other.gameObject.name);
        if (other.gameObject.GetComponent<EnemyController>() != null)
        {
            Debug.Log("Lose game");
            Time.timeScale = 0f;
            loseScreen.SetActive(true);
        }
    }
}
