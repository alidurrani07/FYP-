using UnityEngine;

public class RotateArrowY : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 100f; // degrees per second

    void Update()
    {
        // Rotate around Y axis
        transform.Rotate(rotationSpeed * Time.deltaTime,0f , 0f);
    }
}