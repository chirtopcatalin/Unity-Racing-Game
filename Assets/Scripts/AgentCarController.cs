using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class AgentCarController : MonoBehaviour
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

    [Header("movement")]
    public float maxAcceleration = 1500f;
    public float maxReverseAcceleration = 1000f;
    public float maxSteeringAngle = 30f;
    public float steeringRate = 1f;
    public float brakingPower = 3000f;

    [Header("Car wheels")]
    public List<Wheel> wheels;

    private Rigidbody rb;
    private int accelerationInput;
    private int steeringInput;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        accelerationInput = 0;
        steeringInput = 0;
    }

    // Update is called once per frame
    void Update()
    {
    }

    // used mainly for physics calculations
    void FixedUpdate()
    {
        Move(accelerationInput);
        Steer(steeringInput);
        AnimateWheels();
        //Brake(brakingInput);
        
    }

    void Move(int accelerationInput)
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

    void Steer(int steeringInput)
    {
        foreach (Wheel wheel in wheels)
        {
            if (wheel.axle == Axle.Front)
            {
                var steeringAngle = steeringInput * maxSteeringAngle;
                wheel.wheelCollider.steerAngle = Mathf.Lerp(wheel.wheelCollider.steerAngle, steeringAngle, steeringRate * Time.deltaTime);

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

    //void Brake(bool brakingInput)
    //{
    //    if (brakingInput)
    //    {
    //        foreach (Wheel wheel in wheels)
    //        {
    //            if (wheel.axle == Axle.Rear)
    //            {
    //                wheel.wheelCollider.brakeTorque = brakingPower * 10;
    //            }
    //            else
    //            {
    //                wheel.wheelCollider.brakeTorque = 0;
    //            }
    //        }
    //    }
    //}

    public void GiveInput(int accelerationInput, int steeringInput)
    {
        this.accelerationInput = accelerationInput;
        this.steeringInput = steeringInput;
        //this.brakingInput = brakingInput == 1;
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
