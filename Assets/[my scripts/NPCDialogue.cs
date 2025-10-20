using System.Collections;
using UnityEngine;
using UnityEngine.XR;
public class NPCDialogue : MonoBehaviour
{
    [Header("Setup")]
    public AudioSource audioSource;
    public AudioClip clip;
    [TextArea] public string subtitleText;
    public GameObject talkPrompt; // world-space "(A) Talk" prefab
    [Header("Behavior")]
    public float showPromptDistance = 2.0f; // meters
    public bool oneShot = false;            // optional: only talk once

    Transform playerHead;
    bool clipPlaying;
    bool lastAButtonState;

    void Start()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        if (talkPrompt) talkPrompt.SetActive(false);
        playerHead = Camera.main ? Camera.main.transform : null;
    }

    void Update()
    {
        // Find player head (Main Camera in XR rigs)
        if (!playerHead && Camera.main) playerHead = Camera.main.transform;
        if (!playerHead) return;

        // Proximity check
        float dist = Vector3.Distance(playerHead.position, transform.position);
        bool inRange = dist <= showPromptDistance && !clipPlaying;

        if (talkPrompt) talkPrompt.SetActive(inRange);

        // Listen for A button press
        if (inRange && GetRightAButtonDown())
            PlayDialogue();
    }

    bool GetRightAButtonDown()
    {
return OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch);
    }
    void PlayDialogue()
    {
        clipPlaying = true;
        if (talkPrompt) talkPrompt.SetActive(false);

        if (clip && audioSource)
        {
            audioSource.clip = clip;
            audioSource.Play();
        }

        if (!string.IsNullOrEmpty(subtitleText) && SubtitlesManager.Instance)
        {
            float duration = clip ? clip.length : 3f;
            SubtitlesManager.Instance.Show(subtitleText, duration);
        }

        StartCoroutine(WaitForClipEnd());
    }

    IEnumerator WaitForClipEnd()
    {
        while (audioSource && audioSource.isPlaying)
            yield return null;

        clipPlaying = false;
        if (oneShot)
        {
            // Optional: disable interaction after first play
            enabled = false;
            if (talkPrompt) talkPrompt.SetActive(false);
        }
    }
}