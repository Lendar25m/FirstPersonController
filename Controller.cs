using System.Linq;
using UnityEngine;

[RequireComponent(typeof (CharacterController), typeof (InputHandler))]
public class Controller : MonoBehaviour
{

#region Fields
    [SerializeField]
    Transform playerCamera;

    [Header("Ground movement values")]
    [SerializeField]
    float maxSpeedOnGround = 12.0f;

    [SerializeField]
    float groundAccelerationTime = 0.06f;

    [Header("In-air movement values")]
    [SerializeField]
    float maxSpeedInAir = 10.0f;

    [SerializeField]
    float accelerationInAir = 43.0f;

    [SerializeField]
    float gravityDownForce = 20.0f;

    [SerializeField]
    float jumpForce = 13.0f;

    [SerializeField]
    bool allowDoubleJumps = true;

    [Header("Dashing")]
    [SerializeField]
    float dashingDuration = 0.2f;

    [SerializeField]
    float dashTimeDelay = 0.7f;

    [SerializeField]
    float maxSpeedWhileDashing = 20.0f;

    [SerializeField]
    bool onlyForwardDashingDirection = false;

    [Header("Wallrunning")]
    [SerializeField]
    float wallMaxDistance = 1.0f;

    [SerializeField]
    float minimumHeight = 1.2f;

    [SerializeField]
    float wallrunGravityDownForce = 2.0f;

    [SerializeField]
    float wallSpeedMultiplier = 1.2f;

    [SerializeField]
    float wallBouncing = 3.0f;

    [Range(0, 1)]
    [SerializeField]
    float normalizedAngleThreshold = 1.0f;

    [SerializeField]
    float WallrunStickPreventionTime = 0.1f;

    [Range(0, 1)]
    [SerializeField]
    float positionAdd = 0.5f;

    [Header("Ground Checking")]
    [SerializeField]
    float groundCheckDistance = 0.05f;

    [SerializeField]
    LayerMask groundCheckLayers = 1;

    const float JumpGroundingPreventionTime = 0.2f;

    bool _CanAirJump;

    bool _IsGrounded;

    float m_LastTimeJumped = 0.0f;

    float m_LastTimeDashed = 0.0f;

    InputHandler m_InputHandler;

    CharacterController m_Controller;

    Vector3 m_PlayerInput;

    Vector3 m_CharacterVelocity;

    Vector3 m_DashingDirection;

    Vector3 m_GroundNormal = Vector3.up;

    Vector3 smoothReference;

    enum Mode
    {
        Walking,
        Flying,
        Dashing,
        Wallrunning
    }

    Mode m_PlayerState = Mode.Flying;

    Vector3[] directions;

    RaycastHit[] hits;

    RaycastHit lastWallRayCastHit;
#endregion



#region ScriptBody
    void Start()
    {
        m_InputHandler = GetComponent<InputHandler>();
        m_Controller = GetComponent<CharacterController>();
        directions =
            new Vector3[] {
                Vector3.right,
                Vector3.left,
                Vector3.right + Vector3.forward,
                Vector3.left + Vector3.forward
            };
    }

    void Update()
    {
        m_InputHandler.LockCursor();
        GroundCheck();
        m_PlayerState = StateChange();
        HandleMovement();
    }
#endregion



#region GroundChecking
    void GroundCheck()
    {
        _IsGrounded = false;
        m_GroundNormal = Vector3.up;

        if (Time.time >= m_LastTimeJumped + JumpGroundingPreventionTime)
        {
            if (
                Physics
                    .CapsuleCast(GetCapsuleBottomHemisphere(),
                    GetCapsuleTopHemisphere(m_Controller.height),
                    m_Controller.radius,
                    Vector3.down,
                    out RaycastHit hit,
                    groundCheckDistance + m_Controller.skinWidth,
                    groundCheckLayers,
                    QueryTriggerInteraction.Ignore)
            )
            {
                m_GroundNormal = hit.normal;
                if (
                    Vector3.Dot(m_GroundNormal, Vector3.up) > 0f &&
                    IsNormalUnderSlopeLimit(m_GroundNormal)
                )
                {
                    _IsGrounded = true;
                    if (hit.distance > m_Controller.skinWidth)
                    {
                        m_Controller.Move(Vector3.down * hit.distance);
                    }
                }
            }
        }
    }
#endregion



#region StateChanging
    Mode StateChange()
    {
        if (m_PlayerState != Mode.Dashing)
        {
            if (_IsGrounded)
            {
                m_PlayerState = Mode.Walking;
            }
            else
            {
                m_PlayerState = Mode.Flying;
            }
            if (CanWallRun())
            {
                m_PlayerState = Mode.Wallrunning;
            }

            if (
                m_InputHandler.GetDashButton() &&
                Time.time > m_LastTimeDashed + dashTimeDelay
            )
            {
                m_LastTimeDashed = Time.time;
                m_DashingDirection =
                    (
                    m_PlayerInput != Vector3.zero &&
                    !onlyForwardDashingDirection
                    )
                        ? m_PlayerInput
                        : transform.forward;
                m_PlayerState = Mode.Dashing;
            }
        }
        print (m_PlayerState);
        return m_PlayerState;
    }
#endregion



#region Movement
    void HandleMovement()
    {
        {
            Vector2 look = m_InputHandler.MouseLookInput();
            transform.Rotate(new Vector3(0, look.x, 0));
            playerCamera.localEulerAngles = new Vector3(look.y, 0, 0);
        }

        {
            m_PlayerInput = m_InputHandler.DirectionInput();
            switch (m_PlayerState)
            {
                case Mode.Walking:
                    Walk();
                    _CanAirJump = true;
                    if (m_InputHandler.GetJumpButton())
                    {
                        Jump(Vector3.up);
                    }
                    break;
                case Mode.Flying:
                    Fly();
                    if (
                        m_InputHandler.GetJumpButton() &&
                        _CanAirJump &&
                        allowDoubleJumps
                    )
                    {
                        Jump(Vector3.up);
                        _CanAirJump = false;
                    }
                    break;
                case Mode.Dashing:
                    Dash();
                    break;
                case Mode.Wallrunning:
                    WallRun (lastWallRayCastHit);
                    _CanAirJump = true;
                    if (m_InputHandler.GetJumpButton())
                    {
                        Jump(GetWallJumpDirection());
                    }
                    break;
            }
            m_Controller.Move(m_CharacterVelocity * Time.deltaTime);
        }
    }

