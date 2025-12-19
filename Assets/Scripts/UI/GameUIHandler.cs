using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Tanks.Complete
{
    /// <summary>
    /// Handles the start menu UI for tank selection and the pause menu integration.
    /// </summary>
    public class GameUIHandler : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("References")]
        [SerializeField] private GameManager m_GameManager;

        [Header("Start Menu")] 
        [SerializeField] private RectTransform m_StartMenuRoot;
        [SerializeField] private Button m_StartButton;
        [SerializeField] private StartMenuSlot[] m_PlayerSlots;
        [SerializeField] private OnScreenButton m_PauseMenuButton;
        
        #endregion

        #region Private Fields
        
        private TextMeshProUGUI m_StartButtonText;
        private int m_SlotUsed;
        private PauseMenu m_PauseMenu;
        private InputAction m_PauseAction;
        private CanvasScaler m_CanvasScaler;
        private float m_CachedRatio;
        
        private const string TEXT_REQUIRED = "2 Tanks required";
        private const string TEXT_START = "Start";
        private const int MIN_TANKS_REQUIRED = 2;
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            SetupCameraStack();
            m_CanvasScaler = GetComponentInParent<CanvasScaler>();
        }

        private void Start()
        {
            HideMobileUIIfPresent();
            InitializeStartButton();
            InitializePauseMenu();
            InitializePlayerSlots();
        }

        private void Update()
        {
            UpdateCanvasScaling();
        }
        
        #endregion

        #region Initialization
        
        private void SetupCameraStack()
        {
            Camera cam = GetComponentInParent<Camera>();
            var mainCameraData = Camera.main.GetUniversalAdditionalCameraData();
            
            if (!mainCameraData.cameraStack.Contains(cam))
            {
                mainCameraData.cameraStack.Add(cam);
            }
        }

        private void HideMobileUIIfPresent()
        {
            if (MobileUIControl.Instance != null)
            {
                MobileUIControl.Instance.Hide();
            }
        }

        private void InitializeStartButton()
        {
            m_StartButton.onClick.AddListener(StartGame);
            m_StartButton.interactable = false;
            m_StartButtonText = m_StartButton.GetComponentInChildren<TextMeshProUGUI>();
            m_StartButtonText.text = TEXT_REQUIRED;
            m_PauseMenuButton.gameObject.SetActive(false);
        }

        private void InitializePauseMenu()
        {
            m_PauseMenu = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
            
            if (m_PauseMenu != null)
            {
                m_PauseMenu.Init();
                m_PauseAction = InputSystem.actions.FindAction("Pause").Clone();
                
                var rectTransform = m_PauseMenuButton.GetComponent<RectTransform>();
                rectTransform.SetAsLastSibling();
            }
        }

        private void InitializePlayerSlots()
        {
            GameObject[] tankPrefabs =
            {
                m_GameManager.m_Tank1Prefab,
                m_GameManager.m_Tank2Prefab,
                m_GameManager.m_Tank3Prefab,
                m_GameManager.m_Tank4Prefab
            };

            int slotCount = m_PlayerSlots.Length;
            for (int i = 0; i < slotCount; i++)
            {
                InitializeSlot(i, tankPrefabs);
            }
        }

        private void InitializeSlot(int slotIndex, GameObject[] tankPrefabs)
        {
            var slot = m_PlayerSlots[slotIndex];
            slot.SetTankPreview(slotIndex < tankPrefabs.Length ? tankPrefabs[slotIndex] : tankPrefabs[0]);

            // Add button
            slot.m_AddControlButton.onClick.AddListener(() => OnSlotAdded(slot, slotIndex));
            
            // Off button
            slot.m_OffControlButton.onClick.AddListener(() => OnSlotRemoved(slot));
            
            // Player control buttons
            slot.m_P1ControlButton.onClick.AddListener(() => OnPlayerControlSelected(slot, 1));
            slot.m_P2ControlButton.onClick.AddListener(() => OnPlayerControlSelected(slot, 2));
            slot.m_ComputerControlButton.onClick.AddListener(() => slot.SetPlayerControlling(-1));
        }
        
        #endregion

        #region Slot Event Handlers
        
        private void OnSlotAdded(StartMenuSlot slot, int slotIndex)
        {
            slot.AddTank();
            m_SlotUsed++;

            bool player1Present = IsPlayerControllingAnySlot(1, slotIndex);
            slot.SetPlayerControlling(player1Present ? -1 : 1);

            UpdateStartButtonState();
        }

        private void OnSlotRemoved(StartMenuSlot slot)
        {
            slot.RemoveTank();
            m_SlotUsed--;
            UpdateStartButtonState();
        }

        private void OnPlayerControlSelected(StartMenuSlot slot, int playerNumber)
        {
            slot.SetPlayerControlling(playerNumber);

            // Remove duplicate player control from other slots
            int slotCount = m_PlayerSlots.Length;
            for (int j = 0; j < slotCount; j++)
            {
                var otherSlot = m_PlayerSlots[j];
                if (!otherSlot.IsOpen && otherSlot != slot && otherSlot.PlayerControlling == playerNumber)
                {
                    otherSlot.SetPlayerControlling(-1);
                }
            }
        }

        private bool IsPlayerControllingAnySlot(int playerNumber, int excludeIndex)
        {
            int slotCount = m_PlayerSlots.Length;
            for (int j = 0; j < slotCount; j++)
            {
                if (j != excludeIndex && m_PlayerSlots[j].PlayerControlling == playerNumber)
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateStartButtonState()
        {
            bool canStart = m_SlotUsed >= MIN_TANKS_REQUIRED;
            m_StartButtonText.text = canStart ? TEXT_START : TEXT_REQUIRED;
            m_StartButton.interactable = canStart;
        }
        
        #endregion

        #region Game Start
        
        private void StartGame()
        {
            m_StartMenuRoot.gameObject.SetActive(false);

            var playerData = BuildPlayerDataList();
            m_GameManager.StartGame(playerData.ToArray());

            CleanupTankPreviews();
            ShowMobileUIIfPresent();
            EnablePauseMenu();
        }

        private List<GameManager.PlayerData> BuildPlayerDataList()
        {
            var playerData = new List<GameManager.PlayerData>();
            
            int slotCount = m_PlayerSlots.Length;
            for (int i = 0; i < slotCount; i++)
            {
                var slot = m_PlayerSlots[i];
                if (!slot.IsOpen)
                {
                    playerData.Add(new GameManager.PlayerData
                    {
                        TankColor = slot.m_SlotColor,
                        IsComputer = slot.IsComputer,
                        ControlIndex = slot.PlayerControlling,
                        UsedPrefab = slot.TankPrefab
                    });
                }
            }
            
            return playerData;
        }

        private void CleanupTankPreviews()
        {
            int slotCount = m_PlayerSlots.Length;
            for (int i = 0; i < slotCount; i++)
            {
                Destroy(m_PlayerSlots[i].TankPreview);
            }
        }

        private void ShowMobileUIIfPresent()
        {
            if (MobileUIControl.Instance != null)
            {
                MobileUIControl.Instance.Show();
            }
        }

        private void EnablePauseMenu()
        {
            if (m_PauseMenu != null)
            {
                m_PauseAction.performed += OnPausePerformed;
                m_PauseAction.Enable();
                m_PauseMenuButton.gameObject.SetActive(true);
            }
        }

        private void OnPausePerformed(InputAction.CallbackContext ctx)
        {
            m_PauseMenu.TogglePause();
        }
        
        #endregion

        #region Canvas Scaling
        
        private void UpdateCanvasScaling()
        {
            float ratio = Screen.width / (float)Screen.height;
            float newMatch = ratio > 1.0f ? 1.0f : 0.0f;
            
            if (!Mathf.Approximately(m_CachedRatio, newMatch))
            {
                m_CachedRatio = newMatch;
                m_CanvasScaler.matchWidthOrHeight = newMatch;
            }
        }
        
        #endregion
    }
}