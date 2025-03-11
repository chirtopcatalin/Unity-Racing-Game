using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.InputSystem;
using NUnit.Framework;
using System.Collections.Generic;

public class parkingAgent : Agent
{
    [SerializeField] private Transform targetTransform;
    [SerializeField] private MeshRenderer floorMesh;
    [SerializeField] private Material winMaterial;
    [SerializeField] private Material loseMaterial;
    [SerializeField] private GameObject carSensor;


    private float episodeStartTime;
    private AgentCarController carController;
    private int steeringInput;
    private int accelerationInput;

    ActionSegment<int> agentActions;
    CollisionDetector collisionDetector;
    float previousDistance;
    float currentDistance;
    List<Vector3> parkingLocations;

    private void Start()
    {
        parkingLocations = new List<Vector3>();
        for(int i = 0; i < 12; i++)
        {
            float distanceBetweenSpaces = 4f;
            parkingLocations.Add(new Vector3(33.13f, -0.07f, 23.02f - i * distanceBetweenSpaces));
        }
        collisionDetector = carSensor.GetComponent<CollisionDetector>();
        carController = GetComponent<AgentCarController>();
        previousDistance = Vector3.Distance(carSensor.GetComponent<Transform>().localPosition, targetTransform.localPosition);
        currentDistance = 0;
    }
    public override void OnEpisodeBegin()
    {
        int randomIndex = Random.Range(0, parkingLocations.Count);
        targetTransform.localPosition = parkingLocations[randomIndex];
        currentDistance = 0;
        //episodeStartTime = Time.time;
        transform.localPosition = new Vector3(Random.Range(26f,-33f), 1.37f, Random.Range(-20f, 22.3f));
        transform.rotation = Quaternion.Euler(0, 0, 0);
    }

    private void Update()
    {
        carController.GiveInput(accelerationInput, steeringInput);
    }

    private void FixedUpdate()
    {
        currentDistance = Vector3.Distance(collisionDetector.GetComponent<Transform>().localPosition, targetTransform.localPosition);
        var distanceChange = currentDistance - previousDistance;

        if (distanceChange < 0)
        {
            AddReward(0.03f);
        }
        else if (distanceChange > 0)
        {
            AddReward(-0.01f);
        }

        //if (Time.time - episodeStartTime > 60f)
        //{
        //    AddReward(-0.4f);
        //    floorMesh.material = loseMaterial;
        //    previousDistance = currentDistance;
        //    EndEpisode();
        //}

        if (collisionDetector.goalReached == 1)
        {
            SetReward(4f);
            floorMesh.material = winMaterial;
            collisionDetector.goalReached = 0;
            EndEpisode();
        }
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(targetTransform.localPosition);
        sensor.AddObservation(Vector3.Distance(transform.localPosition, targetTransform.localPosition));
        sensor.AddObservation(accelerationInput);
        sensor.AddObservation(steeringInput);
        sensor.AddObservation(carController.GetComponent<Rigidbody>().linearVelocity);


        for (int angle = 0; angle < 360; angle += 360 / 12)
        {
            RaycastHit hit;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0.6f, Mathf.Sin(angle));
            Physics.Raycast(transform.localPosition, direction, out hit, 10);
            sensor.AddObservation(hit.distance);
        }
    }

    // actions are making the agent move
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (actions.DiscreteActions[0] == 0)
        {
            accelerationInput = -1;
        }
        else if(actions.DiscreteActions[0] == 1)
        {
            accelerationInput = 0;
        }
        else if(actions.DiscreteActions[0] == 2)
        {
            accelerationInput = 1;
        }

        if (actions.DiscreteActions[1] == 0)
        {
            steeringInput = -1;
        }
        else if(actions.DiscreteActions[1] == 1)
        {
            steeringInput = 0;
        }
        else if(actions.DiscreteActions[1] == 2)
        {
            steeringInput = 1;
        }

    }

    // you can give the actions to test if your agent controls work
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        agentActions = actionsOut.DiscreteActions;
        if(Input.GetAxis("Vertical") > 0)
        {
            agentActions[0] = 2;
        }
        else if(Input.GetAxis("Vertical") < 0) {
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

    private void OnCollisionEnter(Collision collision)
    {

        if (collision.gameObject.CompareTag("wall"))
        {
            SetReward(-0.9f);
            floorMesh.material = loseMaterial;
            EndEpisode();
        }
    }

}
