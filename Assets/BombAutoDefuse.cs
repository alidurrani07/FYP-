using UnityEngine;

public class BombAutoDefuse : MonoBehaviour
{
    private bool isDone = false;


    void Start()
    {
        Debug.Log("Script Working");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isDone)
        {
            isDone = true;

            Debug.Log("Bomb Defused!");

            gameObject.SetActive(false);

            GameManager.instance.BombDefused();
        }
    }
}