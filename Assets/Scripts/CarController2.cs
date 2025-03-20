using UnityEngine;

public class AutomaticGearbox : MonoBehaviour
{
    // Car components
    public WheelCollider[] wheelColliders; // Reference to all wheel colliders
    public Transform[] wheelMeshes;        // Reference to wheel visual meshes

    // Engine and transmission properties
    public AnimationCurve engineTorqueCurve = new AnimationCurve(
        new Keyframe(0, 0.5f),
        new Keyframe(1000, 0.7f),
        new Keyframe(2000, 0.9f),
        new Keyframe(3000, 1.0f),
        new Keyframe(4000, 0.95f),
        new Keyframe(5000, 0.8f),
        new Keyframe(6000, 0.7f),
        new Keyframe(7000, 0.5f)
    );

    public float maxEngineTorque = 300f;
    public float maxEngineRPM = 7000f;
    public float idleRPM = 800f;
    public float redlineRPM = 6500f;

    // Gear ratios (first gear is highest numerical value)
    public float[] gearRatios = new float[] { 4.5f, 3.5f, 2.5f, 1.7f, 1.3f, 1.0f };
    public float finalDriveRatio = 3.7f;
    public float reverseGearRatio = -4.0f;

    // Automatic transmission settings
    public float upshiftRPM = 5500f;
    public float downshiftRPM = 2000f;
    public float clutchEngageSpeed = 3.0f; // How quickly clutch engages (higher is faster)

    // Current state
    private float currentEngineRPM;
    private int currentGear = 1; // Start in first gear (index 0)
    private float currentClutch = 0f; // 0 = disengaged, 1 = fully engaged
    private float targetClutch = 1f;
    private bool isShifting = false;
    private float shiftTimer = 0f;
    private float shiftDuration = 0.5f; // Time to complete a gear shift

    // Inputs
    private float throttleInput;
    private float brakeInput;
    private bool isReversing = false;

    void Update()
    {
        // Get input
        throttleInput = Input.GetAxis("Vertical");
        brakeInput = Input.GetKey(KeyCode.Space) ? 1.0f : 0.0f;

        // Toggle reverse
        if (Input.GetKeyDown(KeyCode.R))
        {
            isReversing = !isReversing;
            // Force neutral during transition to/from reverse
            currentClutch = 0f;
            targetClutch = 0f;
            StartCoroutine(WaitForStop());
        }

        // Handle gear shifting logic
        HandleGearShifting();

        // Update the clutch engagement
        UpdateClutch();

        // Update wheel meshes to match colliders
        UpdateWheelMeshes();

        // Debug display
        DebugDisplay();
    }

    void FixedUpdate()
    {
        // Calculate the current speed in km/h
        float speedKPH = GetVehicleSpeed() * 3.6f;

        // Calculate engine RPM based on wheel RPM, gear ratio, and final drive
        CalculateEngineRPM(speedKPH);

        // Apply torque to wheels based on engine output and clutch engagement
        ApplyTorqueToWheels();

        // Apply brakes
        ApplyBrakes();
    }

    private System.Collections.IEnumerator WaitForStop()
    {
        // Wait until the car is nearly stopped before allowing gear engagement
        while (GetVehicleSpeed() > 1.0f)
        {
            yield return null;
        }

        // Reset gear and allow clutch engagement
        currentGear = isReversing ? 0 : 1;
        targetClutch = 1f;
    }

    private void HandleGearShifting()
    {
        // Don't shift if we're in reverse or already shifting
        if (isReversing || isShifting) return;

        // Automatic upshift when RPM exceeds threshold
        if (currentEngineRPM > upshiftRPM && currentGear < gearRatios.Length - 1)
        {
            StartShifting(currentGear + 1);
        }
        // Automatic downshift when RPM drops below threshold
        else if (currentEngineRPM < downshiftRPM && currentGear > 1 && throttleInput > 0.1f)
        {
            StartShifting(currentGear - 1);
        }

        // Handle the shifting process
        if (isShifting)
        {
            shiftTimer += Time.deltaTime;
            if (shiftTimer >= shiftDuration)
            {
                CompleteShift();
            }
        }
    }

