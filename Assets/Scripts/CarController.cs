using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Composites;
using static AgentCarController;
using static CarController;
public class CarController : MonoBehaviour
{
    public enum Axle
    {
        Front,
        Rear
    }

    [Serializable]
    public struct Wheel
    {
        public GameObject wheelModel;
        public WheelCollider wheelCollider;
        public Axle axle;
    }

    [Header("Input Actions")]
    public InputActionReference acceleration;
    public InputActionReference steering;
    public InputActionReference brake;
    public InputActionReference spawnLabyrinthInput;

    [Header("movement")]
    public float maxAcceleration = 1500f;
    public float maxReverseAcceleration = 1000f;
    public float maxSteeringAngle = 60f;
    public float steeringRate = 1f;
    public float brakingPower = 3000f;
    public AnimationCurve steeringCurve;

    [Header("Car wheels")]
    public List<Wheel> wheels;

    private Rigidbody rb;
    private float accelerationInput = 0f;
    private float steeringInput = 0f;
    private bool brakingInput = false;
    private float carSpeed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        accelerationInput = 0f;
        steeringInput = 0f;
    }

    // Update is called once per frame
    void Update()
    {
        carSpeed = rb.linearVelocity.magnitude;
        GetInput();
        AnimateWheels();
    }

    // used mainly for physics calculations
    void FixedUpdate()
    {
        Move();
        Steer();
        Brake();
        Debug.Log(carSpeed);
    }

    void Move()
    {
        foreach (Wheel wheel in wheels)
        {
            int direction = DirectionOfMovement();

            if (direction == 1 && accelerationInput == 1)
            {
                wheel.wheelCollider.motorTorque = accelerationInput * maxAcceleration;
            }
            else if (direction == 0 && accelerationInput == 1)
            {
                wheel.wheelCollider.brakeTorque = 0;
                wheel.wheelCollider.motorTorque = accelerationInput * maxAcceleration;
            }
            else if (direction == -1 && accelerationInput == 1)
            {
                wheel.wheelCollider.brakeTorque = brakingPower;
            }
            else if (direction == -1 && accelerationInput == -1)
            {
                wheel.wheelCollider.motorTorque = accelerationInput * maxReverseAcceleration;
            }
            else if (direction == 0 && accelerationInput == -1)
            {
                wheel.wheelCollider.brakeTorque = 0;
                wheel.wheelCollider.motorTorque = accelerationInput * maxReverseAcceleration;
            }
            else if (direction == 1 && accelerationInput == -1)
            {
                wheel.wheelCollider.brakeTorque = brakingPower;
            }
            else
            {
                wheel.wheelCollider.brakeTorque = 0;
                wheel.wheelCollider.motorTorque = 0;
            }

        }
    }

    void Steer()
    {
        foreach (Wheel wheel in wheels)
        {
            if (wheel.axle == Axle.Front)
            {
                var steeringAngle = steeringInput * steeringCurve.Evaluate(carSpeed);
                wheel.wheelCollider.steerAngle = Mathf.Lerp(wheel.wheelCollider.steerAngle, steeringAngle, steeringRate * Time.fixedDeltaTime); // valoare mica - se apropie mai ince de steeringAngle

            }
        }

    }

    void AnimateWheels()
    {
        foreach (Wheel wheel in wheels)
        {
            Vector3 pos;
            Quaternion rot;
            wheel.wheelCollider.GetWorldPose(out pos, out rot);

            wheel.wheelModel.transform.position = pos;
            wheel.wheelModel.transform.rotation = rot;
        }
    }

    void Brake()
    {
        if (brakingInput == true)
        {
            foreach (Wheel wheel in wheels)
            {
                if (wheel.axle == Axle.Rear)
                {
                    wheel.wheelCollider.brakeTorque = brakingPower;
                }
                else
                {
                    wheel.wheelCollider.brakeTorque = 0;
                }
            }
        }

    }

    void GetInput()
    {
        accelerationInput = acceleration.action.ReadValue<float>();
        steeringInput = steering.action.ReadValue<float>();
        brakingInput = brake.action.IsPressed();
    }

    int DirectionOfMovement()
    {
        if (Vector3.Dot(rb.linearVelocity, transform.forward) > 0.1f)
        {
            return 1; // forward
        }
        else if (Vector3.Dot(rb.linearVelocity, transform.forward) < -0.1f)
        {
            return -1; // backward
        }
        else
        {
            return 0; // not moving
        }
    }

   
}