// RacingAgent.cs
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
    private RaceManagerTraining raceManager;

    private int nextCheckpoint = 0;
    private int lapCount = 0;

    [Header("Input Actions")]
    public InputActionReference acceleration;
    public InputActionReference steering;

    [Header("Raycast Settings")]
    [SerializeField] private int numRays = 16;
    [SerializeField] private float rayMaxDistance = 25f;
    [SerializeField] private float rayHeightOffset = 0.5f;

    [Header("Stuck Detection")]
    public float stuckTimeout = 5f;
    public float movementThreshold = 1f;
    public float minSpeedThreshold = 0.5f;

    [Header("Reward Settings")]
    public float checkpointReward = 5f;
    public float lapCompletionReward = 20f;
    public float wallCollisionPenalty = -5f;
    public float carCollisionPenalty = -3f;
    public float wrongCheckpointPenalty = -8f;
    public float stuckPenalty = -10f;
    public float speedRewardMultiplier = 0.01f;
    public float proximityRewardMultiplier = 0.1f;
    public float collisionStayPenalty = -0.1f;

    private Vector3 _lastPosition;
    private float _stuckTimer;
    private float _lowSpeedTimer;
    private float previousCheckpointDistance;
    private float _collisionCooldown;
    private bool _isColliding;

    // Performance tracking
    private float _episodeStartTime;
    private float _totalDistance;
    private Vector3 _previousFramePosition;

    public override void Initialize()
    {
        _lastPosition = transform.position;
        _previousFramePosition = transform.position;
        _stuckTimer = 0f;
        _lowSpeedTimer = 0f;
        _collisionCooldown = 0f;
        _isColliding = false;

        carControllerAgent = GetComponent<CarControllerAgent>();
        trackCheckpoints = transform.parent.GetComponent<TrackCheckpoints>();
        orderedGoals = trackCheckpoints.GetCheckpoints();
        rb = GetComponent<Rigidbody>();
        raceManager = transform.parent.GetComponent<RaceManagerTraining>();
        raceManager.RegisterAgent(this);

        if (orderedGoals.Count > 0)
        {
            previousCheckpointDistance = Vector3.Distance(transform.position, orderedGoals[nextCheckpoint].transform.position);
        }
    }

    private void FixedUpdate()
    {
        // Enhanced stuck detection
        float distanceMoved = Vector3.Distance(transform.position, _lastPosition);
        float currentSpeed = rb.linearVelocity.magnitude;

        // Check for position-based stuck condition
        if (distanceMoved < movementThreshold * Time.fixedDeltaTime)
        {
            _stuckTimer += Time.fixedDeltaTime;
        }
        else
        {
            _stuckTimer = 0f;
        }

        // Check for speed-based stuck condition
        if (currentSpeed < minSpeedThreshold)
        {
            _lowSpeedTimer += Time.fixedDeltaTime;
        }
        else
        {
            _lowSpeedTimer = 0f;
        }

        // Reset if stuck
        if (_stuckTimer >= stuckTimeout || _lowSpeedTimer >= stuckTimeout)
        {
            AddReward(stuckPenalty);
            Debug.Log($"{name}: Agent got stuck, resetting");
            raceManager.ResetAllAgents();
        }

        _lastPosition = transform.position;

        // Update collision cooldown
        if (_collisionCooldown > 0)
        {
            _collisionCooldown -= Time.fixedDeltaTime;
        }

        // Track total distance for performance metrics
        _totalDistance += Vector3.Distance(transform.position, _previousFramePosition);
        _previousFramePosition = transform.position;
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log("New episode starting");
        _lastPosition = transform.position;
        _previousFramePosition = transform.position;
        _stuckTimer = 0f;
        _lowSpeedTimer = 0f;
        _collisionCooldown = 0f;
        _isColliding = false;
        _totalDistance = 0f;
        _episodeStartTime = Time.time;

        orderedGoals = trackCheckpoints.GetCheckpoints();
        lapCount = 0;
        nextCheckpoint = 0;

        if (orderedGoals.Count > 0)
        {
            previousCheckpointDistance = Vector3.Distance(transform.position, orderedGoals[nextCheckpoint].transform.position);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("wall"))
        {
            AddReward(wallCollisionPenalty);
            _collisionCooldown = 1f; // 1 second cooldown
            //Debug.Log($"{name}: Hit wall, penalty applied");
        }
        else if (collision.collider.CompareTag("car"))
        {
            AddReward(carCollisionPenalty);
            _collisionCooldown = 0.5f; // 0.5 second cooldown
            //Debug.Log($"{name}: Hit another car, penalty applied");
        }
        _isColliding = true;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (_collisionCooldown <= 0 && (collision.collider.CompareTag("car") || collision.collider.CompareTag("wall")))
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
        EndEpisode();
        rb.angularVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;
        transform.position = startingPosition;
        transform.rotation = startingRotation * Quaternion.Euler(0, 180f, 0);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        carControllerAgent.GetInput(
            actions.DiscreteActions[0],
            actions.DiscreteActions[1]
        );

        // Speed reward - encourage forward movement
        Vector3 velLocal = transform.InverseTransformDirection(rb.linearVelocity);
        float forwardVel = Mathf.Max(0, velLocal.z); // Only reward forward movement
        AddReward(speedRewardMultiplier * forwardVel * Time.fixedDeltaTime);

        // Proximity reward - encourage approaching next checkpoint
        if (orderedGoals != null && orderedGoals.Count > 0)
        {
            float currentDistance = Vector3.Distance(transform.position, orderedGoals[nextCheckpoint].transform.position);
            float delta = previousCheckpointDistance - currentDistance;

            // Only reward if getting closer
            if (delta > 0)
            {
                AddReward(delta * proximityRewardMultiplier);
            }

            previousCheckpointDistance = currentDistance;
        }

        // Small penalty for being in collision to encourage avoidance
        if (_isColliding && _collisionCooldown <= 0)
        {
            AddReward(-0.01f * Time.fixedDeltaTime);
        }

        // Reward smooth driving (penalize excessive angular velocity)
        float angularVelPenalty = rb.angularVelocity.magnitude * -0.001f;
        AddReward(angularVelPenalty * Time.fixedDeltaTime);
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
            Debug.LogWarning($"{name}: orderedGoals is null or empty");
            return;
        }

        // Current checkpoint information
        int checkpointIndexToObserve = nextCheckpoint;
        if (checkpointIndexToObserve >= orderedGoals.Count)
            checkpointIndexToObserve = 0;

        Vector3 toCheckpointWorld = orderedGoals[checkpointIndexToObserve].transform.position - transform.position;
        Vector3 toCheckpointLocal = transform.InverseTransformDirection(toCheckpointWorld);
        sensor.AddObservation(toCheckpointLocal.normalized);

        // Next checkpoint information (for better planning)
        int nextCheckpointIndex = (checkpointIndexToObserve + 1) % orderedGoals.Count;
        Vector3 toNextCheckpointWorld = orderedGoals[nextCheckpointIndex].transform.position - transform.position;
        Vector3 toNextCheckpointLocal = transform.InverseTransformDirection(toNextCheckpointWorld);
        sensor.AddObservation(toNextCheckpointLocal.normalized);

        // Alignment with checkpoint direction
        Vector3 toCheckpointDir = toCheckpointWorld.normalized;
        float alignment = Vector3.Dot(transform.forward, toCheckpointDir);
        sensor.AddObservation(alignment);

        // Velocity information
        sensor.AddObservation(rb.linearVelocity);
        sensor.AddObservation(rb.angularVelocity.y); // Only Y-axis rotation matters for steering

        // Distance to current checkpoint (normalized)
        float distanceToCheckpoint = toCheckpointWorld.magnitude / rayMaxDistance;
        sensor.AddObservation(Mathf.Clamp01(distanceToCheckpoint));

        // Lap and checkpoint progress
        sensor.AddObservation((float)lapCount / 3f); // Normalize by max laps
        sensor.AddObservation((float)nextCheckpoint / orderedGoals.Count);

        // Raycast observations with improved collision avoidance data
        var origin = transform.position + Vector3.up * rayHeightOffset;
        float angleStep = 360f / numRays;

        for (int i = 0; i < numRays; i++)
        {
            float angle = i * angleStep;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * transform.forward;

            var hits = Physics.RaycastAll(origin, dir, rayMaxDistance);

            float distToGoal = rayMaxDistance;
            float distToWall = rayMaxDistance;
            float distToCar = rayMaxDistance;

            foreach (var hit in hits)
            {
                if (hit.collider.CompareTag("goal") && hit.distance < distToGoal)
                    distToGoal = hit.distance;
                else if (hit.collider.CompareTag("wall") && hit.distance < distToWall)
                    distToWall = hit.distance;
                else if (hit.collider.CompareTag("car") && hit.distance < distToCar && hit.collider.gameObject != gameObject)
                    distToCar = hit.distance;
            }

            sensor.AddObservation(distToGoal / rayMaxDistance);
            sensor.AddObservation(distToWall / rayMaxDistance);
            sensor.AddObservation(distToCar / rayMaxDistance);
        }

        // Add collision state
        sensor.AddObservation(_isColliding ? 1f : 0f);
        sensor.AddObservation(_collisionCooldown > 0 ? 1f : 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("goal"))
        {
            var hitGoal = other.gameObject;
            if (hitGoal == orderedGoals[nextCheckpoint])
            {
                // Scaled checkpoint reward
                AddReward(checkpointReward);
                //Debug.Log($"{name}: Checkpoint {nextCheckpoint} reached! Reward: {checkpointReward}");

                nextCheckpoint++;
                if (nextCheckpoint >= orderedGoals.Count)
                {
                    lapCount++;
                    nextCheckpoint = 0;

                    // Lap completion bonus
                    AddReward(lapCompletionReward);
                    //Debug.Log($"{name}: Lap {lapCount} completed! Bonus: {lapCompletionReward}");

                    if (lapCount >= 3)
                    {
                        // Race completion bonus
                        float timeBonus = Mathf.Max(0, 50f - (Time.time - _episodeStartTime) * 0.1f);
                        AddReward(timeBonus);
                        Debug.Log($"{name}: Race completed! Time bonus: {timeBonus}");
                        raceManager.ResetAllAgents();
                    }
                }

                if (orderedGoals.Count > 0)
                {
                    previousCheckpointDistance = Vector3.Distance(transform.position, orderedGoals[nextCheckpoint].transform.position);
                }

                // Overtaking bonus
                int overtaken = raceManager.UpdateAgentProgress(this, lapCount, nextCheckpoint);
                if (overtaken > 0)
                {
                    float overtakeBonus = overtaken * 2f;
                    AddReward(overtakeBonus);
                    //Debug.Log($"{name}: Overtook {overtaken} agents! Bonus: {overtakeBonus}");
                }
            }
            else
            {
                Debug.Log($"{name}: Wrong checkpoint hit!");
                AddReward(wrongCheckpointPenalty);
            }
        }
    }
}