using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public abstract class DesaturatorBase : MonoBehaviour
{
    public abstract IEnumerator Apply(float duration, float targetSaturation);
}