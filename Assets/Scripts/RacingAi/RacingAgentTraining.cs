using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Linq;

public class RacingAgentTraining : Agent
{
    private List<GameObject> orderedGoals;
    private CarControllerAgent carControllerAgent;
    private Rigidbody rb;
    private RaceManagerTraining raceManagerTraining;

    private int nextCheckpoint = 0;
    private int lapCount = 0;

    [Header("Raycast Settings")]
    [SerializeField] private int numRays = 32;
    [SerializeField] private float rayMaxDistance = 25f;
    [SerializeField] private float rayHeightOffset = 0.5f;

    [Header("Stuck Detection")]
    private float stuckTimeout = 8f;
    private float movementThreshold = 1.5f;
    private float minSpeedThreshold = 2f;

    [Header("Reward Settings")]
    private float checkpointReward = 3.0f;
    private float lapCompletionReward = 25f;
    private float wallCollisionPenalty = -50.0f;
    private float carCollisionPenalty = -15.0f;
    private float wrongCheckpointPenalty = -2.0f;
    private float stuckPenalty = -10.0f;
    private float speedRewardMultiplier = 0.003f;
    private float proximityRewardMultiplier = 0.05f;
    private float collisionStayPenalty = -0.2f;
    private float positionRewardMultiplier = 0.02f;
    private float competitiveRewardMultiplier = 0.05f;

    private Vector3 lastPosition;
    private float stuckTimer;
    private float lowSpeedTimer;
    private float previousCheckpointDistance;
    private float collisionCooldown;
    private bool isColliding;
    private float elapsed;
    private float lastSpeed;
    private float accelerationReward = 0.01f;

    private float episodeStartTime;
    private Vector3 previousFramePosition;

    public override void Initialize()
    {
        carControllerAgent = GetComponent<CarControllerAgent>();
        rb = GetComponent<Rigidbody>();

        if (transform.parent != null)
        {
            raceManagerTraining = transform.parent.GetComponent<RaceManagerTraining>();
        }

        if (raceManagerTraining != null)
        {
            raceManagerTraining.RegisterAgent(this);
            orderedGoals = raceManagerTraining.GetCheckpoints();
        }

        lastPosition = transform.position;
        previousFramePosition = transform.position;
        stuckTimer = 0f;
        lowSpeedTimer = 0f;
        collisionCooldown = 0f;
        isColliding = false;
        elapsed = 0f;
        lastSpeed = 0f;

        if (orderedGoals != null && orderedGoals.Count > 0 && nextCheckpoint < orderedGoals.Count)
        {
            previousCheckpointDistance = Vector3.Distance(transform.position, orderedGoals[nextCheckpoint].transform.position);
        }
    }

    private void FixedUpdate()
    {
        elapsed = Time.time - episodeStartTime;
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

        if (currentSpeed < minSpeedThreshold && !isColliding)
        {
            lowSpeedTimer += Time.fixedDeltaTime;
        }
        else
        {
            lowSpeedTimer = 0f;
        }

        if (stuckTimer >= stuckTimeout || lowSpeedTimer >= stuckTimeout)
        {
            AddReward(stuckPenalty);
            raceManagerTraining.ResetAllAgents();
        }

        lastPosition = transform.position;

        if (collisionCooldown > 0)
        {
            collisionCooldown -= Time.fixedDeltaTime;
        }

        lastSpeed = currentSpeed;
        previousFramePosition = transform.position;
    }

