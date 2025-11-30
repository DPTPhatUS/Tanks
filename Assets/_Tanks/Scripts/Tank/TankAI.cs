using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;


namespace Tanks.Complete
{
    public class TankAI : MonoBehaviour
    {
        enum State
        {
            Seek,
            Flee
        }
    
        private TankMovement m_Movement;
        private TankShooting m_Shooting;
        
        private float m_PathfindTime = 0.5f;
        private float m_PathfindTimer = 0.0f;

        private Transform m_CurrentTarget = null;
        private float m_MaxShootingDistance = 0.0f;

        private float m_TimeBetweenShot = 2.0f;
        private float m_ShotCooldown = 0.0f;

        private Vector3 m_LastTargetPosition;
        private float m_TimeSinceLastTargetMove;
        private float m_TimeCloseToTarget;

        private Vector3 m_FleeingLastPosition;
        private float m_SinceLastFleeingMove = 0.0f;
        
        private NavMeshPath m_CurrentPath = null;
        private int m_CurrentCorner = 0;
        private bool m_IsMoving = false;

        private GameObject[] m_AllTanks;
        private NavMeshPath[] m_CachedPaths;  // Reusable path array to avoid allocations
        private Transform m_Transform;         // Cached transform reference

        private State m_CurrentState = State.Seek;
        
        // Squared distance thresholds for optimization (avoid sqrt)
        private const float CLOSE_DISTANCE_SQR = 9.0f;      // 3.0f squared
        private const float CORNER_REACH_SQR = 0.25f;       // 0.5f squared
        private const float MOVEMENT_THRESHOLD_SQR = 0.000001f;

        private void Awake()
        {
            if(!isActiveAndEnabled)
                return;
            
            m_Transform = transform;  // Cache transform reference
            
            m_Movement = GetComponent<TankMovement>();
            m_Shooting = GetComponent<TankShooting>();

            m_Movement.m_IsComputerControlled = true;
            m_Shooting.m_IsComputerControlled = true;
            
            m_PathfindTime = Random.Range(0.3f, 0.6f);
            
            m_MaxShootingDistance = Vector3.Distance(m_Shooting.GetProjectilePosition(1.0f), m_Transform.position);
            
            m_AllTanks = FindObjectsByType<TankMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Select(e => e.gameObject).ToArray();
            InitializePathCache();
        }
        
        private void InitializePathCache()
        {
            // Pre-allocate path array to avoid GC allocations during gameplay
            int maxTanks = m_AllTanks != null ? m_AllTanks.Length : 8;
            m_CachedPaths = new NavMeshPath[maxTanks];
            for (int i = 0; i < m_CachedPaths.Length; i++)
            {
                m_CachedPaths[i] = new NavMeshPath();
            }
        }

        public void Setup(GameManager manager)
        {
            m_AllTanks = manager.m_SpawnPoints.Select(e => e.m_Instance).ToArray();
            InitializePathCache();
        }

        public void TurnOff()
        {
            enabled = false;
        }

        void Update()
        {
            if(m_ShotCooldown > 0)
                m_ShotCooldown -= Time.deltaTime;
            
            m_PathfindTimer += Time.deltaTime;

            switch (m_CurrentState)
            {
                case State.Seek:
                    SeekUpdate();
                    break;
                case State.Flee:
                    FleeUpdate();
                    break;
            }
        }

        void SeekUpdate()
        {
            if (m_PathfindTimer > m_PathfindTime)
            {
                m_PathfindTimer = 0;
                
                float shortestPath = float.MaxValue;
                int usedPath = -1;
                Transform target = null;
                Vector3 myPosition = m_Transform.position;
                
                for (int i = 0; i < m_AllTanks.Length; i++)
                {
                    var tankObj = m_AllTanks[i];

                    if (tankObj == null || !tankObj.activeInHierarchy || tankObj == gameObject)
                        continue;

                    // Reuse cached path instead of allocating new one
                    m_CachedPaths[i].ClearCorners();

                    if (NavMesh.CalculatePath(myPosition, tankObj.transform.position, ~0, m_CachedPaths[i]))
                    {
                        float length = GetPathLength(m_CachedPaths[i]);
                        if (length < shortestPath)
                        {
                            usedPath = i;
                            shortestPath = length;
                            target = tankObj.transform;
                        }
                    }
                }

                if (usedPath != -1)
                {
                    if (target != m_CurrentTarget)
                    {
                        m_CurrentTarget = target;
                        m_LastTargetPosition = m_CurrentTarget.position;
                    }

                    m_CurrentPath = m_CachedPaths[usedPath];
                    m_CurrentCorner = 1;
                    m_IsMoving = true;
                }
            }

            if (m_CurrentTarget != null)
            {
                Vector3 targetPos = m_CurrentTarget.position;
                float targetMovementSqr = (targetPos - m_LastTargetPosition).sqrMagnitude;

                if (targetMovementSqr < MOVEMENT_THRESHOLD_SQR)
                {
                    m_TimeSinceLastTargetMove += Time.deltaTime;
                }
                else
                {
                    m_TimeSinceLastTargetMove = 0;
                }

                m_LastTargetPosition = targetPos;
                
                Vector3 toTarget = targetPos - m_Transform.position;
                toTarget.y = 0;
                
                float targetDistanceSqr = toTarget.sqrMagnitude;
                float targetDistance = Mathf.Sqrt(targetDistanceSqr);  // Only calculate sqrt when needed
                toTarget /= targetDistance;  // Normalize using already calculated magnitude

                if (targetDistanceSqr < CLOSE_DISTANCE_SQR)
                {
                    m_TimeCloseToTarget += Time.deltaTime;

                    if (m_TimeCloseToTarget > 2.0f)
                    {
                        StartFleeing();
                        return;
                    }
                }
                else
                {
                    m_TimeCloseToTarget = 0.0f;
                }
                
                float dotToTarget = Vector3.Dot(toTarget, transform.forward);
                
                if (m_Shooting.IsCharging)
                {
                    Vector3 currentShotTarget = m_Shooting.GetProjectilePosition(m_Shooting.CurrentChargeRatio);
                    float currentShotDistance = Vector3.Distance(currentShotTarget, transform.position);

                    if (currentShotDistance >= targetDistance - 2 && dotToTarget > 0.99f)
                    {
                        m_IsMoving = false;
                        m_Shooting.StopCharging();
                        
                        m_ShotCooldown = m_TimeBetweenShot;
                        
                        if (m_TimeSinceLastTargetMove > 2.0f)
                        {
                            StartFleeing();
                        }
                    }
                }
                else
                {
                    if (targetDistance < m_MaxShootingDistance)
                    {
                        if (!NavMesh.Raycast(m_Transform.position, m_CurrentTarget.position, out var hit, ~0))
                        {
                            m_IsMoving = false;

                            if (m_ShotCooldown <= 0.0f)
                            {
                                m_Shooting.StartCharging();
                            }
                        }
                    }
                }
            }
        }

