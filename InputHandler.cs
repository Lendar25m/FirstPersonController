using UnityEngine;

public class InputHandler : MonoBehaviour
{
    [Header("Mouse Input Settings")]
    [SerializeField]
    [Range(0, 10)]
    float sensitivity = 3.0f;

    [SerializeField]
    bool invertXAxis = false;

    [SerializeField]
    bool invertYAxis = false;

    [Header("Optional")]
    [SerializeField]
    bool lockCursor = true;

    [SerializeField]
    bool smoothLook = true;

    [SerializeField]
    [Range(0, 2)]
    float mouseSmoothTime = 0.02f;

    Vector2 lookDir;

    Vector2 lookDirVelocity;

    float cameraPitch;

    public void LockCursor()
    {
        Cursor.lockState =
            lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
    }

    public Vector2 MouseLookInput()
    {
        float xInput =
            (
            invertXAxis
                ? (Input.GetAxisRaw("Mouse X") * -1)
                : Input.GetAxisRaw("Mouse X")
            ) *
            sensitivity;
        float yInput =
            (
            invertYAxis
                ? (Input.GetAxisRaw("Mouse Y") * -1)
                : Input.GetAxisRaw("Mouse Y")
            ) *
            sensitivity;

        cameraPitch -= yInput;
        cameraPitch = Mathf.Clamp(cameraPitch, -89.0f, 89.0f);

        Vector2 targetLookDir = new Vector2(xInput, cameraPitch);

        lookDir =
            smoothLook
                ? Vector2
                    .SmoothDamp(lookDir,
                    targetLookDir,
                    ref lookDirVelocity,
                    mouseSmoothTime)
                : targetLookDir;
        return lookDir;
    }

    public Vector3 DirectionInput()
    {
        Vector3 input =
            new Vector3(Input.GetAxisRaw("Vertical"),
                0,
                Input.GetAxisRaw("Horizontal"));
        input = VectorToLocal(input);

        return input;
    }

    Vector3 VectorToLocal(Vector3 vector)
    {
        vector = vector.normalized;
        Vector3 localVector =
            (transform.forward * vector.x + transform.right * vector.z);

        return localVector;
    }

    public bool GetJumpButton()
    {
        return Input.GetKeyDown(KeyCode.Space);
    }

    public bool GetDashButton()
    {
        return Input.GetKeyDown(KeyCode.LeftShift);
    }
}
