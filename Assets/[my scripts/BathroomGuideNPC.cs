using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class BathroomGuideNPC : MonoBehaviour
{
    public MainRoomMoodController mainRoomMood;
    [Header("References")]
    public Transform player;
    public Transform bathroomWaypoint;
    [Header("Timing")]
    public float initialDelay = 180f;  // 3 minutes

    [Header("Movement")]
    public float speed = 1.8f;
    public float approachDistance = 1.5f;
    public float repathInterval = 0.25f;
    public float arrivalThreshold = 0.2f;

    [Header("NPCDialogue")]
    public NPCDialogue dialogueComponent;
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip approachLine;
    public AudioClip vomitingSfx;
    public bool loopVomiting = true;

    [Header("Visuals")]
    public SpriteRenderer spriteRenderer;
    public Sprite normalSprite;
    public Sprite vomitingSprite;

    [Header("Animator (optional)")]
    public Animator animator;
    public string speedParameter = "speed";
    public string vomitingTrigger = "Vomit";

    public enum PlayerArrivalMode { ByDistance, ByTrigger }
    [Header("Player arrival gating")]
    public PlayerArrivalMode arrivalMode = PlayerArrivalMode.ByDistance;

    // Distance mode
    public float bathroomArrivalRadius = 2.5f;

    // Trigger mode
    public PlayerPresenceTrigger bathroomPresenceTrigger;

    private NavMeshAgent agent;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!agent)
        {
            Debug.LogError($"{name}: BathroomGuideNPC requires a NavMeshAgent.");
            enabled = false;
            return;
        }

        agent.speed = speed;
        agent.autoBraking = true;
        agent.stoppingDistance = 0f;
        agent.updateRotation = false;

        if (spriteRenderer && normalSprite)
            spriteRenderer.sprite = normalSprite;
    }

    void OnEnable()
    {
        StartCoroutine(RunSequence());
    }

    IEnumerator RunSequence()
    {
        // 1) Wait 3 minutes from start
        yield return new WaitForSeconds(initialDelay);

        // 2) Approach the player
        yield return ApproachPlayer();

        // 3) Play approach line
        if (audioSource && approachLine)
        {
            audioSource.loop = false;
            audioSource.clip = approachLine;
            audioSource.Play();
            yield return new WaitWhile(() => audioSource.isPlaying);
        }

        // 4) Go to bathroom waypoint
        if (bathroomWaypoint)
            yield return GoTo(bathroomWaypoint.position);

        // Stop moving and switch to vomiting visuals immediately
        agent.isStopped = true;
        agent.ResetPath();
        if (animator) animator.SetFloat(speedParameter, 0f);
        EnterVomitingVisuals();

        // 5) Wait until the player is in the bathroom with them
        yield return new WaitUntil(() => IsPlayerInBathroom());

        // Trigger main-room changes now
        if (mainRoomMood) mainRoomMood.Activate();

        // 6) Play vomiting SFX (once or loop)
        PlayVomitingSfx();

        // 7) Remain there for the rest of runtime
        while (true) yield return null;
    }

    IEnumerator ApproachPlayer()
    {
        if (!player) yield break;

        agent.isStopped = false;
        float nextRepath = 0f;

        while (true)
        {
            if (!player) yield break;

            if (Time.time >= nextRepath)
            {
                agent.SetDestination(player.position);
                nextRepath = Time.time + repathInterval;
            }

            if (animator) animator.SetFloat(speedParameter, agent.velocity.magnitude);

            if (!agent.pathPending && Vector3.Distance(transform.position, player.position) <= approachDistance)
                break;

            yield return null;
        }

        FaceTargetFlat(player.position);
    }

    IEnumerator GoTo(Vector3 worldPos)
    {
        agent.isStopped = false;
        agent.SetDestination(worldPos);

        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + arrivalThreshold)
        {
            if (animator) animator.SetFloat(speedParameter, agent.velocity.magnitude);
            yield return null;
        }
    }

    void EnterVomitingVisuals()
    {
        if (animator && !string.IsNullOrEmpty(vomitingTrigger))
        {
            animator.SetTrigger(vomitingTrigger);
        }
        else if (spriteRenderer && vomitingSprite)
        {
            spriteRenderer.sprite = vomitingSprite;
        }
    }

    void PlayVomitingSfx()
    {
        if (audioSource && vomitingSfx)
        {
            audioSource.loop = loopVomiting;
            audioSource.clip = vomitingSfx;
            audioSource.Play();
        }
    }

    bool IsPlayerInBathroom()
    {
        if (!player) return false;

        if (arrivalMode == PlayerArrivalMode.ByTrigger)
        {
            return bathroomPresenceTrigger && bathroomPresenceTrigger.IsPlayerInside;
        }
        else
        {
            if (!bathroomWaypoint) return false;
            Vector3 a = bathroomWaypoint.position; a.y = 0f;
            Vector3 b = player.position; b.y = 0f;
            return Vector3.Distance(a, b) <= bathroomArrivalRadius;
        }
    }

    void FaceTargetFlat(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}