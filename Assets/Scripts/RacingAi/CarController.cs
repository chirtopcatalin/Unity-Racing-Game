using Google.Protobuf.WellKnownTypes;
using NUnit.Framework.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;

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

    [Header("Steering")]
    public AnimationCurve steeringCurve;

    [Header("Car Wheels")]
    public List<Wheel> wheels;

    public TMP_Text gearText;
    public TMP_Text RPMText;
    public TMP_Text speedText;
    public TMP_Text rankText;

    private float accelerationInput;
    private float steeringInput;
    private bool brakingInput = false;


    internal enum driveType
    {
        fwd,
        rwd,
        awd
    }
    [SerializeField] private driveType drive;

    [Header("Variables")]
    public float handBrakeFrictionMultiplier = 2f;
    public float maxRPM;
    public float minRPM;
    public float[] gears;
    public float[] gearChangeSpeed;
    public AnimationCurve enginePowerCurve;

    [HideInInspector] public int gearNum = 1;
    [HideInInspector] public float KPH;
    [HideInInspector] public float engineRPM;
    [HideInInspector] public bool reverse = false;

    private Rigidbody rb;

    private float totalPower;
    private float brakingPower = 0;
    private float driftFactor;
    private bool flag = false;
    float radius = 6;
    private float smoothTime = 0.09f;
    private float downForceValue = 10f;

    private WheelFrictionCurve forwardFriction, sidewaysFriction;
    private RaceManager raceManager;

    void Start()
    {
        raceManager = this.transform.parent.GetComponent<RaceManager>();
        rb = GetComponent<Rigidbody>();
        accelerationInput = 0f;
        steeringInput = 0f;
    }

    private void Awake()
    {
    }

    void Update()
    {
        radius = 4 + KPH / 25;
        UpdateGearDisplay();
        AnimateWheels();

        if (!raceManager.IsCountdownActive())
        {
            GetInput();
        }
    }

    void FixedUpdate()
    {
        CalculateEngineTorque();
        AddDownForce();
        Steer();
        AdjustTraction();
    }

    void UpdateGearDisplay()
    {
        RPMText.text = "RPM: " + engineRPM.ToString("F0");
        gearText.text = "Gear:" + (gearNum+1).ToString("F0");
        speedText.text = "Speed: " + KPH.ToString("F1") + "KM/H";
        rankText.text = "Place: " + raceManager.GetPlayerRank().ToString();
    }

    void GetInput()
    {
        accelerationInput = acceleration.action.ReadValue<float>();
        steeringInput = steering.action.ReadValue<float>();
        brakingInput = brake.action.IsPressed();
    }

    void Move()
    {
        ApplyBrake();

        switch (drive)
        {
            case driveType.awd:
                foreach (var wheel in wheels)
                {
                    wheel.wheelCollider.motorTorque = totalPower / 4;
                    wheel.wheelCollider.brakeTorque = brakingPower;
                }
                break;
            case driveType.rwd:
                wheels[2].wheelCollider.motorTorque = totalPower / 2;
                wheels[3].wheelCollider.motorTorque = totalPower / 2;
                break;
            case driveType.fwd:
                foreach (var wheel in wheels)
                {
                    wheels[0].wheelCollider.motorTorque = totalPower / 2;
                    wheels[1].wheelCollider.motorTorque = totalPower / 2;
                }
                break;
            default:
                foreach (var wheel in wheels)
                {
                    wheel.wheelCollider.brakeTorque = brakingPower;
                }
                break;
        }
        KPH = rb.linearVelocity.magnitude * 3.6f;
    }

    void ApplyBrake()
    {
        if (accelerationInput < 0)
        {
            brakingPower = (KPH >= 10) ? 500 : 0;
        }
        else if (accelerationInput == 0 && (KPH <= 10))
        {
            brakingPower = 10;
        }
        else
        {
            brakingPower = 0;
        }
    }

    private void CalculateEngineTorque()
    {
        if (accelerationInput != 0)
        {
            rb.linearDamping = 0.005f;
        }
        if (steeringInput == 0)
        {
            rb.linearDamping = 0.1f;
        }
        totalPower = 3.6f * enginePowerCurve.Evaluate(engineRPM) * (accelerationInput);

        float velocity = 0.0f;
        if (engineRPM >= maxRPM || flag)
        {
            engineRPM = Mathf.SmoothDamp(engineRPM, maxRPM - 500, ref velocity, 0.05f);

            flag = (engineRPM >= maxRPM - 450) ? true : false;
        }
        else
        {
            engineRPM = Mathf.SmoothDamp(engineRPM, 1000 + (Mathf.Abs(WheelsRPM()) * 3.6f * (gears[gearNum])), ref velocity, smoothTime);
        }
        if (engineRPM >= maxRPM + 1000) engineRPM = maxRPM + 1000; // clamp
        Move();
        Shifter();
    }

    private void AddDownForce()
    {

        rb.AddForce(-transform.up * downForceValue * rb.linearVelocity.magnitude);

    }

    void Shifter()
    {
        if (!isGrounded()) return;

        if (engineRPM > maxRPM && gearNum < gears.Length - 1 && !reverse && KPH >= gearChangeSpeed[gearNum])
        {
            gearNum++;
            return;
        }
        if (engineRPM < minRPM && gearNum > 0)
        {
            gearNum--;
        }
    }

    bool isGrounded()
    {
        foreach (var wheel in wheels)
        {
            if (!wheel.wheelCollider.isGrounded) return false;
        }
        return true;
    }

    float WheelsRPM()
    {
        float sum = 0;
        foreach (var wheel in wheels)
        {
            sum += wheel.wheelCollider.rpm;
        }
        float avg = sum / wheels.Count;

        if (avg < 0 && !reverse) reverse = true;
        else if (avg > 0 && reverse) reverse = false;

        return avg;
    }

    void Steer()
    {
        var steeringAngle = steeringInput * steeringCurve.Evaluate(KPH);

        foreach (Wheel wheel in wheels)
        {
            if (wheel.axle == Axle.Front)
            {
                wheel.wheelCollider.steerAngle = steeringAngle;
            }
        }
    }

    void AnimateWheels()
    {
        foreach (var wheel in wheels)
        {
            Vector3 pos;
            Quaternion rot;
            wheel.wheelCollider.GetWorldPose(out pos, out rot);
            wheel.wheelModel.transform.position = pos;
            wheel.wheelModel.transform.rotation = rot;
        }
    }

    private void AdjustTraction()
    {
        float driftSmothFactor = .7f * Time.deltaTime;

        if (brakingInput)
        {
            sidewaysFriction = wheels[0].wheelCollider.sidewaysFriction;
            forwardFriction = wheels[0].wheelCollider.forwardFriction;

            float velocity = 0;
            sidewaysFriction.extremumValue = sidewaysFriction.asymptoteValue = forwardFriction.extremumValue = forwardFriction.asymptoteValue =
                Mathf.SmoothDamp(forwardFriction.asymptoteValue, driftFactor * handBrakeFrictionMultiplier, ref velocity, driftSmothFactor);

            for (int i = 0; i < 4; i++)
            {
                wheels[i].wheelCollider.sidewaysFriction = sidewaysFriction;
                wheels[i].wheelCollider.forwardFriction = forwardFriction;
            }

            sidewaysFriction.extremumValue = sidewaysFriction.asymptoteValue = forwardFriction.extremumValue = forwardFriction.asymptoteValue = 1.1f;
            for (int i = 0; i < 2; i++)
            {
                wheels[i].wheelCollider.sidewaysFriction = sidewaysFriction;
                wheels[i].wheelCollider.forwardFriction = forwardFriction;
            }
            rb.AddForce(transform.forward * (KPH / 400) * 10000);
        }
        else
        {

            forwardFriction = wheels[0].wheelCollider.forwardFriction;
            sidewaysFriction = wheels[0].wheelCollider.sidewaysFriction;

            forwardFriction.extremumValue = forwardFriction.asymptoteValue = sidewaysFriction.extremumValue = sidewaysFriction.asymptoteValue =
                ((KPH * handBrakeFrictionMultiplier) / 300) + 1;

            for (int i = 0; i < 4; i++)
            {
                wheels[i].wheelCollider.forwardFriction = forwardFriction;
                wheels[i].wheelCollider.sidewaysFriction = sidewaysFriction;

            }
        }

        for (int i = 2; i < 4; i++)
        {

            WheelHit wheelHit;

            wheels[i].wheelCollider.GetGroundHit(out wheelHit);

            if (wheelHit.sidewaysSlip < 0) driftFactor = (1 + -steeringInput) * Mathf.Abs(wheelHit.sidewaysSlip);

            if (wheelHit.sidewaysSlip > 0) driftFactor = (1 + steeringInput) * Mathf.Abs(wheelHit.sidewaysSlip);
        }

    }

    private IEnumerator timedLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(.7f);
            radius = 6 + KPH / 20;
        }
    }
}
