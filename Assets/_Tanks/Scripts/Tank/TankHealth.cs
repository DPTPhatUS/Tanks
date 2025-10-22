using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    public class TankHealth : MonoBehaviour
    {
        public float m_StartingHealth = 100f;
        public Slider m_Slider;
        public Image m_FillImage;
        public Color m_FullHealthColor = Color.green;
        public Color m_ZeroHealthColor = Color.red;
        public GameObject m_ExplosionPrefab;
        [HideInInspector] public bool m_HasShield;

        private AudioSource m_ExplosionAudio;
        private ParticleSystem m_ExplosionParticles;
        private float m_CurrentHealth;
        private bool m_Dead;
        private float m_ShieldValue;
        private bool m_IsInvincible;

        private void Awake()
        {
            m_ExplosionParticles = Instantiate(m_ExplosionPrefab).GetComponent<ParticleSystem>();
            m_ExplosionParticles.gameObject.SetActive(false);

            m_ExplosionAudio = m_ExplosionParticles.GetComponent<AudioSource>();

            m_Slider.maxValue = m_StartingHealth;
        }

        private void OnDestroy()
        {
            if (m_ExplosionParticles != null)
                Destroy(m_ExplosionParticles.gameObject);
        }

        private void OnEnable()
        {
            m_CurrentHealth = m_StartingHealth;
            m_Dead = false;
            m_HasShield = false;
            m_ShieldValue = 0;
            m_IsInvincible = false;

            SetHealthUI();
        }


        public void TakeDamage(float amount)
        {
            if (!m_IsInvincible)
            {
                m_CurrentHealth -= amount * (1 - m_ShieldValue);

                SetHealthUI();

                if (m_CurrentHealth <= 0f && !m_Dead)
                {
                    OnDeath();
                }
            }
        }


        public void IncreaseHealth(float amount)
        {
            if (m_CurrentHealth + amount <= m_StartingHealth)
            {
                m_CurrentHealth += amount;
            }
            else
            {
                m_CurrentHealth = m_StartingHealth;
            }

            SetHealthUI();
        }


        public void ToggleShield(float shieldAmount)
        {
            m_HasShield = !m_HasShield;

            if (m_HasShield)
            {
                m_ShieldValue = shieldAmount;
            }
            else
            {
                m_ShieldValue = 0;
            }
        }

        public void ToggleInvincibility()
        {
            m_IsInvincible = !m_IsInvincible;
        }


        private void SetHealthUI()
        {
            m_Slider.value = m_CurrentHealth;
            m_FillImage.color = Color.Lerp(m_ZeroHealthColor, m_FullHealthColor, m_CurrentHealth / m_StartingHealth);
        }


        private void OnDeath()
        {
            m_Dead = true;

            m_ExplosionParticles.transform.position = transform.position;
            m_ExplosionParticles.gameObject.SetActive(true);
            m_ExplosionParticles.Play();

            m_ExplosionAudio.Play();

            gameObject.SetActive(false);
        }
    }
}