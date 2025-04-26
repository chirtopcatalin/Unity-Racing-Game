using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class RacingAgent : Agent
{
    [Header("Track Setup")]
    [Tooltip("Assign all your Goal objects here, in the exact order of the track.")]
    public Transform[] checkpoints;
    [Tooltip("Tag your walls with “Wall”")]
    public string wallTag = "Wall";

    private int nextCheckpoint = 0;
    private CarControllerAgent carControllerAgent;

    public override void Initialize()
    {
        carControllerAgent = GetComponent<CarControllerAgent>();
    }

    public override void OnEpisodeBegin()
    {
        // Reset checkpoint index at the start of each episode
        nextCheckpoint = 0;
        // Also reset your car’s position, velocity, etc.
        //carControllerAgent.ResetCar();
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Steering and acceleration come from the network
        carControllerAgent.GetInput(
            actions.DiscreteActions[0],
            actions.DiscreteActions[1]
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1) Checkpoint logic
        if (other.transform == checkpoints[nextCheckpoint])
        {
            // Correct checkpoint!
            AddReward(+1.0f);
            nextCheckpoint++;

            if (nextCheckpoint >= checkpoints.Length)
            {
                // Completed a lap (or all checkpoints)
                AddReward(+5.0f);
                EndEpisode();
            }
        }
        else if (other.CompareTag("Wall"))
        {
            // Hit a wall: negative reward
            AddReward(-1.0f);
            EndEpisode();
        }
        else if (other.CompareTag("Goal"))
        {
            // Hit the wrong Goal (checkpoint) out of order
            AddReward(-0.5f);
        }
    }
}
