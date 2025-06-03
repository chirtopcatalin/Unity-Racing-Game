using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.InputSystem;

public class RacingAgent : Agent
{
    private TrackCheckpoints trackCheckpoints;
    private List<GameObject> orderedGoals;
    private CarControllerAgent carControllerAgent;
    private Rigidbody rb;
    private RaceManager raceManager;

    private int nextCheckpoint = 0;
    private int lapCount = 0;
    private bool isTraining = false;

    [Header("Input Actions")]
    public InputActionReference acceleration;
    public InputActionReference steering;

    [Header("Raycast Settings")]
    [SerializeField] private int numRays = 32;
    [SerializeField] private float rayMaxDistance = 25f;
    [SerializeField] private float rayHeightOffset = 0.5f;

    [Header("Stuck Detection")]
    public float stuckTimeout = 6f;
    public float movementThreshold = 0.5f;
    public float minSpeedThreshold = 0.5f;

    [Header("Reward Settings")]
    public float checkpointReward = 0.5f;
    public float lapCompletionReward = 2f;
    public float wallCollisionPenalty = -6.5f;
    public float carCollisionPenalty = -1.5f;
    public float wrongCheckpointPenalty = -1f;
    public float stuckPenalty = -6f;
    public float speedRewardMultiplier = 0.0022f;
    public float proximityRewardMultiplier = 0.02f;
    public float collisionStayPenalty = -0.01f;

    [Header("Unstuck Settings")]
    public float carSpawnCheckRadius = 2.5f;
    public float carHeightOffset = 0.5f;

    [Header("Game Mode Settings")]
    public bool despawnOnRaceComplete = true;

    private Vector3 _lastPosition;
    private float _stuckTimer;
    private float _lowSpeedTimer;
    private float previousCheckpointDistance;
    private float _collisionCooldown;
    private bool _isColliding;
    private bool _hasFinishedRace = false;

    // Performance tracking
    private float _episodeStartTime;
    private float _totalDistance;
    private Vector3 _previousFramePosition;

    public override void Initialize()
    {
        isTraining = Academy.Instance.IsCommunicatorOn;

        _lastPosition = transform.position;
        _previousFramePosition = transform.position;
        _stuckTimer = 0f;
        _lowSpeedTimer = 0f;
        _collisionCooldown = 0f;
        _isColliding = false;
        _hasFinishedRace = false;

        carControllerAgent = GetComponent<CarControllerAgent>();
        trackCheckpoints = transform.parent.GetComponent<TrackCheckpoints>();
        orderedGoals = trackCheckpoints.GetCheckpoints();
        rb = GetComponent<Rigidbody>();
        raceManager = transform.parent.GetComponent<RaceManager>();
        raceManager.RegisterAgent(this);

        if (orderedGoals != null && orderedGoals.Count > 0)
        {
            int initialCheckpointIndex = Mathf.Clamp(nextCheckpoint, 0, orderedGoals.Count - 1);
            if (orderedGoals.Count > initialCheckpointIndex && initialCheckpointIndex >= 0)
            {
                previousCheckpointDistance = Vector3.Distance(transform.position, orderedGoals[initialCheckpointIndex].transform.position);
            }
        }
    }

    private void FixedUpdate()
    {
        if (!isTraining && _hasFinishedRace) return;

        float distanceMoved = Vector3.Distance(transform.position, _lastPosition);
        float currentSpeed = rb.linearVelocity.magnitude;

        if (distanceMoved < movementThreshold * Time.fixedDeltaTime)
        {
            _stuckTimer += Time.fixedDeltaTime;
        }
        else
        {
            _stuckTimer = 0f;
        }

        if (currentSpeed < minSpeedThreshold)
        {
            _lowSpeedTimer += Time.fixedDeltaTime;
        }
        else
        {
            _lowSpeedTimer = 0f;
        }

        if (_stuckTimer >= stuckTimeout || _lowSpeedTimer >= stuckTimeout)
        {
            if (isTraining)
            {
                UnstuckAgent();
            }
            else
            {
                DespawnAgent();
            }
        }

        _lastPosition = transform.position;

        if (_collisionCooldown > 0)
        {
            _collisionCooldown -= Time.fixedDeltaTime;
        }

        _totalDistance += Vector3.Distance(transform.position, _previousFramePosition);
        _previousFramePosition = transform.position;
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log($"{name}: New episode starting.");
        _lastPosition = transform.position;
        _previousFramePosition = transform.position;
        _stuckTimer = 0f;
        _lowSpeedTimer = 0f;
        _collisionCooldown = 0f;
        _isColliding = false;
        _hasFinishedRace = false;
        _totalDistance = 0f;
        _episodeStartTime = Time.time;
        orderedGoals = trackCheckpoints.GetCheckpoints();
        lapCount = 0;
        nextCheckpoint = 0;

        if (orderedGoals != null && orderedGoals.Count > 0)
        {
            int initialCheckpointIndex = Mathf.Clamp(nextCheckpoint, 0, orderedGoals.Count - 1);
            if (orderedGoals.Count > initialCheckpointIndex && initialCheckpointIndex >= 0)
            {
                previousCheckpointDistance = Vector3.Distance(transform.position, orderedGoals[initialCheckpointIndex].transform.position);
            }
        }
    }

