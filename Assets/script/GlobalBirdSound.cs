using UnityEngine;

public class GlobalBirdSound : MonoBehaviour
{
    public static GlobalBirdSound Instance;

    [Header("Bird Sound")]
    [SerializeField] private AudioClip birdSound;
    [SerializeField] private float volume = 0.5f;

    private AudioSource audioSource;

    private void Awake()
    {
        // Prevent duplicate bird sound when changing scenes
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.clip = birdSound;
        audioSource.loop = true;
        audioSource.playOnAwake = true;
        audioSource.volume = volume;
        audioSource.spatialBlend = 0f; // 2D global sound
    }

    private void Start()
    {
        if (birdSound != null && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
        else if (birdSound == null)
        {
            Debug.LogWarning("Bird sound AudioClip is missing on GlobalBirdSound.");
        }
    }

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);

        if (audioSource != null)
        {
            audioSource.volume = volume;
        }
    }

    public void StopBirdSound()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    public void PlayBirdSound()
    {
        if (audioSource != null && birdSound != null && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }
}