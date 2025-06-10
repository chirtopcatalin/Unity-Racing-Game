using Unity.MLAgents.Integrations.Match3;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class CameraFollow : MonoBehaviour
{
    private Vector3 direction;
    private Vector3 desiredPosition;
    private int cameraMode = 0;
    private Quaternion rotation;
    public GameObject target;
    public Vector3 positionOffset;
    public Vector3 reversePositionOffset;
    public float cameraFollowSpeed = 1f;
    public float cameraRotationSpeed = 1f;
    public InputActionReference switchViewInput;
    void Start()
    {
        transform.position = target.transform.position + positionOffset;
    }

    void Update()
    {
        HandleMovement();
        HandleRotation();
        SwitchView();
    }

    void HandleMovement()
    {
        if (cameraMode == 0)
        {
            desiredPosition = target.transform.TransformPoint(positionOffset);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, cameraFollowSpeed * Time.deltaTime);
        }
        else if(cameraMode == 1)
        {
            transform.position = target.transform.TransformPoint(reversePositionOffset);
        }
    }

    void HandleRotation()
    {
        direction = target.transform.position - transform.position;

        rotation = Quaternion.LookRotation(direction, Vector3.up);

        if (cameraMode == 0)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, rotation, cameraRotationSpeed * Time.deltaTime);
        }
        else if (cameraMode == 1)
        {
            transform.LookAt(target.transform);
        }

    }

    void SwitchView()
{
    if (switchViewInput.action.IsPressed())
    {
        cameraMode = 1;
    }
    else if(switchViewInput.action.WasReleasedThisFrame())
    {
        desiredPosition = target.transform.TransformPoint(positionOffset);
        transform.position = desiredPosition;
        
        direction = target.transform.position - transform.position;
        rotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = rotation;
        
        cameraMode = 0;
    }
}
}
