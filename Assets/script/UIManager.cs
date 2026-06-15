using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    public GameObject interactPromptPanel;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        interactPromptPanel.SetActive(false); // hide by default
    }

    public void ShowInteractPrompt(bool show)
    {
        interactPromptPanel.SetActive(show);
    }
}
