using UnityEngine;

public class Bomb : MonoBehaviour
{
    public void Defuse()
    {
        gameObject.SetActive(false);
    }
}