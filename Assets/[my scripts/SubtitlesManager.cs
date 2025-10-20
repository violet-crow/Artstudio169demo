using System.Collections;
using UnityEngine;

public class SubtitlesManager : MonoBehaviour
{
    public static SubtitlesManager Instance { get; private set; }
    public TMPro.TextMeshProUGUI text;
    private void Awake()
    {
        Instance = this;
        if (text) text.text = "";
        gameObject.SetActive(false);
    }

    public void Show(string content, float durationSeconds)
    {
        StopAllCoroutines();
        gameObject.SetActive(true);
        if (text) text.text = content;
        StartCoroutine(HideAfter(durationSeconds));
    }

    public void Hide()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    private IEnumerator HideAfter(float seconds)
    {
        // Use unscaled time so it works even if timeScale changes
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Hide();
    }
}