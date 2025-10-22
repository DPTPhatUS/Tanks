using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace Tanks.Complete
{
    public class TankInputUser : MonoBehaviour
    {
        public InputUser InputUser => m_InputUser;
        public InputActionAsset ActionAsset => m_LocalActionAsset;

        private InputUser m_InputUser;
        private InputActionAsset m_LocalActionAsset;

        private void Awake()
        {
            m_LocalActionAsset = InputActionAsset.FromJson(InputSystem.actions.ToJson());
            SetNewInputUser(InputUser.PerformPairingWithDevice(Keyboard.current));
        }

        public void ActivateScheme(string name)
        {
            m_InputUser.ActivateControlScheme(name);
        }

        public void SetNewInputUser(InputUser user)
        {
            if (!user.valid)
                return;

            m_InputUser = user;
            m_InputUser.AssociateActionsWithUser(m_LocalActionAsset);

            if (m_InputUser.controlScheme.HasValue)
                m_InputUser.ActivateControlScheme(m_InputUser.controlScheme.Value);
        }
    }
}
