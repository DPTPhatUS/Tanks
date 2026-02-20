using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.InputSystem;

public class SphereAgent : Agent
{
    public Transform m_Target;
    public float m_ForceMultiplier = 10;

    private Rigidbody m_Rigidbody;
    private InputAction m_VerticalAction;
    private InputAction m_HorizontalAction;

    void Start()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
        m_VerticalAction = InputSystem.actions.FindAction("Vertical");
        m_HorizontalAction = InputSystem.actions.FindAction("Horizontal");
    }

    public override void OnEpisodeBegin()
    {
        if (transform.localPosition.y < 0)
        {
            m_Rigidbody.angularVelocity = Vector3.zero;
            m_Rigidbody.linearVelocity = Vector3.zero;
            transform.localPosition = new Vector3(0, 1.0f, 0);
        }

        m_Target.localPosition = new Vector3(Random.value * 20 - 10.0f, 1.0f, Random.value * 20 - 10.0f);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(m_Rigidbody.linearVelocity.x);
        sensor.AddObservation(m_Rigidbody.linearVelocity.z);
        sensor.AddObservation(m_Target.localPosition);
        sensor.AddObservation(transform.localPosition);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        Vector3 force = Vector3.zero;
        force.x = actions.ContinuousActions[0];
        force.z = actions.ContinuousActions[1];
        m_Rigidbody.AddForce(force * m_ForceMultiplier);

        float distance = Vector3.Distance(transform.localPosition, m_Target.localPosition);
        if (distance < 2.0f)
        {
            AddReward(1.0f);
            EndEpisode();
        }
        else if (transform.localPosition.y < 0)
        {
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = m_HorizontalAction.ReadValue<float>();
        continuousActionsOut[1] = m_VerticalAction.ReadValue<float>();
    }
}
