using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tanks.Complete
{
    /// <summary>
    /// Handles tank shooting mechanics including charging, firing, and special shells.
    /// </summary>
    public class TankShooting : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Projectile")]
        public Rigidbody m_Shell;
        public Transform m_FireTransform;
        
        [Header("Launch Settings")]
        public float m_MinLaunchForce = 5f;
        public float m_MaxLaunchForce = 20f;
        public float m_MaxChargeTime = 0.75f;
        public float m_ShotCooldown = 1.0f;
        
        [Header("Damage")]
        public float m_MaxDamage = 100f;
        public float m_ExplosionForce = 50f;
        public float m_ExplosionRadius = 5f;
        
        [Header("UI & Audio")]
        public Slider m_AimSlider;
        public AudioSource m_ShootingAudio;
        public AudioClip m_ChargingClip;
        public AudioClip m_FireClip;
        
        [HideInInspector]
        public TankInputUser m_InputUser;
        
        #endregion

        #region Public Properties
        
        public float CurrentChargeRatio => (m_CurrentLaunchForce - m_MinLaunchForce) / m_LaunchForceRange;
        public bool IsCharging => m_IsCharging;
        
        public bool IsComputerControlled { get; set; }
        
        #endregion

        #region Private Fields
        
        // State
        private float m_CurrentLaunchForce;
        private float m_ChargeSpeed;
        private float m_LaunchForceRange;
        private float m_CooldownTimer;
        private bool m_IsCharging;
        private bool m_HasFired;
        private float m_BaseMinLaunchForce;
        
        // Special shell
        private bool m_HasSpecialShell;
        private float m_SpecialShellMultiplier = 1f;
        
        // Input
        private InputAction m_FireAction;
        
        // Cached references
        private PowerUpDetector m_PowerUpDetector;
        private PowerUpHUD m_PowerUpHUD;
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            ResetState();
        }

        private void Start()
        {
            SetupInput();
            CalculateChargeSpeed();
        }

        private void Update()
        {
            UpdateCooldown();
            UpdateAimSlider();
            
            if (CheckMaxChargeAutoFire())
            {
                return;
            }
            
            if (IsComputerControlled)
            {
                UpdateAICharging();
            }
            else
            {
                UpdatePlayerInput();
            }
        }
        
        #endregion

        #region Initialization
        
        private void CacheComponents()
        {
            m_InputUser = GetComponent<TankInputUser>();
            if (m_InputUser == null)
            {
                m_InputUser = gameObject.AddComponent<TankInputUser>();
            }
            
            m_PowerUpDetector = GetComponent<PowerUpDetector>();
            m_PowerUpHUD = GetComponentInChildren<PowerUpHUD>();
        }

        private void ResetState()
        {
            m_LaunchForceRange = m_MaxLaunchForce - m_MinLaunchForce;
            m_CurrentLaunchForce = m_MinLaunchForce;
            m_BaseMinLaunchForce = m_MinLaunchForce;
            m_HasSpecialShell = false;
            m_SpecialShellMultiplier = 1f;
            m_IsCharging = false;
            m_HasFired = false;
            
            if (m_AimSlider != null)
            {
                m_AimSlider.minValue = m_MinLaunchForce;
                m_AimSlider.maxValue = m_MaxLaunchForce;
                m_AimSlider.value = m_MinLaunchForce;
            }
        }

        private void SetupInput()
        {
            m_FireAction = m_InputUser.ActionAsset.FindAction("Fire");
            m_FireAction?.Enable();
        }

        private void CalculateChargeSpeed()
        {
            m_ChargeSpeed = m_LaunchForceRange / m_MaxChargeTime;
        }
        
        #endregion

        #region Update Logic
        
        private void UpdateCooldown()
        {
            if (m_CooldownTimer > 0f)
            {
                m_CooldownTimer -= Time.deltaTime;
            }
        }

        private void UpdateAimSlider()
        {
            if (m_AimSlider != null)
            {
                float newValue = m_IsCharging ? m_CurrentLaunchForce : m_BaseMinLaunchForce;
                if (Mathf.Abs(m_AimSlider.value - newValue) > 0.01f)
                {
                    m_AimSlider.value = newValue;
                }
            }
        }

        private bool CheckMaxChargeAutoFire()
        {
            if (m_CurrentLaunchForce >= m_MaxLaunchForce && !m_HasFired)
            {
                m_CurrentLaunchForce = m_MaxLaunchForce;
                Fire();
                return true;
            }
            return false;
        }

        private void UpdateAICharging()
        {
            if (m_IsCharging && !m_HasFired)
            {
                ChargeShot();
            }
        }

        private void UpdatePlayerInput()
        {
            if (m_FireAction == null) return;
            
            if (CanStartCharging() && m_FireAction.WasPressedThisFrame())
            {
                StartCharging();
            }
            else if (m_FireAction.IsPressed() && !m_HasFired)
            {
                ChargeShot();
            }
            else if (m_FireAction.WasReleasedThisFrame() && !m_HasFired)
            {
                Fire();
            }
        }

        private bool CanStartCharging()
        {
            return m_CooldownTimer <= 0f && !m_IsCharging;
        }

        private void ChargeShot()
        {
            m_CurrentLaunchForce += m_ChargeSpeed * Time.deltaTime;
        }
        
        #endregion

        #region Charging API (for AI)
        
        public void StartCharging()
        {
            if (m_IsCharging) return;
            
            m_IsCharging = true;
            m_HasFired = false;
            m_CurrentLaunchForce = m_MinLaunchForce;
            
            PlayAudio(m_ChargingClip);
        }

        public void StopCharging()
        {
            if (m_IsCharging && !m_HasFired)
            {
                Fire();
                m_IsCharging = false;
            }
        }
        
        #endregion

        #region Firing
        
        private void Fire()
        {
            m_HasFired = true;
            m_IsCharging = false;
            
            SpawnProjectile();
            PlayAudio(m_FireClip);
            
            m_CurrentLaunchForce = m_MinLaunchForce;
            m_CooldownTimer = m_ShotCooldown;
        }

        private void SpawnProjectile()
        {
            Rigidbody shellInstance = Instantiate(m_Shell, m_FireTransform.position, m_FireTransform.rotation);
            shellInstance.linearVelocity = m_CurrentLaunchForce * m_FireTransform.forward;
            
            ConfigureShellExplosion(shellInstance);
        }

        private void ConfigureShellExplosion(Rigidbody shell)
        {
            var explosion = shell.GetComponent<ShellExplosion>();
            if (explosion == null) return;
            
            explosion.m_ExplosionForce = m_ExplosionForce;
            explosion.m_ExplosionRadius = m_ExplosionRadius;
            explosion.m_MaxDamage = CalculateDamage();
        }

        private float CalculateDamage()
        {
            float damage = m_MaxDamage;
            
            if (m_HasSpecialShell)
            {
                damage *= m_SpecialShellMultiplier;
                ClearSpecialShell();
            }
            
            return damage;
        }

        private void ClearSpecialShell()
        {
            m_HasSpecialShell = false;
            m_SpecialShellMultiplier = 1f;
            
            if (m_PowerUpDetector != null)
            {
                m_PowerUpDetector.m_HasActivePowerUp = false;
            }
            
            if (m_PowerUpHUD != null)
            {
                m_PowerUpHUD.DisableActiveHUD();
            }
        }
        
        #endregion

        #region Audio
        
        private void PlayAudio(AudioClip clip)
        {
            if (m_ShootingAudio == null || clip == null) return;
            
            m_ShootingAudio.clip = clip;
            m_ShootingAudio.Play();
        }
        
        #endregion

        #region Public Methods
        
        public void EquipSpecialShell(float damageMultiplier)
        {
            m_HasSpecialShell = true;
            m_SpecialShellMultiplier = damageMultiplier;
        }

        /// <summary>
        /// Calculates where a projectile would land at a given charge level.
        /// </summary>
        public Vector3 GetProjectilePosition(float chargeRatio)
        {
            float launchForce = Mathf.Lerp(m_MinLaunchForce, m_MaxLaunchForce, chargeRatio);
            Vector3 velocity = launchForce * m_FireTransform.forward;
            
            // Solve quadratic equation for time of flight: 0.5*g*t^2 + vy*t + y0 = 0
            float a = 0.5f * Physics.gravity.y;
            float b = velocity.y;
            float c = m_FireTransform.position.y;
            
            float discriminant = b * b - 4f * a * c;
            if (discriminant <= 0f)
            {
                return m_FireTransform.position;
            }
            
            float sqrtDiscriminant = Mathf.Sqrt(discriminant);
            float t1 = (-b + sqrtDiscriminant) / (2f * a);
            float t2 = (-b - sqrtDiscriminant) / (2f * a);
            float timeToLand = t1 > 0f ? t1 : t2;
            
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 landingPosition = m_FireTransform.position + horizontalVelocity * timeToLand;
            landingPosition.y = 0f;
            
            return landingPosition;
        }
        
        #endregion
    }
}
