using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Handles power-up collection visual and audio effects.
    /// </summary>
    public class PowerUpFX : MonoBehaviour
    {
        [SerializeField] private float m_LifeTime = 3f;

        private void Start()
        {
            // Play audio and schedule destruction
            AudioSource audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.Play();
            }
            
            Destroy(gameObject, m_LifeTime);
        }
    }
}
