using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class CameraFollow : MonoBehaviour
{
    private Vector3 desiredPosition;
    private int cameraMode = 0;
    public GameObject target;
    public Vector3 positionOffset;
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
            desiredPosition = target.transform.TransformPoint(new Vector3(0, 15, 0));
            transform.position = Vector3.Lerp(transform.position, desiredPosition, cameraFollowSpeed * Time.deltaTime);
        }
    }

    void HandleRotation()
    {
        var direction = target.transform.position - transform.position;
        var rotation = new Quaternion();

        if (cameraMode == 0)
        {
            rotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, rotation, cameraRotationSpeed * Time.deltaTime);
        }
        else if (cameraMode == 1)
        {
            rotation = Quaternion.LookRotation(Vector3.down);
            transform.rotation = Quaternion.Lerp(transform.rotation, rotation, cameraRotationSpeed * Time.deltaTime);
        }

    }

    void SwitchView()
    {
        if (switchViewInput.action.WasPerformedThisFrame())
        {
            cameraMode = (cameraMode + 1) % 2;
        }
    }
}
