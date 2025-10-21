using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

[System.Serializable]
public class DialogueEntry
{
public AudioClip clip;
[TextArea] public string subtitles;
}

public class NPCDialogue : MonoBehaviour
{
    [Header("Setup")]
    public AudioSource audioSource;
    public GameObject talkPrompt; // world-space "(A) Talk" prefab
    [Header("Legacy single-line (optional)")]
    public AudioClip clip;
    [TextArea] public string subtitleText;

    [Header("Multi-line dialogue")]
    public List<DialogueEntry> dialogues = new List<DialogueEntry>();
    public bool randomize = false;           // pick a random line instead of cycling
    public bool avoidImmediateRepeat = true; // try not to repeat the last line when randomizing

    [Header("Behavior")]
    public float showPromptDistance = 2.0f; // meters
    public bool oneShot = false;            // optional: only talk once (after first interaction)

    [Header("Prompt/interaction gating")]
    [Range(0f, 1f)] public float lookDotThreshold = 0.6f; // 0.6 ≈ 53°, 0.8 ≈ 36°

    Transform playerHead;
    bool clipPlaying;
    int nextIndex = 0;
    int lastPlayedIndex = -1;
    float lastDialogueDuration = 0f;

    void Start()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        if (talkPrompt) talkPrompt.SetActive(false);
        playerHead = Camera.main ? Camera.main.transform : null;
    }

    void Update()
    {
        // Find player head
        if (!playerHead && Camera.main) playerHead = Camera.main.transform;
        if (!playerHead) return;

        // Proximity
        float dist = Vector3.Distance(playerHead.position, transform.position);
        bool inRange = dist <= showPromptDistance && !clipPlaying;

        // Looking check (full 3D)
        bool looking = false;
        if (inRange)
        {
            Vector3 toNPC = (transform.position - playerHead.position).normalized;
            float dot = Vector3.Dot(playerHead.forward, toNPC);
            looking = dot >= lookDotThreshold;
        }

        bool canPromptAndInteract = inRange && looking;

        if (talkPrompt) talkPrompt.SetActive(canPromptAndInteract);

        if (canPromptAndInteract && GetRightAButtonDown())
            PlayDialogue();
    }

    bool GetRightAButtonDown()
    {
        // Oculus A button (right controller)
        return OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch);
    }

    void PlayDialogue()
    {
        DialogueEntry entry = SelectDialogueEntry();

        // If no entries and no legacy clip, do nothing
        if (entry == null && clip == null && string.IsNullOrEmpty(subtitleText))
            return;

        clipPlaying = true;
        if (talkPrompt) talkPrompt.SetActive(false);

        // Determine audio and duration
        AudioClip chosenClip = entry != null ? entry.clip : clip;
        string chosenSub = entry != null ? entry.subtitles : subtitleText;

        if (chosenClip && audioSource)
        {
            audioSource.clip = chosenClip;
            audioSource.Play();
        }

        lastDialogueDuration = chosenClip ? chosenClip.length : 3f;

        if (!string.IsNullOrEmpty(chosenSub) && SubtitlesManager.Instance)
        {
            SubtitlesManager.Instance.Show(chosenSub, lastDialogueDuration);
        }

        // Advance index for next time (if using multi-line and not random)
        if (entry != null && !randomize)
        {
            nextIndex = (nextIndex + 1) % dialogues.Count;
            lastPlayedIndex = nextIndex == 0 ? dialogues.Count - 1 : nextIndex - 1;
        }
        else if (entry != null && randomize)
        {
            // Track last played so we can avoid repeating when randomizing
            lastPlayedIndex = GetIndexOfEntry(entry);
        }

        StartCoroutine(WaitForDialogueEnd());
    }

    DialogueEntry SelectDialogueEntry()
    {
        if (dialogues == null || dialogues.Count == 0)
            return null;

        if (!randomize)
        {
            return dialogues[nextIndex];
        }

        // Random selection
        if (dialogues.Count == 1)
            return dialogues[0];

        int idx = Random.Range(0, dialogues.Count);

        if (avoidImmediateRepeat && lastPlayedIndex >= 0)
        {
            // Try up to a few times to avoid picking the same index
            const int attempts = 5;
            int tries = 0;
            while (idx == lastPlayedIndex && tries < attempts)
            {
                idx = Random.Range(0, dialogues.Count);
                tries++;
            }
        }

        return dialogues[idx];
    }

    int GetIndexOfEntry(DialogueEntry entry)
    {
        if (dialogues == null) return -1;
        for (int i = 0; i < dialogues.Count; i++)
            if (dialogues[i] == entry) return i;
        return -1;
    }

    IEnumerator WaitForDialogueEnd()
    {
        // If there is audio, wait for it to finish; otherwise wait for the subtitle duration
        if (audioSource && audioSource.clip && audioSource.isPlaying)
        {
            while (audioSource.isPlaying)
                yield return null;
        }
        else
        {
            yield return new WaitForSeconds(lastDialogueDuration);
        }

        clipPlaying = false;

        if (oneShot)
        {
            // Optional: disable interaction after first play
            enabled = false;
            if (talkPrompt) talkPrompt.SetActive(false);
        }
    }
}