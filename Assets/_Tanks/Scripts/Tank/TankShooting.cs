using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tanks.Complete
{
    public class TankShooting : MonoBehaviour
    {
        public Rigidbody m_Shell;
        public Transform m_FireTransform;
        public Slider m_AimSlider;
        public AudioSource m_ShootingAudio;
        public AudioClip m_ChargingClip;
        public AudioClip m_FireClip;
        public float m_MinLaunchForce = 5f;
        public float m_MaxLaunchForce = 20f;
        public float m_MaxChargeTime = 0.75f;
        public float m_ShotCooldown = 1.0f;
        public float m_MaxDamage = 100f;
        public float m_ExplosionForce = 50f;
        public float m_ExplosionRadius = 5f;

        [HideInInspector]
        public TankInputUser m_InputUser;

        public float CurrentChargeRatio => (m_CurrentLaunchForce - m_MinLaunchForce) / (m_MaxLaunchForce - m_MinLaunchForce);
        public bool IsCharging => m_IsCharging;

        public bool m_IsComputerControlled { get; set; } = false;

        private string m_FireButton;
        private float m_CurrentLaunchForce;
        private float m_ChargeSpeed;
        private bool m_Fired;
        private bool m_HasSpecialShell;
        private float m_SpecialShellMultiplier;
        private InputAction fireAction;
        private bool m_IsCharging = false;
        private float m_BaseMinLaunchForce;
        private float m_ShotCooldownTimer;

        private void OnEnable()
        {
            m_CurrentLaunchForce = m_MinLaunchForce;
            m_BaseMinLaunchForce = m_MinLaunchForce;
            m_AimSlider.value = m_BaseMinLaunchForce;
            m_HasSpecialShell = false;
            m_SpecialShellMultiplier = 1.0f;

            m_AimSlider.minValue = m_MinLaunchForce;
            m_AimSlider.maxValue = m_MaxLaunchForce;
        }

        private void Awake()
        {
            m_InputUser = GetComponent<TankInputUser>();
            if (m_InputUser == null)
                m_InputUser = gameObject.AddComponent<TankInputUser>();
        }

        private void Start()
        {
            m_FireButton = "Fire";
            fireAction = m_InputUser.ActionAsset.FindAction(m_FireButton);

            fireAction.Enable();

            m_ChargeSpeed = (m_MaxLaunchForce - m_MinLaunchForce) / m_MaxChargeTime;
        }


        private void Update()
        {
            // Reset slider to base value each frame
            m_AimSlider.value = m_BaseMinLaunchForce;
            
            // Update cooldown timer (only matters for human, but cheap enough to always run)
            if (m_ShotCooldownTimer > 0.0f)
                m_ShotCooldownTimer -= Time.deltaTime;
            
            // Handle max charge auto-fire (shared logic)
            if (m_CurrentLaunchForce >= m_MaxLaunchForce && !m_Fired)
            {
                m_CurrentLaunchForce = m_MaxLaunchForce;
                Fire();
                return;
            }
            
            if (m_IsComputerControlled)
            {
                UpdateComputerCharging();
            }
            else
            {
                UpdateHumanInput();
            }
        }

        public void StartCharging()
        {
            if (m_IsCharging) return; // Prevent double-charging
            
            m_IsCharging = true;
            m_Fired = false;
            m_CurrentLaunchForce = m_MinLaunchForce;

            m_ShootingAudio.clip = m_ChargingClip;
            m_ShootingAudio.Play();
        }

        public void StopCharging()
        {
            if (m_IsCharging && !m_Fired)
            {
                Fire();
                m_IsCharging = false;
            }
        }
        
        private void UpdateCharging()
        {
            m_CurrentLaunchForce += m_ChargeSpeed * Time.deltaTime;
            m_AimSlider.value = m_CurrentLaunchForce;
        }

        private void UpdateComputerCharging()
        {
            if (m_IsCharging && !m_Fired)
            {
                UpdateCharging();
            }
        }

        private void UpdateHumanInput()
        {
            // Start charging on button press
            if (m_ShotCooldownTimer <= 0 && fireAction.WasPressedThisFrame())
            {
                StartCharging();
            }
            // Continue charging while held
            else if (fireAction.IsPressed() && !m_Fired)
            {
                UpdateCharging();
            }
            // Fire on release
            else if (fireAction.WasReleasedThisFrame() && !m_Fired)
            {
                Fire();
            }
        }


        private void Fire()
        {
            m_Fired = true;

            Rigidbody shellInstance = Instantiate(m_Shell, m_FireTransform.position, m_FireTransform.rotation) as Rigidbody;

            shellInstance.linearVelocity = m_CurrentLaunchForce * m_FireTransform.forward;

            ShellExplosion explosionData = shellInstance.GetComponent<ShellExplosion>();
            explosionData.m_ExplosionForce = m_ExplosionForce;
            explosionData.m_ExplosionRadius = m_ExplosionRadius;
            explosionData.m_MaxDamage = m_MaxDamage;

            if (m_HasSpecialShell)
            {
                explosionData.m_MaxDamage *= m_SpecialShellMultiplier;
                m_HasSpecialShell = false;
                m_SpecialShellMultiplier = 1f;

                PowerUpDetector powerUpDetector = GetComponent<PowerUpDetector>();
                if (powerUpDetector != null)
                    powerUpDetector.m_HasActivePowerUp = false;

                PowerUpHUD powerUpHUD = GetComponentInChildren<PowerUpHUD>();
                if (powerUpHUD != null)
                    powerUpHUD.DisableActiveHUD();
            }

            m_ShootingAudio.clip = m_FireClip;
            m_ShootingAudio.Play();

            m_CurrentLaunchForce = m_MinLaunchForce;

            m_ShotCooldownTimer = m_ShotCooldown;
        }


        public void EquipSpecialShell(float damageMultiplier)
        {
            m_HasSpecialShell = true;
            m_SpecialShellMultiplier = damageMultiplier;
        }

        public Vector3 GetProjectilePosition(float chargingLevel)
        {
            float chargeLevel = Mathf.Lerp(m_MinLaunchForce, m_MaxLaunchForce, chargingLevel);
            Vector3 velocity = chargeLevel * m_FireTransform.forward;

            float a = 0.5f * Physics.gravity.y;
            float b = velocity.y;
            float c = m_FireTransform.position.y;

            float sqrtContent = b * b - 4 * a * c;
            if (sqrtContent <= 0)
            {
                return m_FireTransform.position;
            }

            float answer1 = (-b + Mathf.Sqrt(sqrtContent)) / (2 * a);
            float answer2 = (-b - Mathf.Sqrt(sqrtContent)) / (2 * a);

            float answer = answer1 > 0 ? answer1 : answer2;

            Vector3 position = m_FireTransform.position + new Vector3(velocity.x, 0, velocity.z) * answer;
            position.y = 0;

            return position;
        }
    }
}