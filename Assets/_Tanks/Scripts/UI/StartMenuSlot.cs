using TMPro;
using UnityEngine;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace Tanks.Complete
{
    /// <summary>
    /// Manages a single tank slot in the start menu UI.
    /// </summary>
    public class StartMenuSlot : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Slot Settings")]
        public Color m_SlotColor;
        
        [Header("References")]
        public RectTransform m_TankPreviewPosition;
        public TextMeshProUGUI m_TankStats;
        public Button m_AddControlButton;
        public RectTransform m_ControlChoiceRoot;
        public Button m_P1ControlButton;
        public Button m_P2ControlButton;
        public Button m_ComputerControlButton;
        public Button m_OffControlButton;
        public Image BackgroundImage;
        public Sprite OpenSlotBackground;
        public Sprite UsedSlotBackground;
        
        #endregion

        #region Public Properties
        
        public GameObject TankPreview { get; set; }
        public GameObject TankPrefab { get; private set; }
        public int PlayerControlling { get; set; }
        public bool IsOpen { get; set; }
        public bool IsComputer { get; set; }
        
        #endregion

        #region Private Fields
        
        private Camera m_MenuCamera;
        private Transform m_TankPreviewTransform;
        
        private const float PREVIEW_ROTATION_SPEED = 45f;
        private const float PREVIEW_DEPTH_OFFSET = 3f;
        private const string TANK_COLOR_MATERIAL_NAME = "TankColor";
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            m_MenuCamera = GetComponentInParent<Camera>();
            IsOpen = true;

            if (Application.isMobilePlatform)
            {
                m_P2ControlButton.gameObject.SetActive(false);
            }

            BackgroundImage.sprite = OpenSlotBackground;
        }
        
        private void Update()
        {
            if (m_TankPreviewTransform != null)
            {
                m_TankPreviewTransform.Rotate(Vector3.up, PREVIEW_ROTATION_SPEED * Time.deltaTime);
            }
        }
        
        #endregion

        #region Public Methods
        
        public void AddTank()
        {
            m_AddControlButton.gameObject.SetActive(false);
            m_ControlChoiceRoot.gameObject.SetActive(true);

            IsOpen = false;
            BackgroundImage.sprite = UsedSlotBackground;
        }

        public void RemoveTank()
        {
            m_AddControlButton.gameObject.SetActive(true);
            m_ControlChoiceRoot.gameObject.SetActive(false);

            SetPlayerControlling(-1);

            IsOpen = true;
            BackgroundImage.sprite = OpenSlotBackground;
        }

        public void SetPlayerControlling(int playerNumber)
        {
            // Re-enable previous button
            SetControlButtonInteractable(PlayerControlling, true);
            
            PlayerControlling = playerNumber;
            
            // Disable new button and set computer flag
            SetControlButtonInteractable(playerNumber, false);
            IsComputer = playerNumber == -1;
        }

        public void SetTankPreview(GameObject prefab)
        {
            if (TankPreview != null)
            {
                Destroy(TankPreview);
            }

            TankPrefab = prefab;
            TankPreview = Instantiate(prefab);
            m_TankPreviewTransform = TankPreview.transform;
            
            SetupPreviewComponents();
            PositionPreview();
            ApplySlotColor();
            RemoveAudioSources();
        }
        
        #endregion

        #region Private Methods
        
        private void SetControlButtonInteractable(int playerNumber, bool interactable)
        {
            switch (playerNumber)
            {
                case 1:
                    m_P1ControlButton.interactable = interactable;
                    break;
                case 2:
                    m_P2ControlButton.interactable = interactable;
                    break;
                case -1:
                    m_ComputerControlButton.interactable = interactable;
                    break;
            }
        }

        private void SetupPreviewComponents()
        {
            var move = TankPreview.GetComponent<TankMovement>();
            var shoot = TankPreview.GetComponent<TankShooting>();
            var health = TankPreview.GetComponent<TankHealth>();

            move.enabled = false;
            shoot.enabled = false;

            m_TankStats.text = $"Speed {move.m_Speed}\nDamage {shoot.m_MaxDamage}\nHealth: {health.m_StartingHealth_Accessor}";
        }

        private void PositionPreview()
        {
            Vector3 screenPosition = m_MenuCamera.WorldToScreenPoint(m_TankPreviewPosition.position);
            m_TankPreviewTransform.position = m_MenuCamera.ScreenToWorldPoint(screenPosition) + Vector3.back * PREVIEW_DEPTH_OFFSET;
        }

        private void ApplySlotColor()
        {
            MeshRenderer[] renderers = TankPreview.GetComponentsInChildren<MeshRenderer>();
            int rendererCount = renderers.Length;
            
            for (int i = 0; i < rendererCount; i++)
            {
                var renderer = renderers[i];
                Material[] materials = renderer.materials;
                int materialCount = materials.Length;
                bool materialChanged = false;
                
                for (int j = 0; j < materialCount; j++)
                {
                    if (materials[j].name.Contains(TANK_COLOR_MATERIAL_NAME))
                    {
                        materials[j].color = m_SlotColor;
                        materialChanged = true;
                    }
                }
                
                if (materialChanged)
                {
                    renderer.materials = materials;
                }
            }
        }

        private void RemoveAudioSources()
        {
            AudioSource[] audioSources = TankPreview.GetComponentsInChildren<AudioSource>();
            int count = audioSources.Length;
            
            for (int i = 0; i < count; i++)
            {
                Destroy(audioSources[i]);
            }
        }
        
        #endregion
    }
}