    void Walk()
    {
        Vector3 targetVelocity = m_PlayerInput * maxSpeedOnGround;
        targetVelocity =
            GetDirectionReorientedOnSlope(targetVelocity.normalized,
            m_GroundNormal) *
            targetVelocity.magnitude;

        m_CharacterVelocity =
            Vector3
                .SmoothDamp(m_CharacterVelocity,
                targetVelocity,
                ref smoothReference,
                groundAccelerationTime);
    }

    void Fly()
    {
        m_CharacterVelocity +=
            m_PlayerInput * accelerationInAir * Time.deltaTime;

        float verticalVelocity = m_CharacterVelocity.y;
        Vector3 horizontalVelocity =
            Vector3.ProjectOnPlane(m_CharacterVelocity, Vector3.up);
        horizontalVelocity =
            Vector3.ClampMagnitude(horizontalVelocity, maxSpeedInAir);
        m_CharacterVelocity =
            horizontalVelocity + (Vector3.up * verticalVelocity);

        m_CharacterVelocity += Vector3.down * gravityDownForce * Time.deltaTime;
    }

    void Jump(Vector3 jumpDirection)
    {
        m_CharacterVelocity.y = 0;
        m_CharacterVelocity += jumpDirection * jumpForce;
        m_LastTimeJumped = Time.time;
    }

    void Dash()
    {
        m_PlayerState =
            (Time.time < m_LastTimeDashed + dashingDuration)
                ? Mode.Dashing
                : Mode.Flying;

        m_CharacterVelocity = m_DashingDirection * maxSpeedWhileDashing;
    }

    void WallRun(RaycastHit hit)
    {
        float d = Vector3.Dot(hit.normal, Vector3.up);
        if (d >= -normalizedAngleThreshold && d <= normalizedAngleThreshold)
        {
            // Vector3 alongWall = Vector3.Cross(hit.normal, Vector3.up);
            Vector3 alongWall = transform.TransformDirection(Vector3.forward);
            m_CharacterVelocity =
                alongWall * wallSpeedMultiplier * maxSpeedOnGround +
                Vector3.down * wallrunGravityDownForce * Time.deltaTime * 10;
        }
    }
#endregion



#region Other
    bool CanWallRun()
    {
        hits = new RaycastHit[directions.Length];
        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 dir = transform.TransformDirection(directions[i]);
            Physics
                .Raycast(transform.position + (Vector3.up * positionAdd),
                dir,
                out hits[i],
                wallMaxDistance);
        }

        hits =
            hits
                .ToList()
                .Where(h => h.collider != null)
                .OrderBy(h => h.distance)
                .ToArray();

        lastWallRayCastHit = (hits.Length > 0) ? hits[0] : lastWallRayCastHit;

        return hits.Length > 0 &&
        (m_PlayerState == Mode.Flying) &&
        Input.GetKey(KeyCode.W) &&
        !Physics.Raycast(transform.position, Vector3.down, minimumHeight) &&
        Time.time >= m_LastTimeJumped + WallrunStickPreventionTime &&
        lastWallRayCastHit.collider.tag == "Allow WallRun";
    }

    Vector3 GetWallJumpDirection()
    {
        return lastWallRayCastHit.normal * wallBouncing + Vector3.up;
    }

    Vector3
    GetDirectionReorientedOnSlope(Vector3 direction, Vector3 slopeNormal)
    {
        Vector3 directionRight = Vector3.Cross(direction, transform.up);
        return Vector3.Cross(slopeNormal, directionRight).normalized;
    }

    bool IsNormalUnderSlopeLimit(Vector3 normal)
    {
        return Vector3.Angle(transform.up, normal) <= m_Controller.slopeLimit;
    }

    Vector3 GetCapsuleBottomHemisphere()
    {
        return transform.position - (transform.up * m_Controller.radius);
    }

    Vector3 GetCapsuleTopHemisphere(float atHeight)
    {
        return transform.position +
        (transform.up * (atHeight - m_Controller.radius));
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (m_PlayerState == Mode.Flying)
            m_CharacterVelocity =
                Vector3.ProjectOnPlane(m_CharacterVelocity, hit.normal);

        // if (m_PlayerState == Mode.Dashing)
        //     m_PlayerState = (!_IsGrounded) ? Mode.Flying : Mode.Dashing;
    }
#endregion
}