    private void StartShifting(int targetGear)
    {
        isShifting = true;
        shiftTimer = 0f;
        targetClutch = 0f; // Disengage clutch during shift
        // Target gear is set after the clutch is disengaged
    }

    private void CompleteShift()
    {
        // Update to new gear
        currentGear = Mathf.Clamp(currentGear + (currentEngineRPM > upshiftRPM ? 1 : -1), 1, gearRatios.Length - 1);

        // Re-engage clutch
        targetClutch = 1f;
        isShifting = false;
    }

    private void UpdateClutch()
    {
        // Smoothly engage/disengage clutch
        currentClutch = Mathf.Lerp(currentClutch, targetClutch, Time.deltaTime * clutchEngageSpeed);
    }

    private float GetVehicleSpeed()
    {
        // Calculate average wheel speed for more stability
        float sum = 0f;
        foreach (WheelCollider wheel in wheelColliders)
        {
            sum += wheel.rpm * wheel.radius * 2f * Mathf.PI / 60f; // Convert RPM to m/s
        }
        return sum / wheelColliders.Length;
    }

    private void CalculateEngineRPM(float speedKPH)
    {
        if (speedKPH < 0.1f && Mathf.Abs(throttleInput) < 0.1f)
        {
            // Idle RPM when stationary with no throttle
            currentEngineRPM = Mathf.Lerp(currentEngineRPM, idleRPM, Time.deltaTime * 3f);
            return;
        }

        // Get the current effective gear ratio
        float currentGearRatio = isReversing ? reverseGearRatio : gearRatios[currentGear - 1];

        // Calculate wheel RPM (simplified average)
        float wheelRPM = 0;
        for (int i = 0; i < wheelColliders.Length; i++)
        {
            wheelRPM += Mathf.Abs(wheelColliders[i].rpm);
        }
        wheelRPM /= wheelColliders.Length;

        // Calculate target engine RPM based on wheel speed and gear ratio
        float targetRPM = wheelRPM * currentGearRatio * finalDriveRatio;

        // Add some RPM when accelerating and clutch is disengaged
        if (throttleInput > 0 && currentClutch < 0.5f)
        {
            targetRPM = Mathf.Lerp(targetRPM, redlineRPM * throttleInput, 1f - currentClutch);
        }

        // Smooth RPM changes
        currentEngineRPM = Mathf.Lerp(currentEngineRPM, targetRPM, Time.deltaTime * 5f);

        // Clamp RPM to realistic values
        currentEngineRPM = Mathf.Clamp(currentEngineRPM, idleRPM, maxEngineRPM);
    }

    private void ApplyTorqueToWheels()
    {
        // Get torque from engine based on current RPM
        float normalizedRPM = currentEngineRPM / maxEngineRPM;
        float engineTorque = maxEngineTorque * engineTorqueCurve.Evaluate(normalizedRPM) * throttleInput;

        // Get the current effective gear ratio
        float currentGearRatio = isReversing ? reverseGearRatio : gearRatios[currentGear - 1];

        // Calculate torque at wheels
        float wheelTorque = engineTorque * currentGearRatio * finalDriveRatio * currentClutch;

        // Apply torque to drive wheels (assuming front wheel drive)
        for (int i = 0; i < 2; i++)  // Adjust as needed for your drive configuration
        {
            wheelColliders[i].motorTorque = wheelTorque;
        }
    }

    private void ApplyBrakes()
    {
        float brakeTorque = brakeInput * 2000f; // Adjust brake force as needed

        foreach (WheelCollider wheel in wheelColliders)
        {
            wheel.brakeTorque = brakeTorque;
        }
    }

    private void UpdateWheelMeshes()
    {
        for (int i = 0; i < wheelColliders.Length; i++)
        {
            Vector3 pos;
            Quaternion rot;
            wheelColliders[i].GetWorldPose(out pos, out rot);
            wheelMeshes[i].position = pos;
            wheelMeshes[i].rotation = rot;
        }
    }

    private void DebugDisplay()
    {
        // Display RPM and gear information in the console or UI
        Debug.Log($"RPM: {currentEngineRPM:F0} | Gear: {(isReversing ? "R" : currentGear)} | Clutch: {currentClutch:F2} | Speed: {GetVehicleSpeed() * 3.6f:F1} km/h");
    }
}