    public override void OnEpisodeBegin()
    {
        lastPosition = transform.position;
        previousFramePosition = transform.position;
        stuckTimer = 0f;
        lowSpeedTimer = 0f;
        collisionCooldown = 0f;
        isColliding = false;
        episodeStartTime = Time.time;
        elapsed = 0f;
        lastSpeed = 0f;

        lapCount = 0;
        nextCheckpoint = 0;

        orderedGoals = raceManagerTraining.GetCheckpoints();
        if (orderedGoals != null && orderedGoals.Count > 0)
        {
            previousCheckpointDistance = Vector3.Distance(transform.position, orderedGoals[nextCheckpoint].transform.position);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (carControllerAgent != null)
        {
            carControllerAgent.GetInput(
                actions.DiscreteActions[0],
                actions.DiscreteActions[1]
            );
        }

        Vector3 velLocal = transform.InverseTransformDirection(rb.linearVelocity);
        float forwardVel = Mathf.Max(0, velLocal.z);
        float speedReward = speedRewardMultiplier * forwardVel * Time.fixedDeltaTime;

        float currentSpeed = rb.linearVelocity.magnitude;
        if (currentSpeed > lastSpeed && currentSpeed > 5f)
        {
            speedReward += accelerationReward * (currentSpeed - lastSpeed) * Time.fixedDeltaTime;
        }

        AddReward(speedReward);

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

        if (raceManagerTraining != null)
        {
            int currentPosition = raceManagerTraining.GetAgentPosition(this);
            int totalAgents = raceManagerTraining.GetTotalAgents();
            if (totalAgents > 1)
            {
                float positionReward = ((float)(totalAgents - currentPosition) / (totalAgents - 1)) * positionRewardMultiplier * Time.fixedDeltaTime;
                AddReward(positionReward);
            }
        }
    }

    public void ResetAgent(Vector3 startingPosition, Quaternion startingRotation)
    {
        transform.position = startingPosition;
        transform.rotation = startingRotation;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

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
        lastSpeed = 0f;

        EndEpisode();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
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

        int thirdCheckpointIndex = (checkpointIndexToObserve + 2) % orderedGoals.Count;
        Vector3 toThirdCheckpointWorld = orderedGoals[thirdCheckpointIndex].transform.position - transform.position;
        Vector3 toThirdCheckpointLocal = transform.InverseTransformDirection(toThirdCheckpointWorld);
        sensor.AddObservation(toThirdCheckpointLocal.normalized);

        float alignment = Vector3.Dot(transform.forward, toCheckpointWorld.normalized);
        sensor.AddObservation(alignment);

        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float maxSpeed = 80f;
        sensor.AddObservation(Mathf.Clamp(localVelocity.x / maxSpeed, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(localVelocity.y / maxSpeed, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(localVelocity.z / maxSpeed, -1f, 1f));

        float maxAngularSpeed = 30f;
        sensor.AddObservation(Mathf.Clamp(rb.angularVelocity.y / maxAngularSpeed, -1f, 1f));

        float distanceToCheckpoint = toCheckpointWorld.magnitude;
        sensor.AddObservation(Mathf.Clamp01(distanceToCheckpoint / rayMaxDistance));

        sensor.AddObservation(Mathf.Clamp01((float)lapCount / 3f));
        sensor.AddObservation((float)nextCheckpoint / orderedGoals.Count);

        if (raceManagerTraining != null)
        {
            int position = raceManagerTraining.GetAgentPosition(this);
            int totalAgents = raceManagerTraining.GetTotalAgents();
            sensor.AddObservation((float)position / Mathf.Max(1, totalAgents));
        }
        else
        {
            sensor.AddObservation(0f);
        }

        var origin = transform.position + Vector3.up * rayHeightOffset;
        float angleStep = 360f / numRays;
        for (int i = 0; i < numRays; i++)
        {
            float angle = i * angleStep;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * transform.forward;

            RaycastHit[] hits = Physics.RaycastAll(origin, dir, rayMaxDistance).OrderBy(h => h.distance).ToArray();
            int hitCount = 0;
            const int maxHitsToObserve = 5;

            //Debug.DrawRay(origin, dir * rayMaxDistance, Color.red, 0.1f);

            for (int j = 0; j < hits.Length && hitCount < maxHitsToObserve; j++)
            {
                RaycastHit hitInfo = hits[j];
                float hitType = 0f;

                if (hitInfo.collider.CompareTag("wall"))
                {
                    hitType = 0.1f;
                    sensor.AddObservation(hitType);
                    sensor.AddObservation(hitInfo.distance / rayMaxDistance);
                    hitCount++;
                    break;
                }
                else if (hitInfo.collider.CompareTag("goal"))
                {
                    hitType = 0.2f;
                }
                else if (hitInfo.collider.CompareTag("car") && hitInfo.collider.gameObject != gameObject)
                {
                    hitType = 0.3f;
                }

                if (hitType > 0)
                {
                    sensor.AddObservation(hitType);
                    sensor.AddObservation(hitInfo.distance / rayMaxDistance);
                    hitCount++;
                }
            }

            for (int k = hitCount; k < maxHitsToObserve; k++)
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(1f);
            }
        }


        sensor.AddObservation(isColliding ? 1f : 0f);
        sensor.AddObservation(collisionCooldown > 0 ? 1f : 0f);
        sensor.AddObservation(Mathf.Clamp01(rb.linearVelocity.magnitude / maxSpeed));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("goal"))
        {
            if (orderedGoals == null || orderedGoals.Count == 0) return;

            var hitGoal = other.gameObject;
            if (nextCheckpoint < orderedGoals.Count && hitGoal == orderedGoals[nextCheckpoint])
            {
                float speedBonus = Mathf.Clamp(rb.linearVelocity.magnitude / 10f, 0f, 3f);
                AddReward(checkpointReward + speedBonus);
                nextCheckpoint++;

                if (nextCheckpoint >= orderedGoals.Count)
                {
                    lapCount++;
                    nextCheckpoint = 0;
                    float lapSpeedBonus = Mathf.Clamp(rb.linearVelocity.magnitude / 5f, 0f, 10f);
                    AddReward(lapCompletionReward + lapSpeedBonus);

                    if (lapCount >= 3)
                    {
                        float timeBonus = Mathf.Max(0, 400f - elapsed * 0.5f);
                        AddReward(timeBonus);
                        Debug.Log("Race completed. Time: " + elapsed);
                        raceManagerTraining.ResetAllAgents();
                        return;
                    }
                }

                if (orderedGoals.Count > 0 && nextCheckpoint < orderedGoals.Count)
                {
                    previousCheckpointDistance = Vector3.Distance(transform.position, orderedGoals[nextCheckpoint].transform.position);
                }

                if (raceManagerTraining != null)
                {
                    int overtaken = raceManagerTraining.UpdateAgentProgress(this, lapCount, nextCheckpoint);
                    if (overtaken > 0)
                    {
                        float overtakeBonus = overtaken * 5f;
                        AddReward(overtakeBonus);
                    }
                }
            }
            else
            {
                AddReward(wrongCheckpointPenalty);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("wall"))
        {
            float impactForce = collision.relativeVelocity.magnitude;
            float scaledPenalty = wallCollisionPenalty * Mathf.Clamp(impactForce / 20f, 0.3f, 2f);
            AddReward(scaledPenalty);
            collisionCooldown = 0.3f;
            isColliding = true;
        }
        else if (collision.collider.CompareTag("car"))
        {
            float impactForce = collision.relativeVelocity.magnitude;
            float scaledPenalty = carCollisionPenalty * Mathf.Clamp(impactForce / 15f, 0.2f, 1.5f);
            AddReward(scaledPenalty);
            collisionCooldown = 0.2f;
            isColliding = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag("wall") || collision.collider.CompareTag("car"))
        {
            isColliding = false;
        }
    }

    public int GetCurrentLap()
    {
        return lapCount;
    }

    public int GetCurrentCheckpoint()
    {
        return nextCheckpoint;
    }

    public int GetTotalProgress()
    {
        return lapCount * (orderedGoals?.Count ?? 0) + nextCheckpoint;
    }
}