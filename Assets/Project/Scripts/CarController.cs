using UnityEngine;

namespace ParkPilot
{
    /// <summary>
    /// Kinematic bicycle-model car driven through a Rigidbody.
    /// Deliberately simple (no WheelColliders) so it stays stable at high training time scales.
    /// Inputs: steer in [-1, 1] (right positive), throttle in [-1, 1] (forward positive).
    /// Throttle against the current direction of travel acts as a brake.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        [Header("Speed")]
        [Tooltip("Top forward speed in m/s (parking speeds, not road speeds). v11: 5 -> 3, same as reverse - " +
                 "nose-in entries at up to 5 m/s overshot the bay; reverse-in (capped at 3 m/s) parked cleanly.")]
        public float maxForwardSpeed = 3f;
        [Tooltip("Top reverse speed in m/s.")]
        public float maxReverseSpeed = 3f;
        [Tooltip("Acceleration in m/s^2 when throttle is in the direction of travel.")]
        public float acceleration = 4f;
        [Tooltip("Deceleration in m/s^2 when throttle opposes the direction of travel.")]
        public float brakeDeceleration = 8f;
        [Tooltip("Deceleration in m/s^2 when there is no throttle input (only used if throttleDeadzone is 0).")]
        public float coastDeceleration = 1.5f;
        [Tooltip("v10: |throttle| below this counts as 'foot off the pedal' and brakes the car firmly to a stop. " +
                 "Makes stopping a wide, noise-tolerant action. 0 = old behaviour (gentle coasting).")]
        public float throttleDeadzone = 0.15f;
        [Tooltip("v10: deceleration in m/s^2 applied inside the throttle deadzone.")]
        public float autoBrakeDeceleration = 6f;

        [Header("Steering")]
        [Tooltip("Distance between front and rear axles in metres.")]
        public float wheelBase = 2.6f;
        [Tooltip("Maximum front-wheel steering angle in degrees.")]
        public float maxSteerAngle = 35f;
        [Tooltip("How fast the wheels can turn, in degrees per second.")]
        public float steerRate = 150f;

        [Header("Visuals (optional)")]
        public Transform frontLeftWheel;
        public Transform frontRightWheel;

        Rigidbody rb;
        float steerInput;
        float throttleInput;
        float speed;
        float steerAngle;
        Quaternion flBaseRotation = Quaternion.identity;
        Quaternion frBaseRotation = Quaternion.identity;

        /// <summary>Signed speed along the car's forward axis (m/s).</summary>
        public float Speed => speed;
        /// <summary>Current front-wheel angle in degrees.</summary>
        public float SteerAngle => steerAngle;
        public float MaxSpeed => Mathf.Max(maxForwardSpeed, maxReverseSpeed);
        public Rigidbody Body => rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            // The controller sets velocity directly, so the body must not drag on the ground.
            // With default friction (0.6 -> ~5.9 m/s^2) the car could barely move.
            var noFriction = new PhysicsMaterial("CarNoFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            foreach (var col in GetComponentsInChildren<Collider>())
                if (!col.isTrigger) col.sharedMaterial = noFriction;

            if (frontLeftWheel) flBaseRotation = frontLeftWheel.localRotation;
            if (frontRightWheel) frBaseRotation = frontRightWheel.localRotation;
        }

        /// <summary>Last commands (-1..1), for the showcase debug overlay.</summary>
        public float SteerInput => steerInput;
        public float ThrottleInput => throttleInput;

        public void SetInputs(float steer, float throttle)
        {
            steerInput = Mathf.Clamp(steer, -1f, 1f);
            throttleInput = Mathf.Clamp(throttle, -1f, 1f);
        }

        /// <summary>Stops the car and clears inputs. Call after teleporting it.</summary>
        public void ResetState()
        {
            steerInput = throttleInput = 0f;
            speed = steerAngle = 0f;
            if (rb.isKinematic) return;      // already held (showcase); kinematic bodies take no velocities
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        /// <summary>Moves the car to a pose (world space) and stops it.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            rb.position = position;
            rb.rotation = rotation;
            ResetState();
        }

        /// <summary>Showcase drive-in: a script moves the car (kinematic); the car's own dynamics are paused.</summary>
        public bool ExternallyControlled { get; private set; }

        public void SetExternalControl(bool on)
        {
            ExternallyControlled = on;
            if (!on) rb.isKinematic = false;
            ResetState();                    // zero velocities while the body is still dynamic
            if (on) rb.isKinematic = true;
        }

        /// <summary>Move the car while externally controlled (kinematic), e.g. along a scripted drive-in path.</summary>
        public void MoveExternally(Vector3 position, Quaternion rotation, float speedNow, float steerNow)
        {
            rb.MovePosition(position);
            rb.MoveRotation(rotation);
            speed = speedNow;
            steerAngle = steerNow;
            UpdateWheelVisuals();
        }

        void FixedUpdate()
        {
            if (ExternallyControlled) return;
            float dt = Time.fixedDeltaTime;

            // Re-read speed from physics so that bumping into things actually slows the car.
            speed = Vector3.Dot(rb.linearVelocity, transform.forward);

            // Steering has a slew rate so the policy can't flick the wheels instantly.
            steerAngle = Mathf.MoveTowards(steerAngle, steerInput * maxSteerAngle, steerRate * dt);

            // Longitudinal dynamics
            float deadzone = Mathf.Max(throttleDeadzone, 0.01f);
            if (Mathf.Abs(throttleInput) > deadzone)
            {
                // Rescale so throttle just outside the deadzone starts from ~0 (no jump at the edge)
                float throttle = Mathf.Sign(throttleInput) * (Mathf.Abs(throttleInput) - deadzone) / (1f - deadzone);
                bool braking = Mathf.Abs(speed) > 0.05f && Mathf.Sign(throttle) != Mathf.Sign(speed);
                float accel = braking ? brakeDeceleration : acceleration;
                speed += throttle * accel * dt;
            }
            else
            {
                // Foot off the pedal: brake firmly to a stop (v10), or coast if the deadzone is disabled
                float decel = throttleDeadzone > 0f ? autoBrakeDeceleration : coastDeceleration;
                speed = Mathf.MoveTowards(speed, 0f, decel * dt);
            }
            speed = Mathf.Clamp(speed, -maxReverseSpeed, maxForwardSpeed);

            // Bicycle model: yaw rate = v / L * tan(delta)
            float yawRate = speed / wheelBase * Mathf.Tan(steerAngle * Mathf.Deg2Rad); // rad/s

            Vector3 velocity = transform.forward * speed;
            velocity.y = rb.linearVelocity.y; // keep gravity
            rb.linearVelocity = velocity;
            rb.angularVelocity = new Vector3(0f, yawRate, 0f);

            UpdateWheelVisuals();
        }

        void UpdateWheelVisuals()
        {
            var rot = Quaternion.Euler(0f, steerAngle, 0f);
            if (frontLeftWheel) frontLeftWheel.localRotation = rot * flBaseRotation;
            if (frontRightWheel) frontRightWheel.localRotation = rot * frBaseRotation;
        }
    }
}
