using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// ML-Agents controller for a tank. Designed for fixed-map competitive play.
    /// </summary>
    public class TankAgent : Agent
    {
        [Header("Map Normalization")]
        [SerializeField] private Vector3 m_MapCenter = Vector3.zero;
        [SerializeField] private Vector2 m_MapExtents = new(25f, 25f);

        [Header("Rewards")]
        [SerializeField] private float m_WinReward = 3f;
        [SerializeField] private float m_LoseReward = -3f;
        [SerializeField] private float m_StepPenalty = -0.001f;
        [SerializeField] private float m_DistanceDeltaReward = 0.0025f;
        [SerializeField] private float m_AlignmentReward = 0.001f;
        [SerializeField] private float m_DamageDealtRewardScale = 0.01f;
        [SerializeField] private float m_DamageTakenPenaltyScale = 0.0125f;

        [Header("Inference Assist")]
        [SerializeField] private bool m_EnableAimAssist = true;
        [SerializeField, Range(0f, 1f)] private float m_AimAssistStrength = 0.65f;

        private TankMovement m_Movement;
        private TankShooting m_Shooting;
        private TankHealth m_Health;
        private Rigidbody m_Rigidbody;
        private Transform m_Transform;

        private GameObject[] m_AllTanks;
        private Transform m_CurrentTarget;
        private TankHealth m_CurrentTargetHealth;
        private Rigidbody m_CurrentTargetBody;
        private float m_LastTargetDistance;

        protected override void Awake()
        {
            base.Awake();

            m_Movement = GetComponent<TankMovement>();
            m_Shooting = GetComponent<TankShooting>();
            m_Health = GetComponent<TankHealth>();
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Transform = transform;

            m_Movement.IsComputerControlled = true;
            m_Shooting.IsComputerControlled = true;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (m_Health != null)
            {
                m_Health.Damaged += OnSelfDamaged;
                m_Health.Died += OnSelfDied;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (m_Health != null)
            {
                m_Health.Damaged -= OnSelfDamaged;
                m_Health.Died -= OnSelfDied;
            }

            UnbindTargetEvents();
        }

        public void Setup(GameManager manager)
        {
            m_AllTanks = manager.SpawnPoints
                .Where(sp => sp != null && sp.m_Instance != null)
                .Select(sp => sp.m_Instance)
                .ToArray();

            RefreshTarget();
        }

        public override void OnEpisodeBegin()
        {
            RefreshTarget();
            m_LastTargetDistance = GetFlatDistanceToTarget();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            RefreshTargetIfNeeded();

            Vector3 localPosition = m_Transform.position - m_MapCenter;
            sensor.AddObservation(NormalizeSigned(localPosition.x, m_MapExtents.x));
            sensor.AddObservation(NormalizeSigned(localPosition.z, m_MapExtents.y));

            Vector3 forward = m_Transform.forward;
            sensor.AddObservation(forward.x);
            sensor.AddObservation(forward.z);

            Vector3 velocity = m_Rigidbody.linearVelocity;
            sensor.AddObservation(NormalizeSigned(velocity.x, m_Movement.Speed));
            sensor.AddObservation(NormalizeSigned(velocity.z, m_Movement.Speed));

            sensor.AddObservation(m_Health != null ? m_Health.HealthRatio : 0f);
            sensor.AddObservation(m_Shooting.IsCharging ? 1f : 0f);
            sensor.AddObservation(m_Shooting.CurrentChargeRatio);

            if (m_CurrentTarget != null)
            {
                Vector3 toTarget = m_CurrentTarget.position - m_Transform.position;
                Vector3 targetForward = m_CurrentTarget.forward;
                Vector3 targetVelocity = m_CurrentTargetBody != null ? m_CurrentTargetBody.linearVelocity : Vector3.zero;

                sensor.AddObservation(NormalizeSigned(toTarget.x, m_MapExtents.x));
                sensor.AddObservation(NormalizeSigned(toTarget.z, m_MapExtents.y));
                sensor.AddObservation(targetForward.x);
                sensor.AddObservation(targetForward.z);
                sensor.AddObservation(NormalizeSigned(targetVelocity.x, m_Movement.Speed));
                sensor.AddObservation(NormalizeSigned(targetVelocity.z, m_Movement.Speed));

                Vector3 toTargetFlat = toTarget;
                toTargetFlat.y = 0f;
                float distance = toTargetFlat.magnitude;
                Vector3 direction = distance > 0.001f ? toTargetFlat / distance : m_Transform.forward;
                float alignment = Vector3.Dot(m_Transform.forward, direction);

                sensor.AddObservation(Normalize01(distance, Mathf.Max(m_MapExtents.x, m_MapExtents.y) * 2f));
                sensor.AddObservation(alignment);
                sensor.AddObservation(HasLineOfSight() ? 1f : 0f);
                sensor.AddObservation(m_CurrentTargetHealth != null ? m_CurrentTargetHealth.HealthRatio : 0f);
            }
            else
            {
                for (int i = 0; i < 10; i++)
                {
                    sensor.AddObservation(0f);
                }
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            RefreshTargetIfNeeded();

            float moveInput = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
            float turnInput = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
            float fireControl = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);

            ApplyMovement(moveInput, turnInput);
            HandleShooting(fireControl);
            AddShapingRewards();
            CheckTerminalConditions();
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuous = actionsOut.ContinuousActions;
            continuous[0] = Input.GetAxisRaw("Vertical");
            continuous[1] = Input.GetAxisRaw("Horizontal");
            continuous[2] = Input.GetKey(KeyCode.Space) ? 1f : -1f;
        }

        private void ApplyMovement(float moveInput, float turnInput)
        {
            Vector3 forward = m_Rigidbody.rotation * Vector3.forward;
            m_Rigidbody.linearVelocity = forward * (moveInput * m_Movement.Speed) + m_Movement.ExplosionForceValue;

            float assistedTurn = turnInput;
            if (m_EnableAimAssist && m_CurrentTarget != null)
            {
                Vector3 toTarget = m_CurrentTarget.position - m_Transform.position;
                toTarget.y = 0f;

                if (toTarget.sqrMagnitude > 0.001f)
                {
                    float signedAngle = Vector3.SignedAngle(m_Transform.forward, toTarget.normalized, Vector3.up);
                    float targetTurn = Mathf.Clamp(signedAngle / 45f, -1f, 1f);
                    assistedTurn = Mathf.Lerp(turnInput, targetTurn, m_AimAssistStrength);
                }
            }

            float turnAmount = assistedTurn * m_Movement.TurnSpeed * Time.fixedDeltaTime;
            m_Rigidbody.MoveRotation(m_Rigidbody.rotation * Quaternion.Euler(0f, turnAmount, 0f));
        }

        private void HandleShooting(float fireControl)
        {
            if (fireControl > 0.2f)
            {
                if (!m_Shooting.IsCharging)
                {
                    m_Shooting.StartCharging();
                }
            }
            else if (m_Shooting.IsCharging)
            {
                m_Shooting.StopCharging();
            }
        }

        private void AddShapingRewards()
        {
            AddReward(m_StepPenalty);

            if (m_CurrentTarget == null)
            {
                return;
            }

            float currentDistance = GetFlatDistanceToTarget();
            float delta = m_LastTargetDistance - currentDistance;
            AddReward(delta * m_DistanceDeltaReward);
            m_LastTargetDistance = currentDistance;

            Vector3 toTarget = m_CurrentTarget.position - m_Transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                float alignment = Mathf.Max(0f, Vector3.Dot(m_Transform.forward, toTarget.normalized));
                AddReward(alignment * m_AlignmentReward);
            }
        }

        private void CheckTerminalConditions()
        {
            if (m_CurrentTarget == null || !m_CurrentTarget.gameObject.activeInHierarchy ||
                (m_CurrentTargetHealth != null && m_CurrentTargetHealth.IsDead))
            {
                AddReward(m_WinReward);
                EndEpisode();
                return;
            }

            if (!gameObject.activeInHierarchy || (m_Health != null && m_Health.IsDead) || m_Transform.position.y < -2f)
            {
                AddReward(m_LoseReward);
                EndEpisode();
            }
        }

        private void OnSelfDamaged(float damage, float _)
        {
            AddReward(-damage * m_DamageTakenPenaltyScale);
        }

        private void OnSelfDied()
        {
            AddReward(m_LoseReward);
            EndEpisode();
        }

        private void OnTargetDamaged(float damage, float _)
        {
            AddReward(damage * m_DamageDealtRewardScale);
        }

        private void OnTargetDied()
        {
            AddReward(m_WinReward);
            EndEpisode();
        }

        private void RefreshTargetIfNeeded()
        {
            if (m_CurrentTarget == null || !m_CurrentTarget.gameObject.activeInHierarchy)
            {
                RefreshTarget();
            }
        }

        private void RefreshTarget()
        {
            UnbindTargetEvents();

            if (m_AllTanks == null || m_AllTanks.Length == 0)
            {
                m_AllTanks = FindObjectsByType<TankMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Select(t => t.gameObject)
                    .ToArray();
            }

            float bestDistance = float.MaxValue;
            Transform bestTarget = null;

            for (int i = 0; i < m_AllTanks.Length; i++)
            {
                GameObject candidate = m_AllTanks[i];
                if (candidate == null || candidate == gameObject || !candidate.activeInHierarchy)
                {
                    continue;
                }

                float distance = (candidate.transform.position - m_Transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTarget = candidate.transform;
                }
            }

            m_CurrentTarget = bestTarget;
            if (m_CurrentTarget != null)
            {
                m_CurrentTargetBody = m_CurrentTarget.GetComponent<Rigidbody>();
                m_CurrentTargetHealth = m_CurrentTarget.GetComponent<TankHealth>();

                if (m_CurrentTargetHealth != null)
                {
                    m_CurrentTargetHealth.Damaged += OnTargetDamaged;
                    m_CurrentTargetHealth.Died += OnTargetDied;
                }
            }
            else
            {
                m_CurrentTargetBody = null;
                m_CurrentTargetHealth = null;
            }

            m_LastTargetDistance = GetFlatDistanceToTarget();
        }

        private void UnbindTargetEvents()
        {
            if (m_CurrentTargetHealth == null)
            {
                return;
            }

            m_CurrentTargetHealth.Damaged -= OnTargetDamaged;
            m_CurrentTargetHealth.Died -= OnTargetDied;
        }

        private float GetFlatDistanceToTarget()
        {
            if (m_CurrentTarget == null)
            {
                return 0f;
            }

            Vector3 delta = m_CurrentTarget.position - m_Transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        private bool HasLineOfSight()
        {
            if (m_CurrentTarget == null)
            {
                return false;
            }

            Vector3 origin = m_Transform.position + Vector3.up * 0.6f;
            Vector3 target = m_CurrentTarget.position + Vector3.up * 0.6f;
            Vector3 direction = target - origin;

            if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, direction.magnitude + 0.1f))
            {
                return hit.transform == m_CurrentTarget || hit.transform.IsChildOf(m_CurrentTarget);
            }

            return false;
        }

        private static float NormalizeSigned(float value, float maxAbs)
        {
            if (maxAbs <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Clamp(value / maxAbs, -1f, 1f);
        }

        private static float Normalize01(float value, float max)
        {
            if (max <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Clamp01(value / max);
        }
    }
}
