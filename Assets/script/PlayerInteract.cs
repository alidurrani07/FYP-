using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    public float interactRange = 3f;
    private NPCInteractable currentNPC;

    private void Update()
    {
        CheckForNPC();

        if (currentNPC != null && Input.GetKeyDown(KeyCode.E))
        {
            currentNPC.OnInteract();
        }
    }

    void CheckForNPC()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, interactRange);
        currentNPC = null;

        foreach (Collider col in colliders)
        {
            if (col.TryGetComponent(out NPCInteractable npc))
            {
                currentNPC = npc;
                UIManager.Instance.ShowInteractPrompt(true); // show text
                return;
            }
        }

        UIManager.Instance.ShowInteractPrompt(false); // hide if not near NPC
    }
}
