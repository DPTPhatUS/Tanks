using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Tanks.Complete
{
    /// <summary>
    /// Central game controller managing rounds, spawning, and game state.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region Types
        
        public enum GameState
        {
            MainMenu,
            Game
        }

        public class PlayerData
        {
            public bool IsComputer;
            public Color TankColor;
            public GameObject UsedPrefab;
            public int ControlIndex;
        }
        
        #endregion

        #region Serialized Fields
        
        [Header("Game Settings")]
        [SerializeField] private int m_NumRoundsToWin = 5;
        [SerializeField] private float m_StartDelay = 3f;
        [SerializeField] private float m_EndDelay = 3f;
        
        [Header("References")]
        [SerializeField] private CameraControl m_CameraControl;

        [Header("Tank Prefabs")]
        public GameObject m_Tank1Prefab;
        public GameObject m_Tank2Prefab;
        public GameObject m_Tank3Prefab;
        public GameObject m_Tank4Prefab;
        
        [FormerlySerializedAs("m_Tanks")] 
        public TankManager[] m_SpawnPoints;
        
        #endregion

        #region Private Fields
        
        private GameState m_CurrentState;
        private int m_RoundNumber;
        private WaitForSeconds m_StartWait;
        private WaitForSeconds m_EndWait;
        private TankManager m_RoundWinner;
        private TankManager m_GameWinner;
        private PlayerData[] m_TankData;
        private int m_PlayerCount;
        private TextMeshProUGUI m_TitleText;
        private StringBuilder m_MessageBuilder;  // Reusable StringBuilder for EndMessage
        
        #endregion

        #region Public Properties (Legacy Accessors)
        
        public CameraControl CameraControl => m_CameraControl;
        public TankManager[] SpawnPoints => m_SpawnPoints;
        
        #endregion

        #region Unity Lifecycle
        
        private void Start()
        {
            m_CurrentState = GameState.MainMenu;
            m_MessageBuilder = new StringBuilder(256);

            var textRef = FindAnyObjectByType<MessageTextReference>(FindObjectsInactive.Include);

            if (textRef == null)
            {
                Debug.LogError("You need to add the Menus prefab in the scene to use the GameManager!");
                return;
            }

            m_TitleText = textRef.Text;
            m_TitleText.text = string.Empty;

            ValidateTankPrefabs();
        }
        
        #endregion

        #region Initialization
        
        private void ValidateTankPrefabs()
        {
            if (m_Tank1Prefab == null || m_Tank2Prefab == null || 
                m_Tank3Prefab == null || m_Tank4Prefab == null)
            {
                Debug.LogError("You need to assign 4 tank prefabs in the GameManager!");
            }
        }

        private void GameStart()
        {
            m_StartWait = new WaitForSeconds(m_StartDelay);
            m_EndWait = new WaitForSeconds(m_EndDelay);

            SpawnAllTanks();
            SetCameraTargets();

            StartCoroutine(GameLoop());
        }
        
        #endregion

        #region Public Methods
        
        public void StartGame(PlayerData[] playerData)
        {
            m_TankData = playerData;
            m_PlayerCount = m_TankData.Length;
            ChangeGameState(GameState.Game);
        }
        
        #endregion

        #region Game State
        
        private void ChangeGameState(GameState newState)
        {
            m_CurrentState = newState;

            if (m_CurrentState == GameState.Game)
            {
                GameStart();
            }
        }
        
        #endregion

        #region Spawning
        
        private void SpawnAllTanks()
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
                var playerData = m_TankData[i];
                var spawnPoint = m_SpawnPoints[i];
                
                spawnPoint.m_Instance = Instantiate(
                    playerData.UsedPrefab, 
                    spawnPoint.m_SpawnPoint.position, 
                    spawnPoint.m_SpawnPoint.rotation);

                // Guard against prefab having IsComputerControlled set to true
                var movement = spawnPoint.m_Instance.GetComponent<TankMovement>();
                movement.m_IsComputerControlled = false;
                
                spawnPoint.m_PlayerNumber = i + 1;
                spawnPoint.ControlIndex = playerData.ControlIndex;
                spawnPoint.m_PlayerColor = playerData.TankColor;
                spawnPoint.m_ComputerControlled = playerData.IsComputer;
            }

            // Delayed setup after all tanks are created (AI needs access to all tanks)
            for (int i = 0; i < m_SpawnPoints.Length; i++)
            {
                var tank = m_SpawnPoints[i];
                if (tank.m_Instance != null)
                {
                    tank.Setup(this);
                }
            }
        }

        private void SetCameraTargets()
        {
            Transform[] targets = new Transform[m_PlayerCount];

            for (int i = 0; i < m_PlayerCount; i++)
            {
                targets[i] = m_SpawnPoints[i].m_Instance.transform;
            }

            m_CameraControl.m_Targets = targets;
        }
        
        #endregion

        #region Game Loop
        
        private IEnumerator GameLoop()
        {
            yield return StartCoroutine(RoundStarting());
            yield return StartCoroutine(RoundPlaying());
            yield return StartCoroutine(RoundEnding());

            if (m_GameWinner != null)
            {
                SceneManager.LoadScene(0);
            }
            else
            {
                StartCoroutine(GameLoop());
            }
        }

        private IEnumerator RoundStarting()
        {
            ResetAllTanks();
            DisableTankControl();

            m_CameraControl.SetStartPositionAndSize();

            m_RoundNumber++;
            m_TitleText.text = $"ROUND {m_RoundNumber}";

            yield return m_StartWait;
        }

        private IEnumerator RoundPlaying()
        {
            EnableTankControl();
            m_TitleText.text = string.Empty;

            while (!OneTankLeft())
            {
                yield return null;
            }
        }

        private IEnumerator RoundEnding()
        {
            DisableTankControl();

            m_RoundWinner = GetRoundWinner();

            if (m_RoundWinner != null)
            {
                m_RoundWinner.m_Wins++;
            }

            m_GameWinner = GetGameWinner();

            m_TitleText.text = BuildEndMessage();

            yield return m_EndWait;
        }
        
        #endregion

        #region Round Logic
        
        private bool OneTankLeft()
        {
            int numTanksLeft = 0;

            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (m_SpawnPoints[i].m_Instance.activeSelf)
                {
                    numTanksLeft++;
                }
            }

            return numTanksLeft <= 1;
        }

        private TankManager GetRoundWinner()
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (m_SpawnPoints[i].m_Instance.activeSelf)
                {
                    return m_SpawnPoints[i];
                }
            }

            return null;
        }

        private TankManager GetGameWinner()
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (m_SpawnPoints[i].m_Wins == m_NumRoundsToWin)
                {
                    return m_SpawnPoints[i];
                }
            }

            return null;
        }

        private string BuildEndMessage()
        {
            // Check for game winner first (overrides everything)
            if (m_GameWinner != null)
            {
                return $"{m_GameWinner.m_ColoredPlayerText} WINS THE GAME!";
            }

            m_MessageBuilder.Clear();

            // Round result
            if (m_RoundWinner != null)
            {
                m_MessageBuilder.Append(m_RoundWinner.m_ColoredPlayerText);
                m_MessageBuilder.Append(" WINS THE ROUND!");
            }
            else
            {
                m_MessageBuilder.Append("DRAW!");
            }

            m_MessageBuilder.Append("\n\n\n\n");

            // Player scores
            for (int i = 0; i < m_PlayerCount; i++)
            {
                var tank = m_SpawnPoints[i];
                m_MessageBuilder.Append(tank.m_ColoredPlayerText);
                m_MessageBuilder.Append(": ");
                m_MessageBuilder.Append(tank.m_Wins);
                m_MessageBuilder.Append(" WINS\n");
            }

            return m_MessageBuilder.ToString();
        }
        
        #endregion

        #region Tank Control
        
        private void ResetAllTanks()
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
                m_SpawnPoints[i].Reset();
            }
        }

        private void EnableTankControl()
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
                m_SpawnPoints[i].EnableControl();
            }
        }

        private void DisableTankControl()
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
                m_SpawnPoints[i].DisableControl();
            }
        }
        
        #endregion
    }
}