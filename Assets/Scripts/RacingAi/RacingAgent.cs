using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;

public class RacingAgent : Agent
{
    private CarController carController;
    private void Start()
    {
        carController = GetComponent<CarController>();
    }
    public override void OnActionReceived(ActionBuffers actions)
    {
        Debug.Log("steering:" + actions.DiscreteActions[1]);
        Debug.Log("acceleration:" + actions.DiscreteActions[0]);
    }
}
