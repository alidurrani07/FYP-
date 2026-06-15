using UnityEngine;

public class PlayerInteract2 : MonoBehaviour
{
    public float distance = 3f;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            Ray ray = new Ray(transform.position, transform.forward);
            RaycastHit hit;

            // 👉 YAHAN ADD KARO 👇
            Debug.DrawRay(transform.position, transform.forward * distance, Color.red);

            if (Physics.Raycast(ray, out hit, distance))
            {
                if (hit.collider.CompareTag("Bomb"))
                {
                    Bomb bomb = hit.collider.GetComponent<Bomb>();
                    if (bomb != null)
                    {
                        bomb.Defuse();
                    }
                }
            }
        }
    }
}