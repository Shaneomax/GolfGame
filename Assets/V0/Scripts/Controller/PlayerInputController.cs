using UnityEngine;

namespace GolfGame.Controllers
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerInputController : MonoBehaviour
    {
        #region Settings

        [Header("Input Settings (Scaled 0-10)")]
        [Tooltip("Maximum screen drag amount. Dragging beyond this does nothing. (8 = 80% of screen height)")]
        public float MaxDragDistance = 8f;

        [Tooltip("The drag amount needed to hit the target marker perfectly. (5 = 50% of screen height)")]
        public float NormalDragDistance = 5f;

        [Tooltip("Minimum drag required to fire a shot. Drags below this are cancelled. (3 = 30% of screen height)")]
        public float MinDragToShoot = 3f;

        [Tooltip("Fixed loft angle (in degrees) to ensure a realistic golf shot arc (Parabola).")]
        public float DefaultLoftAngle = 30f;

        [Tooltip("Scale factor to reduce the overall launch force. Lower = softer shot.")]
        public float PowerScale = 0.3f;

        [Tooltip("Maximum sideways deviation (in degrees) applied to a shot when missed.")]
        public float MaxDeviationAngle = 22f;

        [Header("Visuals")]
        [Tooltip("Line Renderer used to show the elastic pull-back connection.")]
        public LineRenderer DragLineRenderer;
        public TrailRenderer BallTrail;

        [Tooltip("Multiplier for how far the visual line stretches in world space per drag unit.")]
        public float DragVisualMultiplier = 0.5f;

        [Header("Physics Modifiers")]
        public float GroundLinearDamping = 0.15f;
        public float GroundAngularDamping = 0.1f;
        public float MudAngularDrag = 8.0f;
        public float MudLinearDrag = 4.0f;

        [Tooltip("How much forward momentum is kept on subsequent Fairway bounces (0 = none, 1 = all). Adjust to get the right roll distance.")]
        [Range(0f, 1f)]
        public float FairwayForwardDamping = 0.9f;

        [Tooltip("Boost multiplier for the VERY FIRST bounce. Increase this if the first bounce isn't high enough.")]
        [Range(1f, 3f)]
        public float FirstBounceBoost = 1.2f;

        [Tooltip("How much forward speed is converted into upward bounce on the first hit (0 to 1). Kills forward momentum to make the bounce 'pop'.")]
        [Range(0f, 1f)]
        public float ForwardToBounceConversion = 0.35f;

        [Tooltip("How much bounce height is kept on subsequent Fairway bounces (0.5 = half of the previous bounce).")]
        [Range(0f, 1.5f)]
        public float FairwayBounceDamping = 0.5f;

        #endregion

        #region Data References

        [Header("Data References")]
        public BallData CurrentBall;
        public ClubData CurrentClub;
        public ShotAccuracyController AccuracyController;
        public GameObject TargetMarkerPrefab;
        public float MaxAimAngle = 45f;
        
        [Tooltip("The minimum allowed distance from the ball to the target marker.")]
        public float MinTargetDistance = 3f;

        [Header("Level References")]
        [Tooltip("Drag the hole/flag Transform here in the Inspector.")]
        public Transform FlagTransform;
        
        [Header("Arcade Roll Settings (Golf Clash Style)")]
        [Tooltip("How much horizontal speed is kept per physics frame when rolling. 0.995 = massive roll, 0.98 = short roll.")]
        [Range(0.90f, 0.999f)]
        public float RollPreservationFactor = 0.994f;
        // REMOVED: TopspinImpactBoost and wasGroundedLastFrame[cite: 4]

        #endregion

        #region Private Fields

        private Rigidbody rb;
        private Camera mainCamera;
        private Collider ballCollider;
        private TrajectoryPredictor trajectoryPredictor;
        
        private Vector3 dragStartPosition;
        private bool isDragging = false;
        private float flightStartTime = 0f;

        private int collisionCount = 0;
        private bool isGrounded = false;
        private bool isInMud = false;
        private string currentGroundTag = "Untagged";
        private int bounceCount = 0;
        private float lastBounceVelocityY = 0f;

        private Vector3 fixedAimDirection = Vector3.forward;
        public Vector3 FixedAimDirection => fixedAimDirection;

        private Vector3 initialAimDirection = Vector3.forward;
        private GameObject activeTargetMarker;
        private bool isDraggingTarget = false;
        
        [Header("Line Dynamic Colors")]
        public Color LowForceColor = Color.yellow;
        public Color NormalForceColor = Color.green;
        public Color ExtremeForceColor = Color.red;

        [Tooltip("At what overpower ratio (0.0 to 1.0) should the line turn Red?")]
        [Range(0.1f, 1.0f)]
        public float ExtremeForceThreshold = 0.5f;
        public bool IsDraggingTarget => isDraggingTarget;

        public GameObject ActiveTargetMarker => activeTargetMarker;

        #endregion

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ballCollider = GetComponent<Collider>();
            mainCamera = Camera.main;
            trajectoryPredictor = GetComponent<TrajectoryPredictor>();
            AccuracyController = FindFirstObjectByType<ShotAccuracyController>();
        }

        private void Start()
        {
            ApplyBallData();
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnStateEnter += OnStateEnter;
                GameStateManager.Instance.OnStateExit += OnStateExit;

                if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Setup)
                {
                    OnStateEnter(GameStateManager.GameState.Setup);
                }
            }
        }

        private void OnDestroy()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnStateEnter -= OnStateEnter;
                GameStateManager.Instance.OnStateExit -= OnStateExit;
            }
            if (activeTargetMarker != null) Destroy(activeTargetMarker);
        }

        private void OnStateEnter(GameStateManager.GameState newState)
        {
            if (newState == GameStateManager.GameState.Setup)
            {
                if (BallTrail != null) 
                {
                    BallTrail.emitting = false;
                    BallTrail.Clear(); 
                }
                
                SpawnTargetMarker();
                RepositionTargetMarker();
            }
            else if (newState == GameStateManager.GameState.Aiming)
            {
                if (trajectoryPredictor != null) trajectoryPredictor.HideTrajectory();
                if (activeTargetMarker != null) activeTargetMarker.SetActive(false);
                if (AccuracyController != null) AccuracyController.SetClub(CurrentClub);
                
                if (BallTrail != null) BallTrail.emitting = false;
            }
            else if (newState == GameStateManager.GameState.Flight)
            {
                flightStartTime = Time.time;
                bounceCount = 0; // Reset bounce count on new shot
                if (activeTargetMarker != null) activeTargetMarker.SetActive(false);
                
                if (BallTrail != null) 
                {
                    BallTrail.emitting = true;
                }
            }
        }

        private void OnStateExit(GameStateManager.GameState oldState)
        {
            if (oldState == GameStateManager.GameState.Setup)
                isDraggingTarget = false;
        }

        private float GetScaledDragMagnitude(Vector3 dragVector)
        {
            return (dragVector.magnitude / Screen.height) * 10f;
        }

        private float CalculateMaxRange()
        {
            float clubPower = CurrentClub != null ? CurrentClub.Power : 15f;
            float v = clubPower * PowerScale; 
            float g = Mathf.Abs(Physics.gravity.y);
            float theta = DefaultLoftAngle * Mathf.Deg2Rad;
            float range = (v * v * Mathf.Sin(2f * theta)) / g;
            return Mathf.Max(range, 1f);
        }

        private void SpawnTargetMarker()
        {
            if (TargetMarkerPrefab != null && activeTargetMarker == null)
            {
                float maxRange = CalculateMaxRange();
                float targetDistance = maxRange * 0.7f; 
                
                if (FlagTransform != null)
                {
                    Vector3 toFlag = FlagTransform.position - transform.position;
                    toFlag.y = 0f; 
                    
                    fixedAimDirection = toFlag.normalized;
                    
                    if (toFlag.magnitude < targetDistance)
                    {
                        targetDistance = toFlag.magnitude;
                    }
                }

                targetDistance = Mathf.Clamp(targetDistance, MinTargetDistance, maxRange);

                Vector3 spawnPos = transform.position + fixedAimDirection * targetDistance;
                spawnPos.y = 0f;
                activeTargetMarker = Instantiate(TargetMarkerPrefab, spawnPos, Quaternion.identity);
            }
            
            if (activeTargetMarker != null)
            {
                activeTargetMarker.SetActive(true);
                Vector3 diff = activeTargetMarker.transform.position - transform.position;
                fixedAimDirection = new Vector3(diff.x, 0f, diff.z).normalized;
                initialAimDirection = fixedAimDirection;
            }
        }

        private void RepositionTargetMarker()
        {
            if (activeTargetMarker != null)
            {
                float maxRange = CalculateMaxRange();
                float targetDistance = maxRange * 0.7f; 
                
                if (FlagTransform != null)
                {
                    Vector3 toFlag = FlagTransform.position - transform.position;
                    toFlag.y = 0f;
                    
                    fixedAimDirection = toFlag.normalized;
                    
                    if (toFlag.magnitude < targetDistance)
                    {
                        targetDistance = toFlag.magnitude;
                    }
                }

                targetDistance = Mathf.Clamp(targetDistance, MinTargetDistance, maxRange);

                Vector3 newPos = transform.position + fixedAimDirection * targetDistance;
                newPos.y = 0f;
                activeTargetMarker.transform.position = newPos;
                activeTargetMarker.SetActive(true);
                
                initialAimDirection = fixedAimDirection; 
            }
        }

        private void HandleSetupInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    if (activeTargetMarker != null && (hit.collider.gameObject == activeTargetMarker || hit.collider.transform.IsChildOf(activeTargetMarker.transform)))
                    {
                        isDraggingTarget = true;
                    }
                }
            }
            else if (Input.GetMouseButton(0) && isDraggingTarget && activeTargetMarker != null)
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    Vector3 diff = hitPoint - transform.position;
                    Vector3 horizontalDiff = new Vector3(diff.x, 0f, diff.z);
                    float maxRange = CalculateMaxRange();

                    Vector3 desiredDir = horizontalDiff.normalized;
                    if (desiredDir.sqrMagnitude > 0.001f)
                    {
                        float signedAngle = Vector3.SignedAngle(initialAimDirection, desiredDir, Vector3.up);
                        float clampedAngle = Mathf.Clamp(signedAngle, -MaxAimAngle, MaxAimAngle);

                        Vector3 testDir = Quaternion.AngleAxis(clampedAngle, Vector3.up) * initialAimDirection;
                        
                        float testDist = Mathf.Clamp(horizontalDiff.magnitude, MinTargetDistance, maxRange);
                        
                        Vector3 testHitPoint = transform.position + testDir * testDist;
                        testHitPoint.y = 0f;

                        Vector3 viewportPos = mainCamera.WorldToViewportPoint(testHitPoint);
                        if (viewportPos.x >= 0f && viewportPos.x <= 1f && viewportPos.y >= 0f && viewportPos.y <= 1f && viewportPos.z > 0f)
                        {
                            fixedAimDirection = testDir;
                            hitPoint = testHitPoint;
                            activeTargetMarker.transform.position = hitPoint;
                        }
                    }
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDraggingTarget = false;
            }
        }

        private Vector3 CalculateVelocityToHitTarget(Vector3 targetPos)
        {
            Vector3 diff = targetPos - transform.position;
            Vector3 horizontalDiff = new Vector3(diff.x, 0f, diff.z);
            float d = horizontalDiff.magnitude;
            float h = diff.y;
            float g = Mathf.Abs(Physics.gravity.y);
            float theta = DefaultLoftAngle * Mathf.Deg2Rad;

            float denominator = 2f * (d * Mathf.Tan(theta) - h);
            if (denominator <= 0.001f || d <= 0.001f) return horizontalDiff.normalized * 5f + Vector3.up * 2f;

            float speed = (d / Mathf.Cos(theta)) * Mathf.Sqrt(g / denominator);
            float maxClubPower = CurrentClub != null ? CurrentClub.Power : 15f;
            float maxSpeed = maxClubPower * PowerScale;
            speed = Mathf.Min(speed, maxSpeed);

            Vector3 flatDirection = horizontalDiff.normalized;
            Vector3 loftAxis = Vector3.Cross(flatDirection, Vector3.up);
            return Quaternion.AngleAxis(DefaultLoftAngle, loftAxis) * flatDirection * speed;
        }

        private void Update()
        {
            if (GameStateManager.Instance == null) return;

            if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Setup)
            {
                HandleSetupInput();
                if (trajectoryPredictor != null && activeTargetMarker != null)
                {
                    Vector3 launchVelocity = CalculateVelocityToHitTarget(activeTargetMarker.transform.position);
                    trajectoryPredictor.ShowTrajectory(transform.position, launchVelocity, activeTargetMarker.transform.position.y);
                }
            }
            else if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Aiming)
            {
                if (!isDragging && trajectoryPredictor != null) trajectoryPredictor.HideTrajectory();
            }
        }

        private void FixedUpdate()
        {
            if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Flight)
            {
                // 1. DYNAMIC MOMENTUM KILL: Apply heavy drag only when touching the Mud
                if (isInMud)
                {
                    // Rapidly kills forward speed and spin while in contact with the ground
                    rb.linearDamping = MudLinearDrag; 
                    rb.angularDamping = MudAngularDrag; 
                }
                else
                {
                    // Reset to air-resistance levels when it bounces back up
                    rb.linearDamping = 0f; 
                    rb.angularDamping = 0.05f; 
                }

                // 2. EXISTING ROLL LOGIC
                bool isPureRolling = isGrounded && Mathf.Abs(rb.linearVelocity.y) < 0.2f;

                if (isPureRolling)
                {
                    Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                    float currentSpeed = flatVel.magnitude;

                    if (currentSpeed > 0.01f)
                    {
                        // Dynamically adjust roll factor based on ground type
                        float dynamicRollFactor = RollPreservationFactor;
                        if (currentGroundTag == "Fairway" || currentGroundTag == "Untagged")
                        {
                            dynamicRollFactor = Mathf.Min(RollPreservationFactor, 0.985f); // Fairway has more friction
                        }
                        else if (currentGroundTag == "Rough")
                        {
                            dynamicRollFactor = Mathf.Min(RollPreservationFactor, 0.95f); // Rough stops quickly
                        }
                        else if (currentGroundTag == "Green")
                        {
                            dynamicRollFactor = Mathf.Max(RollPreservationFactor, 0.995f); // Green rolls very smoothly
                        }

                        float targetSpeed = currentSpeed * dynamicRollFactor;
                        
                        Vector3 newVelocity = flatVel.normalized * targetSpeed;
                        newVelocity.y = rb.linearVelocity.y; 
                        
                        rb.linearVelocity = newVelocity;
                    }
                }

                // 3. EXISTING STOP LOGIC
                if (Time.time > flightStartTime + 0.1f && isGrounded)
                {
                    if (CurrentBall != null && rb.linearVelocity.sqrMagnitude < (CurrentBall.StopThreshold * CurrentBall.StopThreshold))
                    {
                        StopBall();
                    }
                }
            }
        }

        public void ApplyBallData()
        {
            if (CurrentBall != null && rb != null)
            {
                rb.mass = CurrentBall.Mass;
                
                rb.linearDamping = 0f; 
                rb.angularDamping = 0.05f; 

                ApplyBounciness();
            }
        }

        private void ApplyBounciness()
        {
            if (ballCollider == null || CurrentBall == null) return;

            PhysicsMaterial bounceMat = new PhysicsMaterial("BallPhysics")
            {
                bounciness = CurrentBall.Bounciness, // Keep this dependent on your BallData
                dynamicFriction = 0.6f,              // Lowered for a smoother roll transition
                staticFriction = 0.6f,               // Lowered from 1.85f so it doesn't snag
                bounceCombine = PhysicsMaterialCombine.Average, // CHANGED: Dissipates bounce energy
                frictionCombine = PhysicsMaterialCombine.Multiply // CHANGED: More realistic friction blending
            };
            
            ballCollider.material = bounceMat;

            if (rb != null)
            {
                rb.maxAngularVelocity = 150f; 
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            currentGroundTag = collision.gameObject.tag;
            if (currentGroundTag == "Mud") isInMud = true;

            // Handle Ground Type Bounce Physics
            if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Flight)
            {
                bool isFairway = currentGroundTag == "Fairway" || currentGroundTag == "Untagged";
                
                if (isFairway)
                {
                    bounceCount++;
                    
                    if (bounceCount == 1)
                    {
                        StartCoroutine(ApplyFirstBouncePhysics());
                    }
                    else if (bounceCount == 2 || bounceCount == 3) 
                    {
                        // Explicitly half the bounce of the previous
                        StartCoroutine(ApplyExactBounceDamping());
                    }
                    else if (bounceCount > 3)
                    {
                        // Force roll after 3 bounces
                        StartCoroutine(KillBounce());
                    }
                }
            }

            collisionCount++;
            isGrounded = collisionCount > 0;
        }

        private System.Collections.IEnumerator ApplyFirstBouncePhysics()
        {
            yield return new WaitForFixedUpdate();
            if (rb != null)
            {
                Vector3 vel = rb.linearVelocity;
                
                // Calculate horizontal speed
                Vector3 horizontalVel = new Vector3(vel.x, 0, vel.z);
                float forwardSpeed = horizontalVel.magnitude;

                // Transfer a percentage of forward speed into upward bounce
                float transferredSpeed = forwardSpeed * ForwardToBounceConversion;

                // Reduce the forward momentum by the transferred amount
                float newForwardSpeed = Mathf.Max(0, forwardSpeed - transferredSpeed);
                horizontalVel = horizontalVel.normalized * newForwardSpeed;

                // Apply the boost and add the transferred kinetic energy to the vertical axis
                float newUpwardVelocity = (vel.y * FirstBounceBoost) + transferredSpeed;
                lastBounceVelocityY = newUpwardVelocity; // Store for the next bounce

                rb.linearVelocity = new Vector3(horizontalVel.x, newUpwardVelocity, horizontalVel.z);
            }
        }

        private System.Collections.IEnumerator ApplyExactBounceDamping()
        {
            yield return new WaitForFixedUpdate();
            if (rb != null)
            {
                Vector3 vel = rb.linearVelocity;
                
                // Exactly cut the bounce height in half based on the PREVIOUS bounce, ignoring unity physics material weirdness
                lastBounceVelocityY *= FairwayBounceDamping; 
                
                vel.x *= FairwayForwardDamping;
                vel.z *= FairwayForwardDamping;
                vel.y = lastBounceVelocityY;

                rb.linearVelocity = vel;
            }
        }

        private System.Collections.IEnumerator KillBounce()
        {
            yield return new WaitForFixedUpdate();
            if (rb != null)
            {
                Vector3 vel = rb.linearVelocity;
                vel.y = 0f; // Kill vertical bounce completely, forcing it to roll
                vel.x *= FairwayForwardDamping;
                vel.z *= FairwayForwardDamping;
                rb.linearVelocity = vel;
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision.gameObject.CompareTag("Mud")) isInMud = false;
            collisionCount = Mathf.Max(0, collisionCount - 1);
            isGrounded = collisionCount > 0;
        }

        private void StopBall()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep(); 
            
            if (BallTrail != null) BallTrail.emitting = false;
            
            // REMOVED: Resetting wasGroundedLastFrame[cite: 4]
            
            GameStateManager.Instance.ChangeState(GameStateManager.GameState.Setup);
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Flag"))
            {
                Debug.Log("[PlayerInput] Reached the flag! Ending the loop.");
                
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep();
                
                GameStateManager.Instance.ChangeState(GameStateManager.GameState.Resolution);
            }
        }

        private void OnMouseDown()
        {
            if (GameStateManager.Instance.CurrentState != GameStateManager.GameState.Aiming) return;
            isDragging = true;
            dragStartPosition = Input.mousePosition; 
            
            if (DragLineRenderer != null)
            {
                DragLineRenderer.enabled = true;
                DragLineRenderer.SetPosition(0, transform.position);
                DragLineRenderer.SetPosition(1, transform.position);
                
                UpdateDragLineColor(LowForceColor);
            }
        }

        private void OnMouseDrag()
        {
            if (!isDragging) return;
            Vector3 dragVector = dragStartPosition - Input.mousePosition;
            
            float dragMagnitude = Mathf.Clamp(GetScaledDragMagnitude(dragVector), 0f, MaxDragDistance);
            float overpowerRatio = Mathf.InverseLerp(NormalDragDistance, MaxDragDistance, dragMagnitude);

            if (AccuracyController != null)
            {
                AccuracyController.SetDragPowerMultiplier(overpowerRatio);
            }

            Color activeColor = LowForceColor; 

            if (dragMagnitude >= MinDragToShoot) 
            {
                if (overpowerRatio >= ExtremeForceThreshold)
                {
                    activeColor = ExtremeForceColor; 
                }
                else
                {
                    activeColor = NormalForceColor; 
                }
            }
            
            UpdateDragLineColor(activeColor);

            if (DragLineRenderer != null)
            {
                Vector3 visualPullBackDir = -FixedAimDirection;
                Vector3 endPoint = transform.position + (visualPullBackDir * (dragMagnitude * DragVisualMultiplier));
                endPoint.y = transform.position.y;
                
                DragLineRenderer.SetPosition(0, transform.position);
                DragLineRenderer.SetPosition(1, endPoint);
            }
        }

        private void OnMouseUp()
        {
            if (!isDragging) return;
            isDragging = false;

            if (DragLineRenderer != null)
            {
                DragLineRenderer.enabled = false;
            }

            Vector3 dragVector = dragStartPosition - Input.mousePosition;
            float dragMagnitude = GetScaledDragMagnitude(dragVector);

            if (dragMagnitude < MinDragToShoot)
            {
                if (AccuracyController != null)
                {
                    AccuracyController.SetDragPowerMultiplier(0f);
                    AccuracyController.ResetLock();
                }
                Debug.Log($"[PlayerInput] Shot cancelled — drag {dragMagnitude:F1} below minimum {MinDragToShoot}.");
                return;
            }

            if (trajectoryPredictor != null) trajectoryPredictor.HideTrajectory();
            if (AccuracyController != null) AccuracyController.LockAccuracy();

            if (activeTargetMarker == null) return;

            Vector3 launchVelocity = CalculateDeviatedShotVelocity(dragVector);

            if (launchVelocity.sqrMagnitude > 0.1f)
            {
                rb.WakeUp();
                rb.AddForce(launchVelocity, ForceMode.VelocityChange);
                GameStateManager.Instance.ChangeState(GameStateManager.GameState.Flight);
            }
            else
            {
                if (AccuracyController != null) AccuracyController.ResetLock();
            }
        }

        private void UpdateDragLineColor(Color baseColor)
        {
            if (DragLineRenderer == null) return;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(baseColor, 0.0f), new GradientColorKey(baseColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(150f / 255f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            DragLineRenderer.colorGradient = gradient;
        }

        private Vector3 CalculateDeviatedShotVelocity(Vector3 dragVector)
        {
            float dragMagnitude = Mathf.Clamp(GetScaledDragMagnitude(dragVector), 0f, MaxDragDistance);
            
            float powerRatio = dragMagnitude / NormalDragDistance;

            Vector3 toTarget = activeTargetMarker.transform.position - transform.position;
            Vector3 flatDirection = new Vector3(toTarget.x, 0f, toTarget.z).normalized;

            float distanceMultiplier = 1f;

            if (AccuracyController != null && AccuracyController.IsLocked)
            {
                float deviationAngle = AccuracyController.LockedAccuracyValue * AccuracyController.DeviationMultiplier;
                flatDirection = Quaternion.AngleAxis(deviationAngle, Vector3.up) * flatDirection;

                float accuracyAbs = Mathf.Abs(AccuracyController.LockedAccuracyValue);
                if (accuracyAbs < 0.05f) distanceMultiplier = 1.05f; 
                else distanceMultiplier = 1f - (accuracyAbs * 0.2f); 
            }

            Vector3 loftAxis  = Vector3.Cross(flatDirection, Vector3.up);
            Vector3 launchDir = Quaternion.AngleAxis(DefaultLoftAngle, loftAxis) * flatDirection;

            Vector3 preciseVelocity = CalculateVelocityToHitTarget(activeTargetMarker.transform.position);
            
            float speed = preciseVelocity.magnitude * powerRatio * distanceMultiplier;

            return launchDir * speed;
        }
    }
}