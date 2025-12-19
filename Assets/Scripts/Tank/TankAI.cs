using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Tanks.Complete
{
    /// <summary>
    /// AI controller for computer-controlled tanks. Handles pathfinding, target selection, and combat behavior.
    /// </summary>
    public class TankAI : MonoBehaviour
    {
        #region Types
        
        private enum AIState
        {
            Seeking,
            Fleeing
        }
        
        #endregion

        #region Configuration
        
        [Header("Pathfinding")]
        [SerializeField] private float m_PathfindInterval = 0.5f;
        
        [Header("Combat")]
        [SerializeField] private float m_TimeBetweenShots = 2f;
        [SerializeField] private float m_CloseDistanceThreshold = 3f;
        [SerializeField] private float m_FleeAfterCloseTime = 2f;
        [SerializeField] private float m_FleeAfterStationaryTime = 2f;
        [SerializeField] private float m_MinEngagementDistance = 8f;  // Stop moving when this close to target
        [SerializeField] private float m_MaxChargeTime = 1.5f;        // Force fire if charging too long
        [SerializeField] private float m_AlignmentTimeout = 0.8f;     // Fire anyway if can't align in time
        
        [Header("Flee Behavior")]
        [SerializeField] private float m_MinFleeDistance = 5f;
        [SerializeField] private float m_MaxFleeDistance = 20f;
        
        #endregion

        #region Private Fields
        
        // Components
        private TankMovement m_Movement;
        private TankShooting m_Shooting;
        private Rigidbody m_Rigidbody;
        private Transform m_Transform;
        
        // State
        private AIState m_CurrentState = AIState.Seeking;
        private bool m_IsMoving;
        
        // Target tracking
        private Transform m_CurrentTarget;
        private Vector3 m_LastTargetPosition;
        private float m_TimeSinceTargetMoved;
        private float m_TimeCloseToTarget;
        
        // Pathfinding
        private NavMeshPath m_CurrentPath;
        private NavMeshPath[] m_CachedPaths;
        private int m_CurrentCorner;
        private float m_PathfindTimer;
        private float m_ActualPathfindInterval;
        
        // Combat
        private float m_MaxShootingDistance;
        private float m_ShotCooldown;
        private float m_ChargeTimer;           // How long we've been charging
        private float m_AlignmentTimer;        // How long we've been trying to align
        private float m_MinEngagementDistanceSqr;
        
        // Flee tracking
        private Vector3 m_FleeLastPosition;
        private float m_TimeSinceFleeMove;
        
        // Tank references
        private GameObject[] m_AllTanks;
        
        // Constants (squared for optimization)
        private float m_CloseDistanceSqr;
        private const float CORNER_REACH_DISTANCE_SQR = 0.25f;
        private const float MOVEMENT_THRESHOLD_SQR = 0.000001f;
        private const float MIN_MAGNITUDE = 0.0001f;
        private const float SHOT_ALIGNMENT_THRESHOLD = 0.99f;
        private const float SHOT_DISTANCE_BUFFER = 2f;
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            if (!isActiveAndEnabled) return;
            
            CacheComponents();
            ConfigureAsComputerControlled();
            RandomizePathfindInterval();
            CalculateShootingRange();
            FindAllTanks();
            InitializePathCache();
        }

        private void Update()
        {
            UpdateCooldowns();
            
            switch (m_CurrentState)
            {
                case AIState.Seeking:
                    UpdateSeekState();
                    break;
                case AIState.Fleeing:
                    UpdateFleeState();
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (!HasValidPath()) return;
            
            Vector3 orientTarget = GetOrientationTarget();
            ApplyMovementAndRotation(orientTarget);
            CheckCornerReached(orientTarget);
        }
        
        #endregion

        #region Initialization
        
        private void CacheComponents()
        {
            m_Transform = transform;
            m_Movement = GetComponent<TankMovement>();
            m_Shooting = GetComponent<TankShooting>();
            m_Rigidbody = m_Movement.Rigidbody;
            
            m_CloseDistanceSqr = m_CloseDistanceThreshold * m_CloseDistanceThreshold;
            m_MinEngagementDistanceSqr = m_MinEngagementDistance * m_MinEngagementDistance;
        }

        private void ConfigureAsComputerControlled()
        {
            m_Movement.IsComputerControlled = true;
            m_Shooting.IsComputerControlled = true;
        }

        private void RandomizePathfindInterval()
        {
            m_ActualPathfindInterval = Random.Range(m_PathfindInterval * 0.6f, m_PathfindInterval * 1.2f);
        }

        private void CalculateShootingRange()
        {
            Vector3 maxRangePosition = m_Shooting.GetProjectilePosition(1f);
            m_MaxShootingDistance = Vector3.Distance(maxRangePosition, m_Transform.position);
        }

        private void FindAllTanks()
        {
            m_AllTanks = FindObjectsByType<TankMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Select(t => t.gameObject)
                .ToArray();
        }

        private void InitializePathCache()
        {
            int tankCount = Mathf.Max(m_AllTanks?.Length ?? 0, 8);
            m_CachedPaths = new NavMeshPath[tankCount];
            
            for (int i = 0; i < m_CachedPaths.Length; i++)
            {
                m_CachedPaths[i] = new NavMeshPath();
            }
            
            m_CurrentPath = new NavMeshPath();
        }
        
        #endregion

        #region Public Methods
        
        public void Setup(GameManager manager)
        {
            m_AllTanks = manager.m_SpawnPoints.Select(sp => sp.m_Instance).ToArray();
            InitializePathCache();
        }

        public void TurnOff()
        {
            enabled = false;
        }
        
        #endregion

        #region State Updates
        
        private void UpdateCooldowns()
        {
            if (m_ShotCooldown > 0f)
            {
                m_ShotCooldown -= Time.deltaTime;
            }
            
            // Track charging time
            if (m_Shooting.IsCharging)
            {
                m_ChargeTimer += Time.deltaTime;
            }
            else
            {
                m_ChargeTimer = 0f;
                m_AlignmentTimer = 0f;
            }
            
            m_PathfindTimer += Time.deltaTime;
        }

        private void UpdateSeekState()
        {
            if (m_PathfindTimer >= m_ActualPathfindInterval)
            {
                FindAndSetPath();
            }
            
            if (m_CurrentTarget != null)
            {
                UpdateTargetTracking();
                UpdateCombat();
            }
        }

        private void UpdateFleeState()
        {
            if (m_CurrentPath == null || m_CurrentCorner >= m_CurrentPath.corners.Length)
            {
                TransitionToSeek();
                return;
            }
            
            CheckFleeProgress();
        }
        
        #endregion

        #region Pathfinding
        
        private void FindAndSetPath()
        {
            m_PathfindTimer = 0f;
            
            float shortestLength = float.MaxValue;
            int bestPathIndex = -1;
            Transform bestTarget = null;
            Vector3 myPosition = m_Transform.position;
            
            for (int i = 0; i < m_AllTanks.Length; i++)
            {
                GameObject tank = m_AllTanks[i];
                
                if (!IsValidTarget(tank)) continue;
                
                m_CachedPaths[i].ClearCorners();
                
                if (NavMesh.CalculatePath(myPosition, tank.transform.position, NavMesh.AllAreas, m_CachedPaths[i]))
                {
                    float length = CalculatePathLength(m_CachedPaths[i]);
                    if (length < shortestLength)
                    {
                        shortestLength = length;
                        bestPathIndex = i;
                        bestTarget = tank.transform;
                    }
                }
            }
            
            if (bestPathIndex >= 0)
            {
                SetNewTarget(bestTarget, m_CachedPaths[bestPathIndex]);
            }
        }

        private bool IsValidTarget(GameObject tank)
        {
            return tank != null && 
                   tank.activeInHierarchy && 
                   tank != gameObject;
        }

        private void SetNewTarget(Transform target, NavMeshPath path)
        {
            if (target != m_CurrentTarget)
            {
                m_CurrentTarget = target;
                m_LastTargetPosition = target.position;
                m_TimeSinceTargetMoved = 0f;
                m_TimeCloseToTarget = 0f;
            }
            
            m_CurrentPath = path;
            m_CurrentCorner = 1;
            m_IsMoving = true;
        }

        private float CalculatePathLength(NavMeshPath path)
        {
            float length = 0f;
            Vector3[] corners = path.corners;
            
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }
            
            return length;
        }
        
        #endregion

        #region Target Tracking
        
        private void UpdateTargetTracking()
        {
            Vector3 targetPosition = m_CurrentTarget.position;
            float movementSqr = (targetPosition - m_LastTargetPosition).sqrMagnitude;
            
            if (movementSqr < MOVEMENT_THRESHOLD_SQR)
            {
                m_TimeSinceTargetMoved += Time.deltaTime;
            }
            else
            {
                m_TimeSinceTargetMoved = 0f;
            }
            
            m_LastTargetPosition = targetPosition;
            
            // Check if too close to target
            Vector3 toTarget = targetPosition - m_Transform.position;
            toTarget.y = 0f;
            
            if (toTarget.sqrMagnitude < m_CloseDistanceSqr)
            {
                m_TimeCloseToTarget += Time.deltaTime;
                
                if (m_TimeCloseToTarget > m_FleeAfterCloseTime)
                {
                    StartFleeing();
                }
            }
            else
            {
                m_TimeCloseToTarget = 0f;
            }
        }
        
        #endregion

        #region Combat
        
        private void UpdateCombat()
        {
            Vector3 toTarget = m_CurrentTarget.position - m_Transform.position;
            toTarget.y = 0f;
            
            float targetDistanceSqr = toTarget.sqrMagnitude;
            float targetDistance = Mathf.Sqrt(targetDistanceSqr);
            Vector3 directionToTarget = toTarget / targetDistance;
            float alignment = Vector3.Dot(directionToTarget, m_Transform.forward);
            
            // Stop moving when within engagement distance (don't ram the player)
            if (targetDistanceSqr < m_MinEngagementDistanceSqr)
            {
                m_IsMoving = false;
            }
            
            if (m_Shooting.IsCharging)
            {
                HandleCharging(targetDistance, alignment);
            }
            else
            {
                TryStartCharging(targetDistance);
            }
        }

        private void HandleCharging(float targetDistance, float alignment)
        {
            // Stop moving while charging to aim
            m_IsMoving = false;
            
            Vector3 currentShotTarget = m_Shooting.GetProjectilePosition(m_Shooting.CurrentChargeRatio);
            float shotDistance = Vector3.Distance(currentShotTarget, m_Transform.position);
            
            bool inRange = shotDistance >= targetDistance - SHOT_DISTANCE_BUFFER;
            bool aligned = alignment > SHOT_ALIGNMENT_THRESHOLD;
            
            // Track alignment time for timeout
            if (!aligned)
            {
                m_AlignmentTimer += Time.deltaTime;
            }
            else
            {
                m_AlignmentTimer = 0f;
            }
            
            // Fire conditions:
            // 1. Normal: in range and aligned
            // 2. Charge timeout: been charging too long (max charge time)
            // 3. Alignment timeout: can't align (player circling) - fire anyway with reduced accuracy
            bool shouldFire = (inRange && aligned) ||
                              (m_ChargeTimer >= m_MaxChargeTime) ||
                              (m_AlignmentTimer >= m_AlignmentTimeout && inRange);
            
            if (shouldFire)
            {
                m_Shooting.StopCharging();
                m_ShotCooldown = m_TimeBetweenShots;
                m_ChargeTimer = 0f;
                m_AlignmentTimer = 0f;
                
                if (m_TimeSinceTargetMoved > m_FleeAfterStationaryTime)
                {
                    StartFleeing();
                }
            }
        }

        private void TryStartCharging(float targetDistance)
        {
            if (targetDistance > m_MaxShootingDistance) return;
            if (m_ShotCooldown > 0f) return;
            
            // Check line of sight
            if (!NavMesh.Raycast(m_Transform.position, m_CurrentTarget.position, out _, NavMesh.AllAreas))
            {
                m_IsMoving = false;
                m_ChargeTimer = 0f;
                m_AlignmentTimer = 0f;
                m_Shooting.StartCharging();
            }
        }
        
        #endregion

        #region Flee Behavior
        
        private void StartFleeing()
        {
            if (m_CurrentTarget == null) return;
            
            Vector3 myPosition = m_Transform.position;
            m_FleeLastPosition = myPosition;
            m_TimeSinceFleeMove = 0f;
            
            Vector3 awayFromTarget = (myPosition - m_CurrentTarget.position).normalized;
            
            // Random angle between 90-180 degrees
            float randomAngle = Random.Range(90f, 180f) * (Random.value > 0.5f ? 1f : -1f);
            Vector3 fleeDirection = Quaternion.AngleAxis(randomAngle, Vector3.up) * awayFromTarget;
            float fleeDistance = Random.Range(m_MinFleeDistance, m_MaxFleeDistance);
            
            Vector3 fleeTarget = myPosition + fleeDirection * fleeDistance;
            
            if (NavMesh.CalculatePath(myPosition, fleeTarget, NavMesh.AllAreas, m_CachedPaths[0]))
            {
                m_CurrentPath = m_CachedPaths[0];
                m_CurrentState = AIState.Fleeing;
                m_CurrentCorner = 1;
                m_IsMoving = true;
            }
        }

        private void CheckFleeProgress()
        {
            Vector3 currentPosition = m_Transform.position;
            float movementSqr = (currentPosition - m_FleeLastPosition).sqrMagnitude;
            m_FleeLastPosition = currentPosition;
            
            if (movementSqr < MOVEMENT_THRESHOLD_SQR)
            {
                m_TimeSinceFleeMove += Time.deltaTime;
                
                if (m_TimeSinceFleeMove > m_FleeAfterStationaryTime)
                {
                    StartFleeing(); // Try a new flee direction
                }
            }
            else
            {
                m_TimeSinceFleeMove = 0f;
            }
        }

        private void TransitionToSeek()
        {
            m_CurrentState = AIState.Seeking;
            m_TimeCloseToTarget = 0f;
        }
        
        #endregion

        #region Movement
        
        private bool HasValidPath()
        {
            return m_CurrentPath != null && m_CurrentPath.corners.Length > 0;
        }

        private Vector3 GetOrientationTarget()
        {
            int cornerIndex = Mathf.Min(m_CurrentCorner, m_CurrentPath.corners.Length - 1);
            Vector3 target = m_CurrentPath.corners[cornerIndex];
            
            if (!m_IsMoving && m_CurrentTarget != null)
            {
                target = m_CurrentTarget.position;
            }
            
            return target;
        }

        private void ApplyMovementAndRotation(Vector3 orientTarget)
        {
            Vector3 toTarget = orientTarget - m_Transform.position;
            toTarget.y = 0f;
            
            float magnitude = toTarget.magnitude;
            if (magnitude < MIN_MAGNITUDE) return;
            
            Vector3 direction = toTarget / magnitude;
            Vector3 forward = m_Rigidbody.rotation * Vector3.forward;
            
            float alignment = Vector3.Dot(forward, direction);
            float angle = Vector3.SignedAngle(direction, forward, Vector3.up);
            
            // Apply movement
            if (m_IsMoving)
            {
                float speedFactor = Mathf.Clamp01(alignment);
                if (speedFactor > MIN_MAGNITUDE)
                {
                    m_Rigidbody.linearVelocity = speedFactor * m_Movement.Speed * forward + m_Movement.ExplosionForceValue;
                }
            }
            
            // Apply rotation
            float absAngle = Mathf.Abs(angle);
            if (absAngle > MIN_MAGNITUDE)
            {
                float maxRotation = m_Movement.TurnSpeed * Time.deltaTime;
                float rotation = Mathf.Sign(angle) * Mathf.Min(absAngle, maxRotation);
                m_Rigidbody.MoveRotation(m_Rigidbody.rotation * Quaternion.AngleAxis(-rotation, Vector3.up));
            }
        }

        private void CheckCornerReached(Vector3 orientTarget)
        {
            Vector3 toCorner = m_Rigidbody.position - orientTarget;
            toCorner.y = 0f;
            
            if (toCorner.sqrMagnitude < CORNER_REACH_DISTANCE_SQR)
            {
                m_CurrentCorner++;
            }
        }
        
        #endregion
    }
}
