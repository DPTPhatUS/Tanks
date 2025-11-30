using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
#endif
using UnityEngine;
using UnityEngine.InputSystem.Users;
using UnityEngine.UIElements;

namespace Tanks.Complete
{
    /// <summary>
    /// Manages tank instances, controlling behavior and state across different game phases.
    /// </summary>
    [Serializable]
    public class TankManager
    {
        #region Public Fields (Serialized)
        
        [HideInInspector] public Color m_PlayerColor;
        public Transform m_SpawnPoint;
        [HideInInspector] public int m_PlayerNumber;
        [HideInInspector] public string m_ColoredPlayerText;
        [HideInInspector] public GameObject m_Instance;
        [HideInInspector] public int m_Wins;
        [HideInInspector] public bool m_ComputerControlled;
        
        #endregion

        #region Public Properties
        
        public int ControlIndex { get; set; } = 1;
        
        #endregion

        #region Private Fields
        
        private TankMovement m_Movement;
        private TankShooting m_Shooting;
        private GameObject m_CanvasGameObject;
        private TankAI m_AI;
        private InputUser m_InputUser;
        
        private const string TANK_COLOR_MATERIAL_NAME = "TankColor";
        
        #endregion

        #region Public Methods
        
        public void Setup(GameManager manager)
        {
            CacheComponents();
            SetupInputUser();
            SetupControlMode();
            SetupAI(manager);
            GenerateColoredPlayerText();
            ApplyPlayerColor();
        }

        public void DisableControl()
        {
            m_Movement.enabled = false;
            m_Shooting.enabled = false;
            
            if (m_ComputerControlled && m_AI != null)
            {
                m_AI.enabled = false;
            }

            m_CanvasGameObject.SetActive(false);
        }

        public void EnableControl()
        {
            m_Movement.enabled = true;
            m_Shooting.enabled = true;
            
            if (m_ComputerControlled && m_AI != null)
            {
                m_AI.enabled = true;
            }

            m_CanvasGameObject.SetActive(true);
        }

        public void Reset()
        {
            Transform instanceTransform = m_Instance.transform;
            instanceTransform.position = m_SpawnPoint.position;
            instanceTransform.rotation = m_SpawnPoint.rotation;

            m_Instance.SetActive(false);
            m_Instance.SetActive(true);
        }
        
        #endregion

        #region Private Methods
        
        private void CacheComponents()
        {
            m_Movement = m_Instance.GetComponent<TankMovement>();
            m_Shooting = m_Instance.GetComponent<TankShooting>();
            m_AI = m_Instance.GetComponent<TankAI>();
            m_CanvasGameObject = m_Instance.GetComponentInChildren<Canvas>().gameObject;
        }

        private void SetupInputUser()
        {
            var inputUser = m_Instance.GetComponent<TankInputUser>();
            inputUser.SetNewInputUser(m_InputUser);
        }

        private void SetupControlMode()
        {
            m_Movement.m_IsComputerControlled = m_ComputerControlled;
            m_Shooting.IsComputerControlled = m_ComputerControlled;
            m_Movement.m_PlayerNumber = m_PlayerNumber;
            m_Movement.ControlIndex = ControlIndex;
        }

        private void SetupAI(GameManager manager)
        {
            if (m_ComputerControlled)
            {
                m_AI = m_Instance.AddComponent<TankAI>();
                m_AI.Setup(manager);
            }
        }

        private void GenerateColoredPlayerText()
        {
            string colorHex = ColorUtility.ToHtmlStringRGB(m_PlayerColor);
            m_ColoredPlayerText = $"<color=#{colorHex}>PLAYER {m_PlayerNumber}</color>";
        }

        private void ApplyPlayerColor()
        {
            MeshRenderer[] renderers = m_Instance.GetComponentsInChildren<MeshRenderer>();
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
                        materials[j].color = m_PlayerColor;
                        materialChanged = true;
                    }
                }
                
                // Only reassign if we changed something (optimization)
                if (materialChanged)
                {
                    renderer.materials = materials;
                }
            }
        }
        
        #endregion
    }
    
    
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(TankManager))]
    public class TankManagerDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var itemSlot = new PropertyField(property.FindPropertyRelative(nameof(TankManager.m_SpawnPoint)));
            return itemSlot;
        }
    }
#endif
}