        private void FleeUpdate()
        {
            if(m_CurrentCorner >= m_CurrentPath.corners.Length)
                m_CurrentState = State.Seek;
            
            Vector3 currentPos = m_Transform.position;
            float distanceSqr = (currentPos - m_FleeingLastPosition).sqrMagnitude;
            m_FleeingLastPosition = currentPos;

            if (distanceSqr < MOVEMENT_THRESHOLD_SQR)
            {
                m_SinceLastFleeingMove += Time.deltaTime;
            }
            else
            {
                m_SinceLastFleeingMove = 0;
            }

            if (m_SinceLastFleeingMove > 2.0f)
            {
                StartFleeing();
            }
        }

        private void StartFleeing()
        {
            Vector3 myPos = m_Transform.position;
            m_FleeingLastPosition = myPos;
            m_SinceLastFleeingMove = 0.0f;
            
            var toTarget = (m_CurrentTarget.position - myPos).normalized;
            
            // Random angle between 90-180 degrees in random direction
            float randomAngle = Random.Range(90.0f, 180.0f) * (Random.value > 0.5f ? 1f : -1f);
            toTarget = Quaternion.AngleAxis(randomAngle, Vector3.up) * toTarget;
            toTarget *= Random.Range(5.0f, 20.0f);

            // Reuse first cached path for flee calculation
            if (NavMesh.CalculatePath(myPos, myPos + toTarget, NavMesh.AllAreas, m_CachedPaths[0]))
            {
                m_CurrentPath = m_CachedPaths[0];
                m_CurrentState = State.Flee;
                m_CurrentCorner = 1;
                m_IsMoving = true;
            }
        }

        private void FixedUpdate()
        {
            if(m_CurrentPath == null || m_CurrentPath.corners.Length == 0)
                return;
            
            var rb = m_Movement.Rigidbody;
            int cornerIndex = Mathf.Min(m_CurrentCorner, m_CurrentPath.corners.Length - 1);
            Vector3 orientTarget = m_CurrentPath.corners[cornerIndex];

            if (!m_IsMoving && m_CurrentTarget != null)
                orientTarget = m_CurrentTarget.position;

            Vector3 myPos = m_Transform.position;
            Vector3 toOrientTarget = orientTarget - myPos;
            toOrientTarget.y = 0;
            
            float toTargetMagnitude = toOrientTarget.magnitude;
            if (toTargetMagnitude > 0.0001f)
                toOrientTarget /= toTargetMagnitude;  // Normalize

            Vector3 forward = rb.rotation * Vector3.forward;

            float orientDot = Vector3.Dot(forward, toOrientTarget);
            float rotatingAngle = Vector3.SignedAngle(toOrientTarget, forward, Vector3.up);

            if (m_IsMoving)
            {
                float clampedDot = Mathf.Clamp01(orientDot);
                if (clampedDot > 0.0001f)
                {
                    rb.linearVelocity = clampedDot * m_Movement.m_Speed * forward + m_Movement.ExplosionForceValue;
                }
            }

            float absAngle = Mathf.Abs(rotatingAngle);
            if (absAngle > 0.0001f)
            {
                float clampedAngle = Mathf.Sign(rotatingAngle) * Mathf.Min(absAngle, m_Movement.m_TurnSpeed * Time.deltaTime);
                rb.MoveRotation(rb.rotation * Quaternion.AngleAxis(-clampedAngle, Vector3.up));
            }

            // Use sqrMagnitude for corner reach check
            Vector3 toCorner = rb.position - orientTarget;
            toCorner.y = 0;
            if (toCorner.sqrMagnitude < CORNER_REACH_SQR)
            {
                m_CurrentCorner += 1;
            }
        }

        float GetPathLength(NavMeshPath path)
        {
            float dist = 0;
            for (var i = 1; i < path.corners.Length; ++i)
            {
                dist += Vector3.Distance(path.corners[i-1], path.corners[i]);
            }

            return dist;
        }
    }
}