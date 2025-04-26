using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Composites;
using Random = UnityEngine.Random;
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

    public enum GearState
    {
        Neutral,
        Running,
        CheckingChange,
        Changing
    };

    [Header("Input Actions")]
    public InputActionReference acceleration;
    public InputActionReference steering;
    public InputActionReference brake;

    [Header("movement")]
    public float enginePower;
    public float maxReverseAcceleration;
    public float maxSteeringAngle;
    public float steeringRate;
    public float brakingPower;
    public AnimationCurve steeringCurve;

    [Header("Car wheels")]
    public List<Wheel> wheels;

    private Rigidbody rb;
    private float accelerationInput;
    private float steeringInput;
    private bool brakingInput = false;
    private float carSpeed;

    public TMP_Text gearText;
    public TMP_Text RPMText;

    public float[] gearRatios;
    public float differentialRatio;
    private float currentTorque;
    private float RPM;
    public float idleRPM;
    public float redLine;
    private float wheelRPM;
    public AnimationCurve hpToRPMCurve;
    public int currentGear;
    public float increaseGearRPM;
    public float decreaseGearRPM;
    public float changeGearTime;
    private GearState gearState;
    private float speed;



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
        //Debug.Log("speed:" + rb.linearVelocity.magnitude);
        RPMText.text = "RPM: " + RPM.ToString("F0");

        // Update gear display based on current state
        UpdateGearDisplay();

        speed = wheels[3].wheelCollider.rpm * wheels[3].wheelCollider.radius * 2f * Mathf.PI / 10;
        GetInput();
        AnimateWheels();
        CastRay();
    }

    void CastRay()
    {
        Vector3 origin = transform.position + Vector3.up * 0.3f;
        Vector3 direction = transform.forward;
        float maxDistance = 10f;

        RaycastHit hit;

        if (Physics.Raycast(origin, direction, out hit, maxDistance))
        {
            GameObject hitObject = hit.collider.gameObject;
            Goal goalScript = hitObject.GetComponent<Goal>();

            if (goalScript != null)
            {
                Debug.Log("Ray hit a goal: " + hitObject.name);
            }
            else
            {
                Debug.Log("Ray hit something, but it's not a goal: " + hitObject.name);
            }
        }
        else
        {
            Debug.Log("Nothing hit");
        }
    }


    void UpdateGearDisplay()
    {
        int direction = GetDirectionOfMovement();

        if (gearState == GearState.Neutral || (carSpeed < 0.5f && accelerationInput == 0))
        {
            gearText.text = "Gear: N";
        }
        else if (direction == -1 && accelerationInput < 0)
        {
            gearText.text = "Gear: R";
        }
        else
        {
            gearText.text = "Gear: " + (currentGear + 1).ToString();
        }
    }

    // used mainly for physics calculations
    void FixedUpdate()
    {
        Move();
        Steer();
        HandBrake();
    }

    void Move()
    {
        int direction = GetDirectionOfMovement();
        float speed = rb.linearVelocity.magnitude;
        currentTorque = CalculateTorque();

        foreach (Wheel wheel in wheels)
        {
            wheel.wheelCollider.brakeTorque = 0;
        }

        if (accelerationInput > 0)
        {
            if (direction >= 0 || speed < 0.5f)
            {
                ApplyTorque(currentTorque);
            }
            else if (direction == -1 && speed >= 0.5f)
            {
                ApplyBrake(brakingPower);
            }
        }
        else if (accelerationInput < 0)
        {
            if (direction <= 0 || speed < 0.5f)
            {
                ApplyTorque(accelerationInput * maxReverseAcceleration);
            }
            else if (direction == 1 && speed >= 0.5f)
            {
                ApplyBrake(brakingPower);
            }
        }
        else
        {
            ApplyTorque(0);
        }
    }

    float CalculateTorque()
    {
        float torque = 0;

        if (RPM < idleRPM + 200 && accelerationInput == 0 && currentGear == 0)
        {
            gearState = GearState.Neutral;
        }
        if (accelerationInput > 0 && gearState == GearState.Neutral)
        {
            gearState = GearState.Running;
        }
        if (gearState == GearState.Running)
        {
            if (RPM > increaseGearRPM && currentGear < gearRatios.Length - 1)
            {
                StartCoroutine(ChangeGear(1));
            }
            else if (RPM < decreaseGearRPM && currentGear > 0)
            {
                StartCoroutine(ChangeGear(-1));
            }
        }

        wheelRPM = Mathf.Abs((GetAverageWheelRPM()) * gearRatios[currentGear] * differentialRatio);
        RPM = Mathf.Lerp(RPM, Mathf.Max(idleRPM, wheelRPM), Time.deltaTime * 3f);

        torque = (hpToRPMCurve.Evaluate(RPM / redLine) * enginePower / RPM) * gearRatios[currentGear] * differentialRatio * 5252f;

        return torque;
    }

    float GetAverageWheelRPM()
    {
        float averageRPM = 0;
        averageRPM = (wheels[2].wheelCollider.rpm + wheels[3].wheelCollider.rpm);
        return averageRPM;
    }


    void ApplyBrake(float brakingForce)
    {
        foreach (Wheel wheel in wheels)
        {
            if (wheel.axle == Axle.Front)
            {
                wheel.wheelCollider.brakeTorque = brakingPower * 0.7f;
            }
            else
            {
                wheel.wheelCollider.brakeTorque = brakingPower * 0.3f;
            }

        }
    }

    void ApplyTorque(float torque)
    {
        foreach (Wheel wheel in wheels)
        {
            wheel.wheelCollider.motorTorque = torque;
        }
    }

    IEnumerator ChangeGear(int gearChange)
    {
        gearState = GearState.CheckingChange;
        if (currentGear + gearChange >= 0 && currentGear + gearChange < gearRatios.Length)
        {
            if (gearChange > 0)
            {
                //increase the gear
                yield return new WaitForSeconds(0.7f);
                if (RPM < increaseGearRPM || currentGear >= gearRatios.Length - 1)
                {
                    gearState = GearState.Running;
                    yield break;
                }
            }
            if (gearChange < 0)
            {
                //decrease the gear
                yield return new WaitForSeconds(0.1f);

                if (RPM > decreaseGearRPM || currentGear <= 0)
                {
                    gearState = GearState.Running;
                    yield break;
                }
            }
            gearState = GearState.Changing;
            yield return new WaitForSeconds(changeGearTime);
            currentGear += gearChange;
        }

        if (gearState != GearState.Neutral)
        {
            gearState = GearState.Running;
        }
    }


    void Steer()
    {
        var steeringAngle = steeringInput * steeringCurve.Evaluate(carSpeed);

        foreach (Wheel wheel in wheels)
        {
            if (wheel.axle == Axle.Front)
            {

                if (steeringInput == 1 || steeringInput == -1)
                {
                    steeringRate = 2.5f;
                }
                else
                {
                    steeringRate = 7f;
                }

                wheel.wheelCollider.steerAngle = Mathf.Lerp(wheel.wheelCollider.steerAngle, steeringAngle, steeringRate * Time.fixedDeltaTime);

                if (steeringInput == 0 && Math.Abs(wheel.wheelCollider.steerAngle) < 0.2)
                {
                    wheel.wheelCollider.steerAngle = 0;
                }
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

    void HandBrake()
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


    int GetDirectionOfMovement()
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