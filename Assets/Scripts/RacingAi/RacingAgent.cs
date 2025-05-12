using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.InputSystem.XR;

public class RacingAgent : Agent
{
    private TrackCheckpoints trackCheckpoints;
    private List<GameObject> orderedGoals;
    private CarControllerAgent carControllerAgent;
    private Rigidbody rb;
    private RaceManagerTraining raceManager;

    private int nextCheckpoint = 0;
    private int lapCount = 0;

    public override void Initialize()
    {
        carControllerAgent = GetComponent<CarControllerAgent>();
        trackCheckpoints = transform.parent.GetComponent<TrackCheckpoints>();
        orderedGoals = trackCheckpoints.GetCheckpoints();
        rb = GetComponent<Rigidbody>();
        raceManager = transform.parent.GetComponent<RaceManagerTraining>();
        raceManager.RegisterAgent(this);
    }

    public override void OnEpisodeBegin()
    {
        orderedGoals = trackCheckpoints.GetCheckpoints();
        raceManager.ResetAllAgents();
    }

    public void ResetAgent(Vector3 startingPosition, Quaternion startingRotation)
    {
        rb.angularVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;
        transform.position= startingPosition;
        transform.rotation = startingRotation * Quaternion.Euler(0, 180f, 0);
        nextCheckpoint = 0;
        lapCount = 0;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        carControllerAgent.GetInput(
            actions.DiscreteActions[0],
            actions.DiscreteActions[1]
        );
    }
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var agentActions = actionsOut.DiscreteActions;
        if (Input.GetAxis("Vertical") > 0)
        {
            agentActions[0] = 2;
        }
        else if (Input.GetAxis("Vertical") < 0)
        {
            agentActions[0] = 0;
        }
        else
        {
            agentActions[0] = 1;
        }

        if (Input.GetAxis("Horizontal") > 0)
        {
            agentActions[1] = 2;
        }
        else if (Input.GetAxis("Horizontal") < 0)
        {
            agentActions[1] = 0;
        }
        else
        {
            agentActions[1] = 1;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(orderedGoals[nextCheckpoint].transform.localPosition);
        sensor.AddObservation(rb.transform.localRotation);
        sensor.AddObservation(rb.linearVelocity);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("goal"))
        {
            var hitGoal = other.gameObject;
            if (hitGoal == orderedGoals[nextCheckpoint])
            {
                AddReward(+0.1f);
                nextCheckpoint++;
                Debug.Log($"{name} hit checkpoint #{nextCheckpoint}");

                if (nextCheckpoint >= orderedGoals.Count)
                {
                    lapCount++;
                    //Debug.Log($"{name} completed lap {lapCount}!");
                    AddReward(+1f);
                    if (Academy.Instance.IsCommunicatorOn)
                        EndEpisode();
                    nextCheckpoint = 0;
                }
            }
            else
            {
                AddReward(-0.2f);
                //Debug.Log($"{name} hit WRONG checkpoint. Expected {orderedGoals[nextCheckpoint].name}.");
            }
        }
        else if (other.CompareTag("wall"))
        {
            if (Academy.Instance.IsCommunicatorOn)
            {
                AddReward(-0.1f);
                EndEpisode();
            }
            //Debug.Log($"{name} hit a wall.");
        }
        else if (other.CompareTag("car"))
        {
            AddReward(-0.2f);
        }
    }
}
