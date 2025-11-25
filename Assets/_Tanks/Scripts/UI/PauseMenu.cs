using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tanks.Complete
{
    public class PauseMenu : MonoBehaviour
    {
        public RectTransform m_PauseMenuRoot;
        public RectTransform m_PauseMenuButtonsRoot;
        public Button m_ControlScreenButton;

        public RectTransform m_ControlMenuRoot;
        public Button m_ControlMenuBackButton;

        public Button m_SelectTankButton;
        public Button m_QuitButton;

        public void Init()
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

            m_SelectTankButton.onClick.AddListener(() =>
            {
                Time.timeScale = 1.0f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            });

            if (Application.platform == RuntimePlatform.WebGLPlayer || Application.isEditor)
            {
                m_QuitButton.gameObject.SetActive(false);
            }
            else
            {
                m_QuitButton.gameObject.SetActive(true);
                m_QuitButton.onClick.AddListener(Application.Quit);
            }

            m_PauseMenuRoot.gameObject.SetActive(false);
            m_PauseMenuButtonsRoot.gameObject.SetActive(true);
        }
    
        public void TogglePause()
        {
            if (m_PauseMenuRoot != null)
            {
                bool state = !m_PauseMenuRoot.gameObject.activeSelf;
                m_PauseMenuRoot.gameObject.SetActive(state);

                Time.timeScale = state ? 0.0f : 1.0f;

                m_ControlMenuRoot.gameObject.SetActive(false);
                m_PauseMenuButtonsRoot.gameObject.SetActive(true);
            }
        }
    }
}