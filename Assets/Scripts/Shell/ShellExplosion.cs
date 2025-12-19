using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Handles shell explosion effects, damage calculation, and force application to nearby tanks.
    /// </summary>
    public class ShellExplosion : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("References")]
        [SerializeField] private LayerMask m_TankMask;
        [SerializeField] private ParticleSystem m_ExplosionParticles;
        [SerializeField] private AudioSource m_ExplosionAudio;
        
        #endregion

        #region Public Fields (Set by TankShooting)
        
        [HideInInspector] public float m_MaxLifeTime = 2f;
        [HideInInspector] public float m_MaxDamage = 100f;
        [HideInInspector] public float m_ExplosionForce = 50f;
        [HideInInspector] public float m_ExplosionRadius = 5f;
        
        #endregion

        #region Private Fields
        
        private Transform m_Transform;
        private static readonly Collider[] s_HitColliders = new Collider[8]; // Reusable buffer
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            m_Transform = transform;
        }

        private void Start()
        {
            Destroy(gameObject, m_MaxLifeTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            Explode();
        }
        
        #endregion

        #region Explosion Logic
        
        private void Explode()
        {
            Vector3 explosionPosition = m_Transform.position;
            
            // Use non-allocating overlap sphere
            int hitCount = Physics.OverlapSphereNonAlloc(explosionPosition, m_ExplosionRadius, s_HitColliders, m_TankMask);
            
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = s_HitColliders[i];
                
                // Try to get TankMovement (also confirms it's a valid tank)
                TankMovement tankMovement = hitCollider.GetComponent<TankMovement>();
                if (tankMovement == null) continue;
                
                tankMovement.AddExplosionForce(m_ExplosionForce, explosionPosition, m_ExplosionRadius);
                
                // Apply damage if tank has health component
                TankHealth tankHealth = hitCollider.GetComponent<TankHealth>();
                if (tankHealth != null)
                {
                    float damage = CalculateDamage(hitCollider.transform.position, explosionPosition);
                    tankHealth.TakeDamage(damage);
                }
            }
            
            PlayExplosionEffects();
            Destroy(gameObject);
        }

        private float CalculateDamage(Vector3 targetPosition, Vector3 explosionPosition)
        {
            float distance = (targetPosition - explosionPosition).magnitude;
            float normalizedDistance = Mathf.Clamp01(1f - (distance / m_ExplosionRadius));
            return normalizedDistance * m_MaxDamage;
        }

        private void PlayExplosionEffects()
        {
            // Detach particles so they persist after shell is destroyed
            m_ExplosionParticles.transform.parent = null;
            m_ExplosionParticles.Play();
            m_ExplosionAudio.Play();
            
            // Schedule particle cleanup
            float particleDuration = m_ExplosionParticles.main.duration;
            Destroy(m_ExplosionParticles.gameObject, particleDuration);
        }
        
        #endregion
    }
}