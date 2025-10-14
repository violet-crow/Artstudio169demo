using UnityEngine;

public class PlayerPresenceTrigger : MonoBehaviour
{
    public string playerTag = "Player";
    public bool IsPlayerInside { get; private set; }
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
            IsPlayerInside = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
            IsPlayerInside = false;
    }
}