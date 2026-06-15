using UnityEngine;
using UnityEngine.InputSystem;   // NEW INPUT SYSTEM

public class TriggerUIInteraction : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject promptPanel;   // "Press E to Read"
    public GameObject mainPanel;     // Actual reading UI

    private bool isPlayerInside = false;
    private bool mainOpen = false;

    private void Start()
    {
        if (promptPanel != null)
            promptPanel.SetActive(false);

        if (mainPanel != null)
            mainPanel.SetActive(false);
    }

    private void Update()
    {
        if (!isPlayerInside || Keyboard.current == null)
            return;

        // Press E to toggle the main panel
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            mainOpen = !mainOpen;
            mainPanel.SetActive(mainOpen);

            // Hide prompt when main is open, show prompt when main is closed
            promptPanel.SetActive(!mainOpen);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = true;

            // Show "Press E to Read" prompt when entering
            if (promptPanel != null)
                promptPanel.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;

            // Hide everything when leaving
            mainOpen = false;

            if (promptPanel != null)
                promptPanel.SetActive(false);

            if (mainPanel != null)
                mainPanel.SetActive(false);
        }
    }
}
