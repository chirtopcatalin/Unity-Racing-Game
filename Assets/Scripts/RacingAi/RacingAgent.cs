using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.InputSystem;

public class RacingAgent : Agent
{
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

    private Vector3 lastPosition;
    private float stuckTimer;
    private float lowSpeedTimer;
    private float previousCheckpointDistance;
    private float collisionCooldown;
    private bool isColliding;
    private bool hasFinishedRace = false;

    private float episodeStartTime;
    private float totalDistance;
    private Vector3 previousFramePosition;

    public override void Initialize()
    {
        isTraining = Academy.Instance.IsCommunicatorOn;

        carControllerAgent = GetComponent<CarControllerAgent>();
        rb = GetComponent<Rigidbody>();

        if (transform.parent != null)
        {
            raceManager = transform.parent.GetComponent<RaceManager>();
        }

        if (raceManager != null)
        {
            raceManager.RegisterAgent(this);
            orderedGoals = raceManager.GetCheckpoints();
        }
        else
        {
            Debug.LogError("raceManager not found");
        }

        lastPosition = transform.position;
        previousFramePosition = transform.position;
        stuckTimer = 0f;
        lowSpeedTimer = 0f;
        collisionCooldown = 0f;
        isColliding = false;
        hasFinishedRace = false;

        if (orderedGoals != null && orderedGoals.Count > 0 && nextCheckpoint < orderedGoals.Count)
        {
            previousCheckpointDistance = Vector3.Distance(transform.position, orderedGoals[nextCheckpoint].transform.position);
        }
    }

    private void FixedUpdate()
    {
        if (raceManager != null && raceManager.IsCountdownActive())
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            lastPosition = transform.position;
            lowSpeedTimer = 0f;
            stuckTimer = 0f;
            return;
        }

        if (!isTraining && hasFinishedRace) return;

        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        float currentSpeed = rb.linearVelocity.magnitude;

        if (distanceMoved < movementThreshold * Time.fixedDeltaTime)
        {
            stuckTimer += Time.fixedDeltaTime;
        }
        else
        {
            stuckTimer = 0f;
        }

        if (currentSpeed < minSpeedThreshold)
        {
            lowSpeedTimer += Time.fixedDeltaTime;
        }
        else
        {
            lowSpeedTimer = 0f;
        }

        if (stuckTimer >= stuckTimeout || lowSpeedTimer >= stuckTimeout)
        {
            Debug.LogWarning("agent stuck");
            UnstuckAgent();
        }

        lastPosition = transform.position;

        if (collisionCooldown > 0)
        {
            collisionCooldown -= Time.fixedDeltaTime;
        }

        totalDistance += Vector3.Distance(transform.position, previousFramePosition);
        previousFramePosition = transform.position;
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log("new episode");
        lastPosition = transform.position;
        previousFramePosition = transform.position;
        stuckTimer = 0f;
        lowSpeedTimer = 0f;
        collisionCooldown = 0f;
        isColliding = false;
        hasFinishedRace = false;
        totalDistance = 0f;
        episodeStartTime = Time.time;

        lapCount = 0;
        nextCheckpoint = 0;

