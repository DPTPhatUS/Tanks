using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tanks.Complete
{
    /// <summary>
    /// Manages the pause menu UI and game pause state.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Pause Menu")]
        [SerializeField] private RectTransform m_PauseMenuRoot;
        [SerializeField] private RectTransform m_PauseMenuButtonsRoot;
        [SerializeField] private Button m_ControlScreenButton;

        [Header("Control Menu")]
        [SerializeField] private RectTransform m_ControlMenuRoot;
        [SerializeField] private Button m_ControlMenuBackButton;

        [Header("Actions")]
        [SerializeField] private Button m_SelectTankButton;
        [SerializeField] private Button m_QuitButton;
        
        #endregion

        #region Private Fields
        
        private bool m_IsPaused;
        
        #endregion

        #region Public Methods
        
        public void Init()
        {
            SetupControlMenuNavigation();
            SetupActionButtons();
            
            m_PauseMenuRoot.gameObject.SetActive(false);
            m_PauseMenuButtonsRoot.gameObject.SetActive(true);
        }
    
        public void TogglePause()
        {
            if (m_PauseMenuRoot == null) return;
            
            m_IsPaused = !m_IsPaused;
            m_PauseMenuRoot.gameObject.SetActive(m_IsPaused);
            Time.timeScale = m_IsPaused ? 0f : 1f;

            // Reset to main pause menu view
            m_ControlMenuRoot.gameObject.SetActive(false);
            m_PauseMenuButtonsRoot.gameObject.SetActive(true);
        }
        
        #endregion

        #region Private Methods
        
        private void SetupControlMenuNavigation()
        {
            m_ControlMenuBackButton.onClick.AddListener(() =>
            {
                m_ControlMenuRoot.gameObject.SetActive(false);
                m_PauseMenuButtonsRoot.gameObject.SetActive(true);
            });

            m_PauseMenuButtonsRoot.gameObject.SetActive(false);
            
            m_ControlScreenButton.onClick.AddListener(() =>
            {
                m_ControlMenuRoot.gameObject.SetActive(true);
                m_PauseMenuButtonsRoot.gameObject.SetActive(false);
            });
        }

        private void SetupActionButtons()
        {
            m_SelectTankButton.onClick.AddListener(() =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            });

            // Hide quit button on WebGL and in editor
            bool showQuitButton = Application.platform != RuntimePlatform.WebGLPlayer && !Application.isEditor;
            m_QuitButton.gameObject.SetActive(showQuitButton);
            
            if (showQuitButton)
            {
                m_QuitButton.onClick.AddListener(Application.Quit);
            }
        }
        
        #endregion
    }
}