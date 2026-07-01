using UnityEngine;

public class BombAutoDefuse : MonoBehaviour
{
    private bool isDone = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isDone)
        {
            isDone = true;

            Debug.Log("BombAutoDefuse ignored. Demo Scene 1 uses DemoSceneBombMission for bomb placement.");

            if (GameManager.instance != null)
                GameManager.instance.BombDefused();
        }
    }
}
