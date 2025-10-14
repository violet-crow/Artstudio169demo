using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainRoomMoodController : MonoBehaviour
{
    [Header("Lights")]
    public Light[] ceilingLights;
    public Color ceilingLightTargetColor = Color.red;
    public float lightFadeDuration = 1.0f;
    [Tooltip("Optional: emissive mesh renderers for ceiling fixtures if you use emissive materials.")]
    public Renderer[] emissiveRenderers;
    public Color emissiveTargetColor = Color.red;
    public float emissiveIntensity = 1.0f; // multiplier applied to target color
    public float emissiveFadeDuration = 1.0f;
    public string emissiveColorProperty = "_EmissionColor";

    [Header("Audio")]
    public AudioSource musicSource;         // main music
    public AudioSource partyAmbientSource;  // current party ambience
    public AudioSource fightAmbientSource;  // alternate ambience (fighting)
    public float audioFadeDuration = 1.0f;

    [Header("Desaturation (optional)")]
    public DesaturatorBase desaturator;     // assign one of the desaturator components below
    public float desaturationTarget = -40f; // in “saturation” units, e.g., -40 moderately desaturated
    public float desaturationFadeDuration = 0.75f;

    [Header("Guest culling")]
    [Range(0f, 1f)]
    public float guestCullPercent = 0.5f;
    [Tooltip("Guests to potentially disable. If empty, will try to gather from guestsRoot or tag.")]
    public List<GameObject> guestObjects = new List<GameObject>();
    public Transform guestsRoot;            // optional: parent that holds guest NPCs
    public string guestTag;                 // optional: tag to collect guests at runtime
    public bool disableGuestsInsteadOfDestroy = true;

    [Header("Execution")]
    public bool triggerOnce = true;
    private bool alreadyTriggered;

    public void Activate()
    {
        if (triggerOnce && alreadyTriggered) return;
        alreadyTriggered = true;

        // Lazy collection if needed
        if (guestObjects.Count == 0)
        {
            if (guestsRoot)
            {
                foreach (Transform child in guestsRoot)
                    guestObjects.Add(child.gameObject);
            }
            else if (!string.IsNullOrEmpty(guestTag))
            {
                var found = GameObject.FindGameObjectsWithTag(guestTag);
                guestObjects.AddRange(found);
            }
        }

        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // Start audio crossfade and visual changes in parallel
        var tasks = new List<Coroutine>();

        tasks.Add(StartCoroutine(FadeCeilingLights()));
        tasks.Add(StartCoroutine(FadeEmissive()));
        tasks.Add(StartCoroutine(CrossfadeAudio()));
        tasks.Add(StartCoroutine(CullGuestsGradually()));

        if (desaturator)
            tasks.Add(StartCoroutine(desaturator.Apply(desaturationFadeDuration, desaturationTarget)));

        foreach (var t in tasks)
            yield return t;
    }

    IEnumerator FadeCeilingLights()
    {
        if (ceilingLights == null || ceilingLights.Length == 0 || lightFadeDuration <= 0f)
            yield break;

        var originals = new Color[ceilingLights.Length];
        for (int i = 0; i < ceilingLights.Length; i++)
        {
            if (ceilingLights[i])
                originals[i] = ceilingLights[i].color;
        }

        float t = 0f;
        while (t < lightFadeDuration)
        {
            float a = t / lightFadeDuration;
            for (int i = 0; i < ceilingLights.Length; i++)
            {
                var l = ceilingLights[i];
                if (l) l.color = Color.Lerp(originals[i], ceilingLightTargetColor, a);
            }
            t += Time.deltaTime;
            yield return null;
        }
        for (int i = 0; i < ceilingLights.Length; i++)
        {
            var l = ceilingLights[i];
            if (l) l.color = ceilingLightTargetColor;
        }
    }

    IEnumerator FadeEmissive()
    {
        if (emissiveRenderers == null || emissiveRenderers.Length == 0 || emissiveFadeDuration <= 0f)
            yield break;

        // Capture originals
        var blocks = new MaterialPropertyBlock[emissiveRenderers.Length];
        var originalColors = new Color[emissiveRenderers.Length];

        for (int i = 0; i < emissiveRenderers.Length; i++)
        {
            var r = emissiveRenderers[i];
            if (!r) continue;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            originalColors[i] = block.GetColor(emissiveColorProperty);
            blocks[i] = block;

            // Ensure emission keyword on (for legacy/URP)
            foreach (var mat in r.sharedMaterials)
                if (mat) mat.EnableKeyword("_EMISSION");
        }

        var target = emissiveTargetColor * emissiveIntensity;
        float t = 0f;
        while (t < emissiveFadeDuration)
        {
            float a = t / emissiveFadeDuration;
            for (int i = 0; i < emissiveRenderers.Length; i++)
            {
                var r = emissiveRenderers[i];
                if (!r) continue;
                var c = Color.Lerp(originalColors[i], target, a);
                var block = blocks[i];
                block.SetColor(emissiveColorProperty, c);
                r.SetPropertyBlock(block);
            }
            t += Time.deltaTime;
            yield return null;
        }
        for (int i = 0; i < emissiveRenderers.Length; i++)
        {
            var r = emissiveRenderers[i];
            if (!r) continue;
            var block = blocks[i];
            block.SetColor(emissiveColorProperty, target);
            r.SetPropertyBlock(block);
        }
    }

    IEnumerator CrossfadeAudio()
    {
        // Ensure fight ambience is ready
        if (fightAmbientSource)
        {
            if (!fightAmbientSource.isPlaying)
                fightAmbientSource.Play();
            fightAmbientSource.volume = 0f;
            fightAmbientSource.loop = true;
        }

        float startMusic = musicSource ? musicSource.volume : 0f;
        float startParty = partyAmbientSource ? partyAmbientSource.volume : 0f;
        float startFight = fightAmbientSource ? fightAmbientSource.volume : 0f;

        float t = 0f;
        while (t < audioFadeDuration)
        {
            float a = t / audioFadeDuration;
            if (musicSource) musicSource.volume = Mathf.Lerp(startMusic, 0f, a);
            if (partyAmbientSource) partyAmbientSource.volume = Mathf.Lerp(startParty, 0f, a);
            if (fightAmbientSource) fightAmbientSource.volume = Mathf.Lerp(startFight, 1f, a);

            t += Time.deltaTime;
            yield return null;
        }

        if (musicSource)
        {
            musicSource.volume = 0f;
            musicSource.Stop();
        }

        if (partyAmbientSource)
        {
            partyAmbientSource.volume = 0f;
            partyAmbientSource.Stop();
        }

        if (fightAmbientSource)
            fightAmbientSource.volume = 1f;
    }

    IEnumerator CullGuestsGradually()
    {
        if (guestObjects == null || guestObjects.Count == 0 || guestCullPercent <= 0f)
            yield break;

        // Build a working list of alive/active guests
        var candidates = new List<GameObject>();
        foreach (var g in guestObjects)
            if (g) candidates.Add(g);

        int toCull = Mathf.RoundToInt(candidates.Count * guestCullPercent);
        if (toCull <= 0) yield break;

        // Shuffle
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        // Disable over a few frames to avoid hitches if many objects
        int batchSize = Mathf.Clamp(toCull / 5, 1, 20);
        int culled = 0;

        for (int i = 0; i < candidates.Count && culled < toCull; i++)
        {
            var g = candidates[i];
            if (!g) continue;

            if (disableGuestsInsteadOfDestroy)
                g.SetActive(false);
            else
                Destroy(g);

            culled++;

            if (culled % batchSize == 0)
                yield return null;
        }
    }
}