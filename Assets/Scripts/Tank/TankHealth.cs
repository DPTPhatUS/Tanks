using System;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    /// <summary>
    /// Manages tank health, damage, shields, and death effects.
    /// </summary>
    public class TankHealth : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Health Settings")]
        [SerializeField] private float m_StartingHealth = 100f;
        
        [Header("UI References")]
        [SerializeField] private Slider m_Slider;
        [SerializeField] private Image m_FillImage;
        [SerializeField] private Color m_FullHealthColor = Color.green;
        [SerializeField] private Color m_ZeroHealthColor = Color.red;
        
        [Header("Death Effects")]
        [SerializeField] private GameObject m_ExplosionPrefab;
        
        #endregion

        #region Public Properties
        
        // Legacy accessors for backwards compatibility
        public float m_StartingHealth_Accessor => m_StartingHealth;
        public bool m_HasShield { get; private set; }
        public float CurrentHealth => m_CurrentHealth;
        public float StartingHealth => m_StartingHealth;
        public float HealthRatio => m_StartingHealth > 0.001f ? Mathf.Clamp01(m_CurrentHealth / m_StartingHealth) : 0f;
        public bool IsDead => m_IsDead;

        public event Action<float, float> Damaged;
        public event Action Died;
        
        #endregion

        #region Private Fields
        
        private ParticleSystem m_ExplosionParticles;
        private AudioSource m_ExplosionAudio;
        private float m_CurrentHealth;
        private float m_InverseStartingHealth;  // Cached for color lerp optimization
        private float m_ShieldValue;
        private bool m_IsDead;
        private bool m_IsInvincible;
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            CreateExplosionInstance();
            InitializeSlider();
            m_InverseStartingHealth = 1f / m_StartingHealth;
        }

        private void OnDestroy()
        {
            if (m_ExplosionParticles != null)
            {
                Destroy(m_ExplosionParticles.gameObject);
            }
        }

        private void OnEnable()
        {
            ResetState();
        }
        
        #endregion

        #region Initialization
        
        private void CreateExplosionInstance()
        {
            GameObject explosionInstance = Instantiate(m_ExplosionPrefab);
            m_ExplosionParticles = explosionInstance.GetComponent<ParticleSystem>();
            m_ExplosionAudio = explosionInstance.GetComponent<AudioSource>();
            explosionInstance.SetActive(false);
        }

        private void InitializeSlider()
        {
            if (m_Slider != null)
            {
                m_Slider.maxValue = m_StartingHealth;
            }
        }

        private void ResetState()
        {
            m_CurrentHealth = m_StartingHealth;
            m_IsDead = false;
            m_HasShield = false;
            m_ShieldValue = 0f;
            m_IsInvincible = false;
            UpdateHealthUI();
        }
        
        #endregion

        #region Damage & Healing
        
        public void TakeDamage(float amount)
        {
            if (m_IsInvincible || m_IsDead) return;
            
            float effectiveDamage = amount * (1f - m_ShieldValue);
            m_CurrentHealth -= effectiveDamage;
            Damaged?.Invoke(effectiveDamage, m_CurrentHealth);
            
            UpdateHealthUI();
            
            if (m_CurrentHealth <= 0f)
            {
                OnDeath();
            }
        }

        public void IncreaseHealth(float amount)
        {
            m_CurrentHealth = Mathf.Min(m_CurrentHealth + amount, m_StartingHealth);
            UpdateHealthUI();
        }
        
        #endregion

        #region Buffs
        
        public void ToggleShield(float shieldAmount)
        {
            m_HasShield = !m_HasShield;
            m_ShieldValue = m_HasShield ? shieldAmount : 0f;
        }

        public void ToggleInvincibility()
        {
            m_IsInvincible = !m_IsInvincible;
        }
        
        #endregion

        #region UI
        
        private void UpdateHealthUI()
        {
            if (m_Slider == null) return;
            
            if (Mathf.Abs(m_Slider.value - m_CurrentHealth) > 0.01f)
            {
                m_Slider.value = m_CurrentHealth;
            }
            
            float healthPercent = m_CurrentHealth * m_InverseStartingHealth;
            m_FillImage.color = Color.Lerp(m_ZeroHealthColor, m_FullHealthColor, healthPercent);
        }
        
        #endregion

        #region Death
        
        private void OnDeath()
        {
            m_IsDead = true;
            Died?.Invoke();
            
            // Play explosion effects
            m_ExplosionParticles.transform.position = transform.position;
            m_ExplosionParticles.gameObject.SetActive(true);
            m_ExplosionParticles.Play();
            m_ExplosionAudio.Play();
            
            gameObject.SetActive(false);
        }
        
        #endregion
    }
}