using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Controls camera position and zoom to keep all active tanks in view.
    /// </summary>
    public class CameraControl : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Camera Settings")]
        [SerializeField] private float m_DampTime = 0.2f;
        [SerializeField] private float m_ScreenEdgeBuffer = 4f;
        [SerializeField] private float m_MinSize = 6.5f;
        
        #endregion
        
        #region Public Fields
        
        [HideInInspector]
        public Transform[] m_Targets;
        
        #endregion

        #region Private Fields
        
        private Camera m_Camera;
        private Transform m_CameraTransform;
        private Transform m_Transform;
        private float m_ZoomSpeed;
        private Vector3 m_MoveVelocity;
        private Vector3 m_DesiredPosition;
        private Vector3 m_AimToRig;
        private float m_CachedAspect;
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            CacheComponents();
            CalculateAimOffset();
        }

        private void FixedUpdate()
        {
            if (m_Targets == null || m_Targets.Length == 0) return;
            
            Move();
            Zoom();
        }
        
        #endregion

        #region Initialization
        
        private void CacheComponents()
        {
            m_Transform = transform;
            m_Camera = GetComponentInChildren<Camera>();
            m_CameraTransform = m_Camera.transform;
            m_CachedAspect = m_Camera.aspect;
        }

        private void CalculateAimOffset()
        {
            Plane groundPlane = new Plane(Vector3.up, m_Transform.position);
            Ray cameraRay = new Ray(m_CameraTransform.position, m_CameraTransform.forward);
            
            if (groundPlane.Raycast(cameraRay, out float distance))
            {
                Vector3 aimTarget = cameraRay.GetPoint(distance);
                m_AimToRig = m_Transform.position - aimTarget;
            }
        }
        
        #endregion

        #region Camera Movement
        
        private void Move()
        {
            FindAveragePosition();
            m_Transform.position = Vector3.SmoothDamp(m_Transform.position, m_DesiredPosition + m_AimToRig, ref m_MoveVelocity, m_DampTime);
        }

        private void FindAveragePosition()
        {
            Vector3 averagePos = Vector3.zero;
            int activeTargetCount = 0;
            int targetCount = m_Targets.Length;

            for (int i = 0; i < targetCount; i++)
            {
                Transform target = m_Targets[i];
                if (target == null || !target.gameObject.activeSelf)
                    continue;

                averagePos += target.position;
                activeTargetCount++;
            }

            if (activeTargetCount > 0)
            {
                averagePos /= activeTargetCount;
            }
            
            averagePos.y = m_Transform.position.y;
            m_DesiredPosition = averagePos;
        }
        
        #endregion

        #region Camera Zoom
        
        private void Zoom()
        {
            float requiredSize = FindRequiredSize();
            m_Camera.orthographicSize = Mathf.SmoothDamp(m_Camera.orthographicSize, requiredSize, ref m_ZoomSpeed, m_DampTime);
        }

        private float FindRequiredSize()
        {
            Vector3 desiredLocalPos = m_CameraTransform.InverseTransformPoint(m_DesiredPosition);
            float size = 0f;
            int targetCount = m_Targets.Length;

            for (int i = 0; i < targetCount; i++)
            {
                Transform target = m_Targets[i];
                if (target == null || !target.gameObject.activeSelf)
                    continue;

                Vector3 targetLocalPos = m_CameraTransform.InverseTransformPoint(target.position);
                Vector3 offset = targetLocalPos - desiredLocalPos;

                float verticalSize = Mathf.Abs(offset.y);
                float horizontalSize = Mathf.Abs(offset.x) / m_CachedAspect;
                
                size = Mathf.Max(size, verticalSize, horizontalSize);
            }

            return Mathf.Max(size + m_ScreenEdgeBuffer, m_MinSize);
        }
        
        #endregion

        #region Public Methods
        
        public void SetStartPositionAndSize()
        {
            FindAveragePosition();
            m_Transform.position = m_DesiredPosition + m_AimToRig;
            m_Camera.orthographicSize = FindRequiredSize();
        }
        
        #endregion
    }
}