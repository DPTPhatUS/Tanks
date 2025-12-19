using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace Tanks.Complete
{
    /// <summary>
    /// Handles tank movement including player input, direct control mode, and physics-based movement.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class TankMovement : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Player Settings")]
        [Tooltip("The player number. Player 1 uses left keyboard, Player 2 uses right keyboard")]
        public int m_PlayerNumber = 1;
        
        [Tooltip("Is this tank controlled by AI instead of a player")]
        public bool m_IsComputerControlled;
        
        [Header("Movement Settings")]
        [Tooltip("Movement speed in units per second")]
        public float m_Speed = 12f;
        
        [Tooltip("Rotation speed in degrees per second")]
        public float m_TurnSpeed = 180f;
        
        [Tooltip("Use direct control (move toward input direction) instead of tank controls")]
        public bool m_IsDirectControl;
        
        [Header("Audio")]
        public AudioSource m_MovementAudio;
        public AudioClip m_EngineIdling;
        public AudioClip m_EngineDriving;
        public float m_PitchRange = 0.2f;
        
        [HideInInspector]
        public TankInputUser m_InputUser;
        
        #endregion

        #region Public Properties
        
        public float Speed => m_Speed;
        public float TurnSpeed => m_TurnSpeed;
        public Rigidbody Rigidbody => m_Rigidbody;
        public Vector3 ExplosionForceValue => m_ExplosionForce;
        public int ControlIndex { get; set; } = -1;
        
        public bool IsComputerControlled
        {
            get => m_IsComputerControlled;
            set => m_IsComputerControlled = value;
        }
        
        #endregion

        #region Private Fields
        
        // Components
        private Rigidbody m_Rigidbody;
        private ParticleSystem[] m_ParticleSystems;
        
        // Input
        private InputAction m_MoveAction;
        private InputAction m_TurnAction;
        private float m_MovementInput;
        private float m_TurnInput;
        
        // Movement state
        private Vector3 m_RequestedDirection;
        private Vector3 m_ExplosionForce;
        private float m_OriginalPitch;
        
        // Cached references
        private Transform m_CachedCameraTransform;
        private Transform m_Transform;
        private bool m_UseDirectControl;
        
        // Constants
        private const float INPUT_DEADZONE = 0.1f;
        private const float EXPLOSION_DECAY_RATE = 3f;
        private const float DIRECTION_THRESHOLD = 0.0001f;
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            InitializePhysics();
            InitializeParticleSystems();
        }

        private void OnDisable()
        {
            m_Rigidbody.isKinematic = true;
            StopParticleSystems();
        }

        private void Start()
        {
            EnsureAIComponent();
            SetupControlIndex();
            SetupInputScheme();
            SetupInputActions();
            CacheDirectControlState();
            CacheAudioPitch();
        }

        private void Update()
        {
            if (!m_IsComputerControlled)
            {
                ReadInput();
            }
            
            UpdateEngineAudio();
        }

        private void FixedUpdate()
        {
            if (m_UseDirectControl)
            {
                CalculateDirectControlDirection();
            }
            
            ApplyMovement();
            ApplyRotation();
        }
        
        #endregion

        #region Initialization
        
        private void CacheComponents()
        {
            m_Transform = transform;
            m_Rigidbody = GetComponent<Rigidbody>();
            m_InputUser = GetComponent<TankInputUser>();
            
            if (m_InputUser == null)
            {
                m_InputUser = gameObject.AddComponent<TankInputUser>();
            }
        }

        private void InitializePhysics()
        {
            m_Rigidbody.isKinematic = false;
            m_MovementInput = 0f;
            m_TurnInput = 0f;
            m_ExplosionForce = Vector3.zero;
        }

        private void InitializeParticleSystems()
        {
            m_ParticleSystems = GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in m_ParticleSystems)
            {
                ps.Play();
            }
        }

        private void StopParticleSystems()
        {
            if (m_ParticleSystems == null) return;
            
            foreach (var ps in m_ParticleSystems)
            {
                ps.Stop();
            }
        }

        private void EnsureAIComponent()
        {
            if (m_IsComputerControlled && GetComponent<TankAI>() == null)
            {
                gameObject.AddComponent<TankAI>();
            }
        }

        private void SetupControlIndex()
        {
            if (ControlIndex == -1 && !m_IsComputerControlled)
            {
                ControlIndex = m_PlayerNumber;
            }
        }

        private void SetupInputScheme()
        {
            var mobileControl = FindAnyObjectByType<MobileUIControl>();
            
            if (mobileControl != null && ControlIndex == 1)
            {
                m_InputUser.SetNewInputUser(InputUser.PerformPairingWithDevice(mobileControl.Device));
                m_InputUser.ActivateScheme("Gamepad");
            }
            else
            {
                string scheme = ControlIndex == 1 ? "KeyboardLeft" : "KeyboardRight";
                m_InputUser.ActivateScheme(scheme);
            }
        }

        private void SetupInputActions()
        {
            m_MoveAction = m_InputUser.ActionAsset.FindAction("Vertical");
            m_TurnAction = m_InputUser.ActionAsset.FindAction("Horizontal");
            
            m_MoveAction?.Enable();
            m_TurnAction?.Enable();
        }

        private void CacheDirectControlState()
        {
            if (Camera.main != null)
            {
                m_CachedCameraTransform = Camera.main.transform;
            }
            
            var scheme = m_InputUser.InputUser.controlScheme;
            bool isGamepad = scheme.HasValue && scheme.Value.name == "Gamepad";
            m_UseDirectControl = m_IsDirectControl || isGamepad;
        }

        private void CacheAudioPitch()
        {
            if (m_MovementAudio != null)
            {
                m_OriginalPitch = m_MovementAudio.pitch;
            }
        }
        
        #endregion

        #region Input Handling
        
        private void ReadInput()
        {
            m_MovementInput = m_MoveAction?.ReadValue<float>() ?? 0f;
            m_TurnInput = m_TurnAction?.ReadValue<float>() ?? 0f;
        }

        private bool HasMovementInput()
        {
            return Mathf.Abs(m_MovementInput) >= INPUT_DEADZONE || 
                   Mathf.Abs(m_TurnInput) >= INPUT_DEADZONE;
        }
        
        #endregion

        #region Movement
        
        private void CalculateDirectControlDirection()
        {
            if (m_CachedCameraTransform == null) return;
            
            Vector3 camForward = m_CachedCameraTransform.forward;
            camForward.y = 0;
            
            if (camForward.sqrMagnitude < DIRECTION_THRESHOLD)
            {
                camForward = m_CachedCameraTransform.up;
                camForward.y = 0;
            }
            
            camForward.Normalize();
            Vector3 camRight = Vector3.Cross(Vector3.up, camForward);
            
            m_RequestedDirection = camForward * m_MovementInput + camRight * m_TurnInput;
            m_RequestedDirection.Normalize();
        }

        private void ApplyMovement()
        {
            float speedInput = CalculateSpeedInput();
            Vector3 movement = m_Transform.forward * speedInput * m_Speed;
            
            m_Rigidbody.linearVelocity = movement + m_ExplosionForce;
            DecayExplosionForce();
        }

        private float CalculateSpeedInput()
        {
            if (!m_UseDirectControl)
            {
                return m_MovementInput;
            }
            
            float baseSpeed = m_RequestedDirection.magnitude;
            float angleToTarget = Vector3.Angle(m_RequestedDirection, m_Transform.forward);
            float angleModifier = 1f - Mathf.Clamp01((angleToTarget - 90f) / 90f);
            
            return baseSpeed * angleModifier;
        }

        private void ApplyRotation()
        {
            Quaternion rotation = CalculateTurnRotation();
            m_Rigidbody.MoveRotation(m_Rigidbody.rotation * rotation);
        }

        private Quaternion CalculateTurnRotation()
        {
            if (m_UseDirectControl)
            {
                float angleToTarget = Vector3.SignedAngle(m_RequestedDirection, m_Transform.forward, Vector3.up);
                float maxRotation = m_TurnSpeed * Time.deltaTime;
                float rotation = Mathf.Sign(angleToTarget) * Mathf.Min(Mathf.Abs(angleToTarget), maxRotation);
                
                return Quaternion.AngleAxis(-rotation, Vector3.up);
            }
            
            float turn = m_TurnInput * m_TurnSpeed * Time.deltaTime;
            return Quaternion.Euler(0f, turn, 0f);
        }

        private void DecayExplosionForce()
        {
            m_ExplosionForce = Vector3.Lerp(m_ExplosionForce, Vector3.zero, Time.deltaTime * EXPLOSION_DECAY_RATE);
        }
        
        #endregion

        #region Audio
        
        private void UpdateEngineAudio()
        {
            if (m_MovementAudio == null) return;
            
            AudioClip targetClip = HasMovementInput() ? m_EngineDriving : m_EngineIdling;
            
            if (m_MovementAudio.clip != targetClip)
            {
                PlayEngineClip(targetClip);
            }
        }

        private void PlayEngineClip(AudioClip clip)
        {
            m_MovementAudio.clip = clip;
            m_MovementAudio.pitch = Random.Range(m_OriginalPitch - m_PitchRange, m_OriginalPitch + m_PitchRange);
            m_MovementAudio.Play();
        }
        
        #endregion

        #region Public Methods
        
        public void AddExplosionForce(float force, Vector3 position, float radius, float upwardsModifier = 0f)
        {
            Vector3 direction = m_Transform.position - position;
            float distance = direction.magnitude;
            
            if (upwardsModifier != 0f)
            {
                direction.y += upwardsModifier;
            }
            direction.Normalize();
            
            float attenuation = 1f - Mathf.Clamp01(distance / radius);
            m_ExplosionForce = direction * force * attenuation;
        }
        
        #endregion
    }
}
