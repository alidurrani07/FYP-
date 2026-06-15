using UnityEngine;
using System.Collections;

public class NPCInteractable : MonoBehaviour
{
    [Header("Camera References")]
    public Camera mainCamera;            // Player camera
    public Camera cutsceneCamera;        // Cutscene camera

    [Header("Cutscene Settings")]
    public Animator cutsceneAnimator;    // Animator for cutscene camera (optional)
    public string animationTrigger = "Play";  // Trigger name in Animator
    public float cutsceneDuration = 5f;      // Force cutscene camera display time

    private bool cutscenePlaying = false;

    private void Start()
    {
        // Ensure cutscene camera is OFF at start
        if (cutsceneCamera != null)
            cutsceneCamera.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !cutscenePlaying)
        {
            StartCoroutine(PlayCutscene());
        }
    }

    /// <summary>
    /// Public method to trigger cutscene (e.g., from PlayerInteract)
    /// </summary>
    public void OnInteract()
    {
        if (!cutscenePlaying)
            StartCoroutine(PlayCutscene());
    }

    private IEnumerator PlayCutscene()
    {
        cutscenePlaying = true;

        // Disable main camera
        if (mainCamera != null)
            mainCamera.gameObject.SetActive(false);

        // Enable cutscene camera
        if (cutsceneCamera != null)
            cutsceneCamera.gameObject.SetActive(true);

        // Trigger animator if assigned
        if (cutsceneAnimator != null)
            cutsceneAnimator.SetTrigger(animationTrigger);

        // Wait for cutscene duration
        yield return new WaitForSeconds(cutsceneDuration);

        // Switch back to main camera
        if (cutsceneCamera != null)
            cutsceneCamera.gameObject.SetActive(false);

        if (mainCamera != null)
            mainCamera.gameObject.SetActive(true);

        cutscenePlaying = false;
    }
}
