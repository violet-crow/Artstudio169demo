using System.Collections;   
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class URPDesaturator : DesaturatorBase
{
    public Volume volume;
    [Range(-100f, 100f)] public float defaultSaturation = 0f;
    public override IEnumerator Apply(float duration, float targetSaturation)
    {
        if (!volume || !volume.profile) yield break;
        if (!volume.profile.TryGet<ColorAdjustments>(out var colorAdj)) yield break;

        float start = colorAdj.saturation.value;
        float t = 0f;
        while (t < duration)
        {
            float a = t / duration;
            colorAdj.saturation.value = Mathf.Lerp(start, targetSaturation, a);
            t += Time.deltaTime;
            yield return null;
        }
        colorAdj.saturation.value = targetSaturation;
    }

    private void OnDisable()
    {
        if (!volume || !volume.profile) return;
        if (volume.profile.TryGet<ColorAdjustments>(out var colorAdj))
            colorAdj.saturation.value = defaultSaturation;
    }
}