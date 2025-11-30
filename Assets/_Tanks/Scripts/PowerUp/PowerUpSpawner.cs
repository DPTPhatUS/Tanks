using System.Collections;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Spawns and manages power-up instances at a specific location.
    /// </summary>
    public class PowerUpSpawner : MonoBehaviour
    {
        #region Serialized Fields
        
        [Tooltip("Array that holds different power-up prefabs that can be spawned.")]
        [SerializeField] private PowerUp[] m_PowerUps;
        
        [Tooltip("Time in seconds before respawning after collection.")]
        [SerializeField] private float m_RespawnCooldown = 20f;
        
        #endregion

        #region Private Fields
        
        private WaitForSeconds m_RespawnWait;
        private Vector3 m_SpawnPosition;
        private const float SPAWN_HEIGHT = 1.09f;
        
        #endregion

        #region Unity Lifecycle
        
        private void Start()
        {
            m_RespawnWait = new WaitForSeconds(m_RespawnCooldown);
            
            Vector3 position = transform.position;
            m_SpawnPosition = new Vector3(position.x, SPAWN_HEIGHT, position.z);
            
            SpawnRandomPowerUp();
        }
        
        #endregion

        #region Spawning
        
        private void SpawnRandomPowerUp()
        {
            int powerUpCount = m_PowerUps.Length;
            if (powerUpCount == 0) return;
            
            int randomIndex = Random.Range(0, powerUpCount);
            PowerUp spawnedPowerup = Instantiate(m_PowerUps[randomIndex], m_SpawnPosition, Quaternion.identity);
            spawnedPowerup.SetSpawner(this);
        }
        
        #endregion

        #region Public Methods
        
        public void CollectPowerUp()
        {
            StartCoroutine(RespawnPowerUp());
        }
        
        #endregion

        #region Coroutines
        
        private IEnumerator RespawnPowerUp()
        {
            yield return m_RespawnWait;
            SpawnRandomPowerUp();
        }
        
        #endregion
    }
}