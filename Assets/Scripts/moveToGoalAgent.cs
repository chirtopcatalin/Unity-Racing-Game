using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class moveToGoalAgent : Agent
{
    [SerializeField] private Transform targetTransform;
    [SerializeField] private MeshRenderer floorMesh;
    [SerializeField] private Material winMaterial;
    [SerializeField] private Material loseMaterial;


    private float episodeStartTime;

    public override void OnEpisodeBegin()
    {
        episodeStartTime = Time.time;
        transform.localPosition = new Vector3(Random.Range(-4f,3f), 0.6f, Random.Range(-3.5f, 3.5f));
        targetTransform.localPosition = new Vector3(Random.Range(-4f, 3f), 0.6f, Random.Range(-3.5f, 3.5f));
    }

    private void FixedUpdate()
    {
        if (Time.time - episodeStartTime > 5f)
        {
            float distanceToTarget = Vector3.Distance(transform.localPosition, targetTransform.localPosition);
            SetReward(-distanceToTarget);
            floorMesh.material = loseMaterial;
            EndEpisode();
        }
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(targetTransform.localPosition);
        sensor.AddObservation(Vector3.Distance(transform.localPosition, targetTransform.localPosition));
    }

    // actions are making the agent move
    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        float moveSpeed = 5f;

        transform.position += new Vector3(moveX, 0, moveZ) * Time.deltaTime * moveSpeed;
    }

    // you can give the actions to test if your agent controls work
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Goal>(out Goal goal))
        {
            SetReward(1f);
            floorMesh.material = winMaterial;
            EndEpisode();
        }
        if (other.TryGetComponent<Wall>(out Wall Wall))
        {
            SetReward(-1f);
            floorMesh.material = loseMaterial;
            EndEpisode();
        }
    }

}
