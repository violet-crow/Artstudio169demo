using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class waypointNPC : MonoBehaviour
{
    [Header("Waypoints")]
    public Transform[] waypoints;
    public bool randomOrder = true;
    public bool avoidRepeats = true;
    [Header("Movement")]
    public float speed = 0.5f;
    public float arrivalThreshold = 0.2f;   // Extra buffer beyond stoppingDistance
    public float minWaitAtWaypoint = 1.0f;
    public float maxWaitAtWaypoint = 3.0f;

    [Header("Animation (optional)")]
    public Animator animator;
    public string speedParameter = "speed"; // Animator float that drives walk/idle

    [Header("Robustness")]
    public float stuckTimeout = 4.0f;       // Repath if barely moving for this long

    private NavMeshAgent agent;
    private int currentIndex = -1;
    private bool waiting;
    private Vector3 lastPos;
    private float lastMoveTime;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!agent)
        {
            Debug.LogWarning($"{name}: RoamingNPC requires a NavMeshAgent on the same GameObject.");
            enabled = false;
            return;
        }

        // Configure the agent
        agent.speed = speed;
        agent.autoBraking = true;       // Slow down near destinations
        agent.stoppingDistance = 0f;
        agent.updateRotation = false;   // Let your sprite billboard handle facing

        // Start moving
        lastPos = transform.position;
        PickNextDestination();
    }

    void Update()
    {
        // Drive Animator with agent velocity if provided
        if (animator)
            animator.SetFloat(speedParameter, agent.velocity.magnitude);

        if (!waiting)
        {
            // Arrival check
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + arrivalThreshold)
                StartCoroutine(WaitThenGo());

            // Stuck detection
            float movedSqr = (transform.position - lastPos).sqrMagnitude;
            if (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + arrivalThreshold)
            {
                if (movedSqr < 0.0001f)
                {
                    if (Time.time - lastMoveTime > stuckTimeout)
                    {
                        RepathCurrent();
                        lastMoveTime = Time.time;
                    }
                }
                else
                {
                    lastMoveTime = Time.time;
                }
            }
        }

        lastPos = transform.position;
    }

    IEnumerator WaitThenGo()
    {
        waiting = true;
        float wait = Random.Range(minWaitAtWaypoint, maxWaitAtWaypoint);
        if (animator) animator.SetFloat(speedParameter, 0f);
        yield return new WaitForSeconds(wait);
        waiting = false;
        PickNextDestination();
    }

    void PickNextDestination()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        int nextIndex = currentIndex;

        if (randomOrder)
        {
            if (waypoints.Length == 1)
                nextIndex = 0;
            else
            {
                do
                {
                    nextIndex = Random.Range(0, waypoints.Length);
                }
                while (avoidRepeats && waypoints.Length > 1 && nextIndex == currentIndex);
            }
        }
        else
        {
            nextIndex = (currentIndex + 1) % waypoints.Length;
        }

        currentIndex = nextIndex;
        agent.SetDestination(waypoints[currentIndex].position);
    }

    void RepathCurrent()
    {
        if (currentIndex < 0 || waypoints == null || waypoints.Length == 0) return;
        agent.SetDestination(waypoints[currentIndex].position);
    }

    // Public helpers if you want to control roaming externally
    public void StopRoaming()
    {
        agent.ResetPath();
        waiting = false;
        if (animator) animator.SetFloat(speedParameter, 0f);
    }

    public void ResumeRoaming()
    {
        waiting = false;
        PickNextDestination();
    }

    public void SetWaypoints(Transform[] newWaypoints)
    {
        waypoints = newWaypoints;
        currentIndex = -1;
        PickNextDestination();
    }
}