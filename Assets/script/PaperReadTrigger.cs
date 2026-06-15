using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;
using UnityEngine.SceneManagement;

public class PaperReadTrigger : MonoBehaviour
{
    [Header("UI Elements")]
    public CanvasGroup panelGroup;
    public Text typingText;
    public GameObject buttonsGroup;

    [Header("Typing Settings")]
    [TextArea(3, 8)]
    public string fullText;
    public float typingSpeed = 0.05f;

    private bool triggered = false;
    private bool canChoose = false;   // controls when input is allowed

    private void Start()
    {
        panelGroup.gameObject.SetActive(false);
        panelGroup.alpha = 0f;

        typingText.text = "";
        buttonsGroup.SetActive(false);
    }

    private void Update()
    {
        // Only allow input after typing is complete
        if (!canChoose) return;

        if (Input.GetKeyDown(KeyCode.A))
        {
            LoadArmyScene();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            LoadRebelScene();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !triggered)
        {
            triggered = true;
            StartCoroutine(Sequence());
        }
    }

    private IEnumerator Sequence()
    {
        // 1. Wait before showing panel
        yield return new WaitForSeconds(5f);

        // 2. Show panel with animation
        panelGroup.gameObject.SetActive(true);
        panelGroup.alpha = 0f;

        panelGroup.DOFade(1f, 0.8f);
        panelGroup.transform.DOScale(1f, 0.8f)
            .From(0.65f)
            .SetEase(Ease.OutBack);

        // 3. Small delay before typing
        yield return new WaitForSeconds(0.4f);

        // 4. Start typing
        StartCoroutine(TypeText());
    }

    private IEnumerator TypeText()
    {
        typingText.text = "";

        foreach (char c in fullText)
        {
            typingText.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }

        // 5. Show buttons after typing
        buttonsGroup.SetActive(true);

        // Enable keyboard selection
        canChoose = true;
    }

    // UI Button OR Keyboard (R)
    public void LoadRebelScene()
    {
        SceneManager.LoadScene("RebelScene");
    }

    // UI Button OR Keyboard (A)
    public void LoadArmyScene()
    {
        SceneManager.LoadScene("ArmyScene");
    }
}