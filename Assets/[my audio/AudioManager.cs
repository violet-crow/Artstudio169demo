using UnityEngine;
public class AudioManager : MonoBehaviour
{
    public AudioSource audioSource;
    void Start()
    {
        audioSource.spatialBlend = 1.0f; // Set to 3D sound
        audioSource.Play();
    }
}