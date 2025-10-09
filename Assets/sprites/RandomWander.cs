using UnityEngine;

public class RandomWander : MonoBehaviour
{
    [Header("Wander Area")]
    public Transform wanderCenter;               // optional moving center; if null uses start position
    public float wanderRadius = 8f;
    public float minTargetDistance = 1.5f;

    [Header("Movement")]
    public float maxSpeed = 3f;
    public float acceleration = 4f;
    public float brakingDistance = 1.5f;
    public float stopThreshold = 0.2f;

    [Header("Pausing")]
    public float pauseMin = 1f;
    public float pauseMax = 3f;
    public float postPauseWait = 0.25f;

    [Header("Ground Sampling (optional)")]
    public bool sampleGround = false;            // default false -> safer
    public LayerMask groundMask = ~0;            // change to the layer(s) of your ground
    public float groundRayHeight = 50f;

    [Header("Misc")]
    public int maxTargetAttempts = 10;

    Vector3 _centerPosition;
    Vector3 _targetPosition;
    Vector3 _velocity;
    float _startY;

    enum State { Moving, Paused, Waiting }
    State _state = State.Waiting;
    float _stateTimer = 0f;

    void Start()
    {
        _startY = transform.position.y;
        _centerPosition = (wanderCenter != null) ? wanderCenter.position : transform.position;

        // Pick initial target but be conservative: if pick fails, remain waiting briefly
        PickNewTarget();
    }

    void Update()
    {
        if (wanderCenter != null)
            _centerPosition = wanderCenter.position;

        switch (_state)
        {
            case State.Moving:
                DoMoveUpdate();
                break;
            case State.Paused:
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
                {
                    _state = State.Waiting;
                    _stateTimer = postPauseWait;
                }
                break;
            case State.Waiting:
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
                    PickNewTarget();
                break;
        }
    }

    void DoMoveUpdate()
    {
        Vector3 current = transform.position;
        Vector3 toTarget = new Vector3(_targetPosition.x - current.x, 0f, _targetPosition.z - current.z);
        float dist = toTarget.magnitude;

        // arrival behavior
        float desiredSpeed = maxSpeed;
        if (dist <= brakingDistance && brakingDistance > 0f)
            desiredSpeed = Mathf.Lerp(0f, maxSpeed, dist / brakingDistance);

        Vector3 desiredVelocity = (dist > 0.0001f) ? (toTarget.normalized * desiredSpeed) : Vector3.zero;

        // smooth velocity change
        _velocity = Vector3.MoveTowards(_velocity, desiredVelocity, acceleration * Time.deltaTime);

        // compute next horizontal position
        Vector3 nextPosXZ = new Vector3(current.x, 0f, current.z) + new Vector3(_velocity.x, 0f, _velocity.z) * Time.deltaTime;
        float newY = current.y;

        if (sampleGround)
        {
            float sampleY;
            if (SampleGroundHeight(nextPosXZ.x, nextPosXZ.z, out sampleY))
            {
                // optionally smooth vertical changes for nicer motion:
                newY = Mathf.Lerp(current.y, sampleY, Mathf.Clamp01(Time.deltaTime * 6f));
            }
            // if sampling failed, keep current Y (do not teleport to 0)
        }

        Vector3 nextPos = new Vector3(nextPosXZ.x, newY, nextPosXZ.z);
        transform.position = nextPos;

        if (dist <= stopThreshold)
        {
            _velocity = Vector3.zero;
            _state = State.Paused;
            _stateTimer = Random.Range(pauseMin, pauseMax);
        }
    }

    void PickNewTarget()
    {
        Vector3 horizontalCurrent = new Vector3(transform.position.x, 0f, transform.position.z);

        for (int i = 0; i < maxTargetAttempts; i++)
        {
            Vector2 r = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = new Vector3(_centerPosition.x + r.x, _startY, _centerPosition.z + r.y);

            float candidateDist = Vector3.Distance(horizontalCurrent, new Vector3(candidate.x, 0f, candidate.z));
            if (candidateDist < minTargetDistance)
                continue;

            if (sampleGround)
            {
                float sampleY;
                if (SampleGroundHeight(candidate.x, candidate.z, out sampleY))
                {
                    candidate.y = sampleY;
                }
                else
                {
                    // If sampling fails, do NOT reject the point repeatedly - fall back to starting Y
                    candidate.y = _startY;
                }
            }
            else
            {
                candidate.y = transform.position.y; // keep current Y when not sampling
            }

            _targetPosition = candidate;
            _state = State.Moving;
            return;
        }

        // failed to find candidate after attempts: wait a bit, then try again
        _state = State.Waiting;
        _stateTimer = 1f;
    }

    bool SampleGroundHeight(float x, float z, out float height)
    {
        float originY = Mathf.Max(transform.position.y, _startY) + groundRayHeight;
        Vector3 origin = new Vector3(x, originY, z);
        Ray ray = new Ray(origin, Vector3.down);
        RaycastHit hit;

        // cast down a reasonable distance
        float castDist = groundRayHeight + 10f;
        if (Physics.Raycast(ray, out hit, castDist, groundMask, QueryTriggerInteraction.Ignore))
        {
            height = hit.point.y;
            return true;
        }

        // try an extreme fallback cast if nothing found (rare)
        Vector3 highOrigin = new Vector3(x, 1000f, z);
        if (Physics.Raycast(highOrigin, Vector3.down, out hit, Mathf.Infinity, groundMask, QueryTriggerInteraction.Ignore))
        {
            height = hit.point.y;
            return true;
        }

        height = 0f;
        Debug.LogWarning("[RandomWander] SampleGroundHeight failed at (" + x + "," + z + "). Check groundMask and that colliders exist on your ground.", this);
        return false;
    }

    // Optional: draw center and target to help debugging
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = (wanderCenter != null) ? wanderCenter.position : transform.position;
        Gizmos.DrawWireSphere(center, wanderRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(new Vector3(_targetPosition.x, transform.position.y, _targetPosition.z), 0.12f);
    }
}