    private void UnstuckAgent()
    {
        Debug.LogWarning($"{name}: Agent got stuck, attempting to unstuck.");
        AddReward(stuckPenalty);

        _stuckTimer = 0f;
        _lowSpeedTimer = 0f;

        if (orderedGoals == null || orderedGoals.Count < 2)
        {
            Debug.LogError($"{name}: Not enough checkpoints ({orderedGoals?.Count ?? 0})");
            if (isTraining)
            {
                EndEpisode();
            }
            else
            {
                DespawnAgent();
            }
            return;
        }

        int lastCheckpointIndex = (nextCheckpoint == 0) ? orderedGoals.Count - 1 : nextCheckpoint - 1;

        if (lastCheckpointIndex < 0 || lastCheckpointIndex >= orderedGoals.Count || nextCheckpoint < 0 || nextCheckpoint >= orderedGoals.Count)
        {
            Debug.LogError($"{name}: Invalid checkpoint indices for unstucking. Last: {lastCheckpointIndex}, Next: {nextCheckpoint}. Checkpoint count: {orderedGoals.Count}. Ending episode.");
            if (isTraining)
            {
                EndEpisode();
            }
            else
            {
                DespawnAgent();
            }
            return;
        }

        Vector3 lastCheckpointPos = orderedGoals[lastCheckpointIndex].transform.position;
        Vector3 nextCheckpointPos = orderedGoals[nextCheckpoint].transform.position;

        Vector3 segmentDirection = (Vector3.Distance(nextCheckpointPos, lastCheckpointPos) > 0.1f) ? (nextCheckpointPos - lastCheckpointPos).normalized : transform.forward;
        Vector3 midpoint = (lastCheckpointPos + nextCheckpointPos) / 2.0f;

        Vector3 baseSpawnPosition = midpoint;
        baseSpawnPosition.y = ((lastCheckpointPos.y + nextCheckpointPos.y) / 2.0f) + carHeightOffset;

        Vector3 finalSpawnPosition = baseSpawnPosition;
        bool positionFound = false;
        int maxSpawnAttempts = 5;
        float spawnOffsetDistance = carSpawnCheckRadius * 1.5f;

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector3 currentTestPosition = baseSpawnPosition;
            if (attempt == 1) currentTestPosition += segmentDirection * spawnOffsetDistance;
            else if (attempt == 2) currentTestPosition -= segmentDirection * spawnOffsetDistance;
            else if (attempt == 3) currentTestPosition += Vector3.Cross(segmentDirection, Vector3.up).normalized * spawnOffsetDistance;
            else if (attempt == 4) currentTestPosition -= Vector3.Cross(segmentDirection, Vector3.up).normalized * spawnOffsetDistance;

            Collider[] hitColliders = Physics.OverlapSphere(currentTestPosition, carSpawnCheckRadius);
            bool occupied = false;
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.gameObject.CompareTag("car") && hitCollider.gameObject != this.gameObject)
                {
                    occupied = true;
                    Debug.Log($"{name}: Unstuck spawn attempt {attempt} at {currentTestPosition} is occupied by {hitCollider.name}.");
                    break;
                }
            }

            if (!occupied)
            {
                finalSpawnPosition = currentTestPosition;
                positionFound = true;
                Debug.Log($"{name}: Found clear unstuck spawn position at attempt {attempt}: {finalSpawnPosition}");
                break;
            }
        }

        if (!positionFound)
        {
            Debug.LogWarning("Could not find a spawn position");
            finalSpawnPosition = baseSpawnPosition;
        }

        transform.position = finalSpawnPosition;

        if (Vector3.Distance(nextCheckpointPos, lastCheckpointPos) > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(segmentDirection);
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (orderedGoals.Count > 0 && nextCheckpoint < orderedGoals.Count)
        {
            previousCheckpointDistance = Vector3.Distance(transform.position, orderedGoals[nextCheckpoint].transform.position);
        }
        else if (orderedGoals.Count > 0)
        {
            previousCheckpointDistance = Vector3.Distance(transform.position, orderedGoals[0].transform.position);
        }

        Debug.Log($"{name}: Agent unstuck and repositioned to {transform.position}.");
    }

    private void DespawnAgent()
    {
        Debug.Log("Despawning agent");
        _hasFinishedRace = true;

        if (carControllerAgent != null)
        {
            carControllerAgent.enabled = false;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        gameObject.SetActive(false);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("wall"))
        {
            if (isTraining) AddReward(wallCollisionPenalty);
            _collisionCooldown = 1f;
            _isColliding = true;
        }
        else if (collision.collider.CompareTag("car"))
        {
            if (isTraining) AddReward(carCollisionPenalty);
            _collisionCooldown = 0.5f;
            _isColliding = true;
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (isTraining && _collisionCooldown <= 0 && (collision.collider.CompareTag("car") || collision.collider.CompareTag("wall")))
        {
            AddReward(collisionStayPenalty * Time.fixedDeltaTime);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag("car") || collision.collider.CompareTag("wall"))
        {
            _isColliding = false;
        }
    }

    public void ResetAgent(Vector3 startingPosition, Quaternion startingRotation)
    {
        if (isTraining)
        {
            EndEpisode();
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = Vector3.zero;
            transform.position = startingPosition;
            transform.rotation = startingRotation * Quaternion.Euler(0, 180f, 0);
        }
        else
        {
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = Vector3.zero;
            transform.position = startingPosition;
            transform.rotation = startingRotation * Quaternion.Euler(0, 180f, 0);
            _hasFinishedRace = false;

            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
                rb.isKinematic = false;
                if (carControllerAgent != null)
                {
                    carControllerAgent.enabled = true;
                }
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!isTraining && _hasFinishedRace) return;

        carControllerAgent.GetInput(
            actions.DiscreteActions[0],
            actions.DiscreteActions[1]
        );

        if (isTraining)
        {
            Vector3 velLocal = transform.InverseTransformDirection(rb.linearVelocity);
            float forwardVel = Mathf.Max(0, velLocal.z);
            AddReward(speedRewardMultiplier * forwardVel * Time.fixedDeltaTime);

            if (orderedGoals != null && orderedGoals.Count > 0 && nextCheckpoint < orderedGoals.Count)
            {
                float currentDistance = Vector3.Distance(transform.position, orderedGoals[nextCheckpoint].transform.position);
                float delta = previousCheckpointDistance - currentDistance;

                if (delta > 0)
                {
                    AddReward(delta * proximityRewardMultiplier);
                }
                previousCheckpointDistance = currentDistance;
            }

            if (_isColliding && _collisionCooldown <= 0)
            {
                AddReward(-0.01f * Time.fixedDeltaTime);
            }

            float angularVelPenalty = rb.angularVelocity.magnitude * -0.001f;
            AddReward(angularVelPenalty * Time.fixedDeltaTime);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var d = actionsOut.DiscreteActions;
        float accel = acceleration.action.ReadValue<float>();
        d[0] = accel > 0 ? 2 : accel < 0 ? 0 : 1;
        float steer = steering.action.ReadValue<float>();
        d[1] = steer > 0 ? 2 : steer < 0 ? 0 : 1;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (orderedGoals == null || orderedGoals.Count == 0)
        {
            int expectedObservationCount = 15 + (numRays * 3);
            for (int i = 0; i < expectedObservationCount; ++i)
                sensor.AddObservation(0f);
            Debug.LogWarning("orderedGoals is null or empty");
            return;
        }

        int checkpointIndexToObserve = nextCheckpoint;
        if (checkpointIndexToObserve >= orderedGoals.Count)
            checkpointIndexToObserve = 0;

        Vector3 toCheckpointWorld = orderedGoals[checkpointIndexToObserve].transform.position - transform.position;
        Vector3 toCheckpointLocal = transform.InverseTransformDirection(toCheckpointWorld);
        sensor.AddObservation(toCheckpointLocal.normalized);

        int nextNextCheckpointIndex = (checkpointIndexToObserve + 1) % orderedGoals.Count;
        Vector3 toNextNextCheckpointWorld = orderedGoals[nextNextCheckpointIndex].transform.position - transform.position;
        Vector3 toNextNextCheckpointLocal = transform.InverseTransformDirection(toNextNextCheckpointWorld);
        sensor.AddObservation(toNextNextCheckpointLocal.normalized);

        float alignment = Vector3.Dot(transform.forward, toCheckpointWorld.normalized);
        sensor.AddObservation(alignment);

        sensor.AddObservation(rb.linearVelocity);
        sensor.AddObservation(rb.angularVelocity.y);

        float distanceToCheckpoint = toCheckpointWorld.magnitude;
        sensor.AddObservation(Mathf.Clamp01(distanceToCheckpoint / rayMaxDistance));

        sensor.AddObservation((float)lapCount / 3f);
        sensor.AddObservation((float)nextCheckpoint / orderedGoals.Count);

        var origin = transform.position + Vector3.up * rayHeightOffset;
        float angleStep = 360f / numRays;

        for (int i = 0; i < numRays; i++)
        {
            float angle = i * angleStep;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * transform.forward;

            float obsDistToGoal = rayMaxDistance;
            float obsDistToWall = rayMaxDistance;
            float obsDistToCar = rayMaxDistance;

            RaycastHit hitInfo;
            if (Physics.Raycast(origin, dir, out hitInfo, rayMaxDistance))
            {
                if (hitInfo.collider.CompareTag("wall"))
                {
                    obsDistToWall = hitInfo.distance;
                }
                else if (hitInfo.collider.CompareTag("goal"))
                {
                    obsDistToGoal = hitInfo.distance;
                }
                else if (hitInfo.collider.CompareTag("car") && hitInfo.collider.gameObject != gameObject)
                {
                    obsDistToCar = hitInfo.distance;
                }
            }
            sensor.AddObservation(obsDistToGoal / rayMaxDistance);
            sensor.AddObservation(obsDistToWall / rayMaxDistance);
            sensor.AddObservation(obsDistToCar / rayMaxDistance);
        }

        sensor.AddObservation(_isColliding ? 1f : 0f);
        sensor.AddObservation(_collisionCooldown > 0 ? 1f : 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("goal"))
        {
            if (orderedGoals == null || orderedGoals.Count == 0) return;

            var hitGoal = other.gameObject;
            if (nextCheckpoint < orderedGoals.Count && hitGoal == orderedGoals[nextCheckpoint])
            {
                if (isTraining) AddReward(checkpointReward);
                nextCheckpoint++;

                if (nextCheckpoint >= orderedGoals.Count)
                {
                    lapCount++;
                    nextCheckpoint = 0;
                    if (isTraining) AddReward(lapCompletionReward);

                    if (lapCount >= 3)
                    {
                        if (isTraining)
                        {
                            float timeBonus = Mathf.Max(0, 50f - (Time.time - _episodeStartTime) * 0.1f);
                            AddReward(timeBonus);
                            raceManager.ResetAllAgents();
                        }
                        else
                        {
                            Debug.Log("Race completed(game mode)");
                            if (despawnOnRaceComplete)
                            {
                                DespawnAgent();
                            }
                        }
                    }
                }

                if (orderedGoals.Count > 0)
                {
                    int currentNextCheckpointIndex = Mathf.Clamp(nextCheckpoint, 0, orderedGoals.Count - 1);
                    if (orderedGoals.Count > currentNextCheckpointIndex)
                    {
                        previousCheckpointDistance = Vector3.Distance(transform.position, orderedGoals[currentNextCheckpointIndex].transform.position);
                    }
                }

                int overtaken = raceManager.UpdateAgentProgress(this, lapCount, nextCheckpoint);
                if (isTraining && overtaken > 0)
                {
                    float overtakeBonus = overtaken * 2f;
                    AddReward(overtakeBonus);
                }
            }
            else
            {
                bool isAValidCheckpoint = false;
                for (int i = 0; i < orderedGoals.Count; ++i)
                {
                    if (hitGoal == orderedGoals[i])
                    {
                        isAValidCheckpoint = true;
                        break;
                    }
                }
                if (isAValidCheckpoint && nextCheckpoint < orderedGoals.Count)
                {
                    Debug.LogWarning($"Hit wrong checkpoint!");
                    if (isTraining) AddReward(wrongCheckpointPenalty);
                }
            }
        }
    }

    public bool HasFinishedRace()
    {
        return _hasFinishedRace;
    }

    public int GetCurrentLap()
    {
        return lapCount;
    }

    public int GetCurrentCheckpoint()
    {
        return nextCheckpoint;
    }
}