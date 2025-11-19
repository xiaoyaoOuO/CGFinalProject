using UnityEngine;

/// <summary>
/// 简单的相机移动与旋转控制脚本。
/// 支持 WASD/方向键 平移、鼠标右键拖拽旋转、鼠标滚轮缩放。
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("移动")]
    [Tooltip("水平/前后移动速度")]
    public float moveSpeed = 10f;
    [Tooltip("加速倍率（按住 LeftShift）")]
    public float boostMultiplier = 2f;

    [Header("旋转")]
    [Tooltip("鼠标灵敏度")]
    public float rotationSensitivity = 3f;
    [Tooltip("俯仰角限制")]
    public Vector2 pitchLimits = new Vector2(-80f, 80f);

    [Header("缩放")]
    [Tooltip("鼠标滚轮缩放速度")]
    public float zoomSpeed = 5f;
    [Tooltip("缩放范围（相机与焦点距离）")]
    public Vector2 zoomRange = new Vector2(2f, 50f);

    private float _yaw;
    private float _pitch;
    private float _currentDistance;

    void Start()
    {
        Vector3 euler = transform.eulerAngles;
        _yaw = euler.y;
        _pitch = euler.x;
        _currentDistance = Vector3.Distance(transform.position, Vector3.zero);
        _currentDistance = Mathf.Clamp(_currentDistance, zoomRange.x, zoomRange.y);
    }

    void Update()
    {
        HandleMovement();
        HandleRotation();
        HandleZoom();
    }

    void HandleMovement()
    {
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            speed *= boostMultiplier;
        }

        float horizontal = Input.GetAxisRaw("Horizontal"); // A/D 或 左/右
        float vertical = Input.GetAxisRaw("Vertical");     // W/S 或 上/下
        float upDown = 0f;

        if (Input.GetKey(KeyCode.E)) upDown += 1f;
        if (Input.GetKey(KeyCode.Q)) upDown -= 1f;

        Vector3 move = (transform.right * horizontal + transform.forward * vertical + Vector3.up * upDown).normalized;
        transform.position += move * speed * Time.deltaTime;
    }

    void HandleRotation()
    {
        if (!Input.GetMouseButton(1)) return; // 鼠标右键按下才旋转

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _yaw += Input.GetAxis("Mouse X") * rotationSensitivity;
        _pitch -= Input.GetAxis("Mouse Y") * rotationSensitivity;
        _pitch = Mathf.Clamp(_pitch, pitchLimits.x, pitchLimits.y);

        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Approximately(scroll, 0f)) return;

        _currentDistance = Mathf.Clamp(_currentDistance - scroll * zoomSpeed, zoomRange.x, zoomRange.y);

        // 直接沿着前方方向进行缩放
        transform.position = transform.position + transform.forward * scroll * zoomSpeed;
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}