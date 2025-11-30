using System.Collections.Generic;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Manages the visual HUD indicator for active power-ups on tanks.
    /// </summary>
    public class PowerUpHUD : MonoBehaviour
    {
        #region Serialized Fields
        
        [SerializeField] private GameObject m_DamageReductionHUD;
        [SerializeField] private GameObject m_EnhancedShootingHUD;
        [SerializeField] private GameObject m_EnhancedSpeedHUD;
        [SerializeField] private GameObject m_EnhancedShellHUD;
        [SerializeField] private GameObject m_HealingHUD;
        [SerializeField] private GameObject m_TemporaryInvencibilityHUD;
        
        #endregion

        #region Private Fields
        
        private Dictionary<PowerUp.PowerUpType, GameObject> m_HUDMap;
        private GameObject m_ActivePowerUpHUD;
        private Transform m_Transform;
        private float m_DisplayTime;
        private bool m_HasActivePowerUp;
        private bool m_IsTimeBased;
        
        private const float ROTATION_SPEED = 100f;
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            m_Transform = transform;
            InitializeHUDMap();
        }

        private void Update()
        {
            if (!m_HasActivePowerUp) return;
            
            // Rotate HUD
            m_Transform.rotation = Quaternion.Euler(0f, ROTATION_SPEED * Time.time, 0f);
            
            // Update timer for time-based power-ups
            if (m_IsTimeBased)
            {
                m_DisplayTime -= Time.deltaTime;
                if (m_DisplayTime <= 0f)
                {
                    DisableActiveHUD();
                }
            }
        }
        
        #endregion

        #region Initialization
        
        private void InitializeHUDMap()
        {
            m_HUDMap = new Dictionary<PowerUp.PowerUpType, GameObject>
            {
                { PowerUp.PowerUpType.DamageReduction, m_DamageReductionHUD },
                { PowerUp.PowerUpType.ShootingBonus, m_EnhancedShootingHUD },
                { PowerUp.PowerUpType.Speed, m_EnhancedSpeedHUD },
                { PowerUp.PowerUpType.DamageMultiplier, m_EnhancedShellHUD },
                { PowerUp.PowerUpType.Healing, m_HealingHUD },
                { PowerUp.PowerUpType.Invincibility, m_TemporaryInvencibilityHUD }
            };
        }
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Activates the HUD indicator for the specified power-up type.
        /// </summary>
        /// <param name="powerUpType">Type of the power-up to display.</param>
        /// <param name="duration">Duration to display the HUD (0 for non-time-based like DamageMultiplier).</param>
        public void SetActivePowerUp(PowerUp.PowerUpType powerUpType, float duration)
        {
            if (m_HUDMap.TryGetValue(powerUpType, out GameObject hud) && hud != null)
            {
                hud.SetActive(true);
                m_ActivePowerUpHUD = hud;
                m_DisplayTime = duration;
                m_HasActivePowerUp = true;
                
                // DamageMultiplier (special shell) is not time-based - it lasts until fired
                m_IsTimeBased = powerUpType != PowerUp.PowerUpType.DamageMultiplier;
            }
        }

        /// <summary>
        /// Disables the currently active power-up HUD indicator.
        /// </summary>
        public void DisableActiveHUD()
        {
            if (m_ActivePowerUpHUD != null)
            {
                m_ActivePowerUpHUD.SetActive(false);
                m_ActivePowerUpHUD = null;
            }
            
            m_HasActivePowerUp = false;
            m_DisplayTime = 0f;
        }
        
        #endregion
    }
}