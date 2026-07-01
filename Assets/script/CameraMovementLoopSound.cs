using UnityEngine;

[DisallowMultipleComponent]
public class CameraMovementLoopSound : MonoBehaviour
{
    [SerializeField] private string resourceClipName = "truckmoving";
    [SerializeField] private float movementThreshold = 0.01f;
    [SerializeField] private float rotationThreshold = 0.1f;
    [SerializeField] private float stopDelay = 0.15f;
    [SerializeField] private float volume = 1f;

    private AudioSource audioSource;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float stillTimer;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        AudioClip clip = Resources.Load<AudioClip>(resourceClipName);
        if (clip == null)
        {
            Debug.LogWarning($"Camera movement sound clip not found in Resources: {resourceClipName}");
        }

        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = Mathf.Clamp01(volume);

        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    private void LateUpdate()
    {
        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        float movementSpeed = Vector3.Distance(transform.position, lastPosition) / deltaTime;
        float rotationSpeed = Quaternion.Angle(transform.rotation, lastRotation) / deltaTime;
        bool isMoving = movementSpeed > movementThreshold || rotationSpeed > rotationThreshold;

        if (isMoving)
        {
            stillTimer = 0f;

            if (audioSource.clip != null && !audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
        else
        {
            stillTimer += Time.deltaTime;

            if (stillTimer >= stopDelay && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    private void OnDisable()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }
}