        orderedGoals = raceManager.GetCheckpoints();
        if (orderedGoals != null && orderedGoals.Count > 0)
        {
            previousCheckpointDistance = Vector3.Distance(transform.position, orderedGoals[nextCheckpoint].transform.position);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if ((raceManager != null && raceManager.IsCountdownActive()) || (!isTraining && hasFinishedRace))
        {
            if (carControllerAgent != null)
            {
                carControllerAgent.GetInput(1, 1);
            }
            return;
        }

        if (carControllerAgent != null)
        {
            carControllerAgent.GetInput(
                actions.DiscreteActions[0],
                actions.DiscreteActions[1]
            );
        }
        else
        {
            return;
        }

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
            if (isColliding && collisionCooldown <= 0)
            {
                AddReward(collisionStayPenalty * Time.fixedDeltaTime);
            }

            float angularVelPenalty = rb.angularVelocity.magnitude * -0.001f;
            AddReward(angularVelPenalty * Time.fixedDeltaTime);
        }
    }

    public void ResetAgent(Vector3 startingPosition, Quaternion startingRotation)
    {
        transform.position = startingPosition;
        transform.rotation = startingRotation;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        hasFinishedRace = false;
        lapCount = 0;
        nextCheckpoint = 0;

        if (orderedGoals != null && orderedGoals.Count > 0)
        {
            previousCheckpointDistance = Vector3.Distance(transform.position, orderedGoals[0].transform.position);
        }

        stuckTimer = 0f;
        lowSpeedTimer = 0f;
        collisionCooldown = 0f;
        isColliding = false;

        if (isTraining)
        {
            EndEpisode();
        }
        else
        {
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }
            if (rb.isKinematic)
            {
                rb.isKinematic = false;
            }
            if (carControllerAgent != null && !carControllerAgent.enabled)
            {
                carControllerAgent.enabled = true;
            }
        }
    }
    private void UnstuckAgent()
    {
        Debug.LogWarning("agent stuck");
        AddReward(stuckPenalty);

        stuckTimer = 0f;
        lowSpeedTimer = 0f;

        if (orderedGoals == null || orderedGoals.Count < 2)
        {
            Debug.LogError("not enough checkpoints");
            if (isTraining)
            {
                EndEpisode();
            }
            return;
        }

        int lastCheckpointIndex = (nextCheckpoint == 0) ? orderedGoals.Count - 1 : nextCheckpoint - 1;

        if (lastCheckpointIndex < 0 || lastCheckpointIndex >= orderedGoals.Count || nextCheckpoint < 0 || nextCheckpoint >= orderedGoals.Count)
        {
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
                    break;
                }
            }

            if (!occupied)
            {
                finalSpawnPosition = currentTestPosition;
                positionFound = true;
                break;
            }
        }

        if (!positionFound)
        {
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

        Debug.Log($"agent unstuck and repositioned to {transform.position}.");
    }

    private void DespawnAgent()
    {
        Debug.Log("despawned agent");
        hasFinishedRace = true;

        if (carControllerAgent != null)
        {
            carControllerAgent.enabled = false;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        gameObject.SetActive(false);
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

        sensor.AddObservation(isColliding ? 1f : 0f);
        sensor.AddObservation(collisionCooldown > 0 ? 1f : 0f);
    }


    private void OnTriggerEnter(Collider other)
    {
        if (raceManager != null && raceManager.IsCountdownActive()) return;
        if (!isTraining && hasFinishedRace) return;

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
                        Debug.Log($"{name} completed the race!");
                        hasFinishedRace = true;
                        if (isTraining)
                        {
                            float timeBonus = Mathf.Max(0, 50f - (Time.time - episodeStartTime) * 0.1f);
                            AddReward(timeBonus);
                            EndEpisode();
                        }
                        else
                        {
                            if (despawnOnRaceComplete)
                            {
                                DespawnAgent();
                            }
                        }
                        return;
                    }
                }

                if (orderedGoals.Count > 0 && nextCheckpoint < orderedGoals.Count)
                {
                    previousCheckpointDistance = Vector3.Distance(transform.position, orderedGoals[nextCheckpoint].transform.position);
                }


                if (raceManager != null)
                {
                    int overtaken = raceManager.UpdateAgentProgress(this, lapCount, nextCheckpoint);
                    if (isTraining && overtaken > 0)
                    {
                        float overtakeBonus = overtaken * 2f;
                        AddReward(overtakeBonus);
                    }
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
                    Debug.LogWarning("hit wrong checkpoint!");
                    if (isTraining) AddReward(wrongCheckpointPenalty);
                }
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (raceManager != null && raceManager.IsCountdownActive()) return;
        if (!isTraining && hasFinishedRace) return;


        if (collision.collider.CompareTag("wall"))
        {
            if (isTraining) AddReward(wallCollisionPenalty);
            collisionCooldown = 1f;
            isColliding = true;
        }
        else if (collision.collider.CompareTag("car"))
        {
            if (isTraining) AddReward(carCollisionPenalty);
            collisionCooldown = 0.5f;
            isColliding = true;
        }
    }
    public bool HasFinishedRace()
    {
        return hasFinishedRace;
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