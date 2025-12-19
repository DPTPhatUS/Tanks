using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Represents a collectible power-up that grants various effects to tanks.
    /// </summary>
    public class PowerUp : MonoBehaviour
    {
        #region Types
        
        public enum PowerUpType
        {
            Speed,
            DamageReduction,
            ShootingBonus,
            Healing,
            Invincibility,
            DamageMultiplier
        }
        
        #endregion

        #region Serialized Fields
        
        [Header("General Settings")]
        [SerializeField] private PowerUpType m_PowerUpType = PowerUpType.DamageReduction;
        [SerializeField] private ParticleSystem m_CollectFX;
        [SerializeField] private float m_DurationTime = 5f;

        [Header("Damage Reduction")]
        [SerializeField, Range(0f, 1f)] private float m_DamageReduction = 0.5f;

        [Header("Speed Bonus")]
        [SerializeField] private float m_SpeedBonus = 5f;
        [SerializeField] private float m_TurnSpeedBonus = 0f;

        [Header("Shooting Bonus")]
        [SerializeField, Range(0f, 1f)] private float m_CooldownReduction = 0.5f;

        [Header("Healing")]
        [SerializeField] private float m_HealingAmount = 20f;

        [Header("Extra Damage")]
        [SerializeField] private float m_DamageMultiplier = 2f;
        
        #endregion

        #region Private Fields
        
        private PowerUpSpawner m_Spawner;
        private Transform m_Transform;
        private int m_PlayerLayer;
        
        private const float ROTATION_SPEED = 50f;
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            m_Transform = transform;
            m_PlayerLayer = LayerMask.NameToLayer("Players");
        }

        private void Update()
        {
            // Rotate power-up for visual effect
            m_Transform.rotation = Quaternion.Euler(0f, ROTATION_SPEED * Time.time, 0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer != m_PlayerLayer) return;
            
            PowerUpDetector detector = other.GetComponent<PowerUpDetector>();
            if (detector == null || detector.m_HasActivePowerUp) return;
            
            ApplyPowerUp(detector);
            OnCollected();
        }
        
        #endregion

        #region Power-Up Logic
        
        private void ApplyPowerUp(PowerUpDetector detector)
        {
            switch (m_PowerUpType)
            {
                case PowerUpType.DamageReduction:
                    detector.PickUpShield(m_DamageReduction, m_DurationTime);
                    break;
                case PowerUpType.Speed:
                    detector.PowerUpSpeed(m_SpeedBonus, m_TurnSpeedBonus, m_DurationTime);
                    break;
                case PowerUpType.ShootingBonus:
                    detector.PowerUpShoootingRate(m_CooldownReduction, m_DurationTime);
                    break;
                case PowerUpType.Healing:
                    detector.PowerUpHealing(m_HealingAmount);
                    break;
                case PowerUpType.Invincibility:
                    detector.PowerUpInvincibility(m_DurationTime);
                    break;
                case PowerUpType.DamageMultiplier:
                    detector.PowerUpSpecialShell(m_DamageMultiplier);
                    break;
            }
        }

        private void OnCollected()
        {
            // Notify spawner
            if (m_Spawner != null)
            {
                m_Spawner.CollectPowerUp();
            }
            
            // Spawn collection effects
            if (m_CollectFX != null)
            {
                Instantiate(m_CollectFX, m_Transform.position, Quaternion.identity);
            }
            
            Destroy(gameObject);
        }
        
        #endregion

        #region Public Methods
        
        public void SetSpawner(PowerUpSpawner spawner)
        {
            m_Spawner = spawner;
        }
        
        #endregion
    }
}
