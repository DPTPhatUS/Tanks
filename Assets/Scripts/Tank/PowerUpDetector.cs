using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Detects and applies power-up effects to tanks.
    /// </summary>
    public class PowerUpDetector : MonoBehaviour
    {
        #region Public Properties
        
        public bool m_HasActivePowerUp { get; set; }
        
        #endregion

        #region Private Fields
        
        private TankShooting m_TankShooting;
        private TankMovement m_TankMovement;
        private TankHealth m_TankHealth;
        private PowerUpHUD m_PowerUpHUD;
        
        // Cached WaitForSeconds for common durations
        private Dictionary<float, WaitForSeconds> m_WaitCache;
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            m_TankShooting = GetComponent<TankShooting>();
            m_TankMovement = GetComponent<TankMovement>();
            m_TankHealth = GetComponent<TankHealth>();
            m_PowerUpHUD = GetComponentInChildren<PowerUpHUD>();
            m_WaitCache = new Dictionary<float, WaitForSeconds>();
        }
        
        #endregion

        #region Public Methods - Power-Up Application
        
        public void PowerUpSpeed(float speedBoost, float turnSpeedBoost, float duration)
        {
            StartCoroutine(ApplySpeedBoost(speedBoost, turnSpeedBoost, duration));
        }

        public void PowerUpShoootingRate(float cooldownReduction, float duration)
        {
            if (cooldownReduction > 0)
            {
                StartCoroutine(ApplyShootingBoost(cooldownReduction, duration));
            }
        }

        public void PickUpShield(float shieldAmount, float duration)
        {
            if (!m_TankHealth.m_HasShield)
            {
                StartCoroutine(ApplyShield(shieldAmount, duration));
            }
        }

        public void PowerUpHealing(float healAmount)
        {
            m_TankHealth.IncreaseHealth(healAmount);
            m_PowerUpHUD.SetActivePowerUp(PowerUp.PowerUpType.Healing, 1.0f);
        }

        public void PowerUpInvincibility(float duration)
        {
            StartCoroutine(ApplyInvincibility(duration));
        }

        public void PowerUpSpecialShell(float damageMultiplier)
        {
            m_HasActivePowerUp = true;
            m_PowerUpHUD.SetActivePowerUp(PowerUp.PowerUpType.DamageMultiplier, 0f);
            m_TankShooting.EquipSpecialShell(damageMultiplier);
        }
        
        #endregion

        #region Private Methods
        
        private WaitForSeconds GetWaitForSeconds(float duration)
        {
            if (!m_WaitCache.TryGetValue(duration, out WaitForSeconds wait))
            {
                wait = new WaitForSeconds(duration);
                m_WaitCache[duration] = wait;
            }
            return wait;
        }
        
        #endregion

        #region Coroutines
        
        private IEnumerator ApplySpeedBoost(float speedBoost, float turnSpeedBoost, float duration)
        {
            m_HasActivePowerUp = true;
            m_PowerUpHUD.SetActivePowerUp(PowerUp.PowerUpType.Speed, duration);
            
            m_TankMovement.m_Speed += speedBoost;
            m_TankMovement.m_TurnSpeed += turnSpeedBoost;
            
            yield return GetWaitForSeconds(duration);
            
            m_TankMovement.m_Speed -= speedBoost;
            m_TankMovement.m_TurnSpeed -= turnSpeedBoost;
            m_HasActivePowerUp = false;
        }

        private IEnumerator ApplyShootingBoost(float cooldownReduction, float duration)
        {
            m_HasActivePowerUp = true;
            m_PowerUpHUD.SetActivePowerUp(PowerUp.PowerUpType.ShootingBonus, duration);
            
            m_TankShooting.m_ShotCooldown *= cooldownReduction;
            
            yield return GetWaitForSeconds(duration);
            
            m_TankShooting.m_ShotCooldown /= cooldownReduction;
            m_HasActivePowerUp = false;
        }

        private IEnumerator ApplyShield(float shieldAmount, float duration)
        {
            m_HasActivePowerUp = true;
            m_PowerUpHUD.SetActivePowerUp(PowerUp.PowerUpType.DamageReduction, duration);
            
            m_TankHealth.ToggleShield(shieldAmount);
            
            yield return GetWaitForSeconds(duration);
            
            m_TankHealth.ToggleShield(shieldAmount);
            m_HasActivePowerUp = false;
        }

        private IEnumerator ApplyInvincibility(float duration)
        {
            m_HasActivePowerUp = true;
            m_PowerUpHUD.SetActivePowerUp(PowerUp.PowerUpType.Invincibility, duration);
            
            m_TankHealth.ToggleInvincibility();
            
            yield return GetWaitForSeconds(duration);
            
            m_TankHealth.ToggleInvincibility();
            m_HasActivePowerUp = false;
        }
        
        #endregion
    }
}