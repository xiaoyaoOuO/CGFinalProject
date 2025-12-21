using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Animator))]
public class RigidbodyCharacter : MonoBehaviour
{
    [Header("Camera Setup")]
    public Transform cameraRoot;
    public float mouseSensitivity = 2.0f;
    public float topClamp = -40f;
    public float bottomClamp = 70f;

    [Header("Movement Stats")]
    public float moveSpeed = 6.0f;
    public float runBackSpeed = 4.0f; // 后退通常慢一点
    public float jumpForce = 100.0f;
    
    [Header("Ground Detection")]
    public LayerMask groundLayer; // 必须设置，指定哪些层是地面
    public LayerMask snowLayer; // 雪地层
    public LayerMask defaultLayer; // 默认层
    public float groundCheckDistance = 0.2f;
    public float groundCheckRadius = 0.3f; // 检测球半径

    [Header("Input")]
    public KeyCode restKey = KeyCode.R;

    [Header("Interact Material")]
    public Material interactMaterial;
    // 内部变量
    private Rigidbody _rb;
    private Animator _animator;
    private float _cameraPitch;
    private bool _isGrounded;
    private bool _isResting;
    
    // 输入缓存
    private Vector2 _input;
    private bool _jumpInput;

    // 动画ID
    private int _animIDSpeed;
    private int _animIDJump;
    private int _animIDRest;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();

        // 锁定鼠标
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 动画哈希
        _animIDSpeed = Animator.StringToHash("Speed");
        _animIDJump = Animator.StringToHash("Jump");
        _animIDRest = Animator.StringToHash("Rest");
        
        // 自动防止刚体翻倒 (双重保险)
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    void Update()
    {
        // 1. 处理休息状态
        if (Input.GetKeyDown(restKey))
        {
            _isResting = !_isResting;
            _animator.SetBool(_animIDRest, _isResting);
        }

        if (_isResting) return;

        // 2. 读取输入 (在Update中读取输入最灵敏)
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        _input = new Vector2(x, z).normalized; // 归一化防止斜跑加速
        if (Input.GetButtonDown("Jump") && _isGrounded)
        {
            _jumpInput = true;
        }
        if(interactMaterial != null)
        {
            interactMaterial.SetVector("_PositionMoving", transform.position);
        }

            // 3. 处理相机旋转 (视觉相关放Update)
        HandleCameraRotation();
        
        // 4. 更新动画
        UpdateAnimator(z); // 传入原始Z值用于判断前后
    }

    void FixedUpdate()
    {
        if (_isResting)
        {
            _rb.velocity = Vector3.zero;
            return;
        }

        CheckGround();
        Move();
        Jump();
    }

    private void HandleCameraRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // 左右转动角色身体
        // 注意：刚体旋转建议使用MoveRotation，但在Update里做纯视觉旋转直接改Transform更跟手
        transform.Rotate(Vector3.up * mouseX);

        // 上下转动相机节点
        if (cameraRoot != null)
        {
            _cameraPitch -= mouseY;
            _cameraPitch = Mathf.Clamp(_cameraPitch, topClamp, bottomClamp);
            cameraRoot.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
        }
    }

    private void Move()
    {
        // 计算目标移动方向 (基于当前角色朝向)
        // transform.right 是角色右边，transform.forward 是角色前边
        Vector3 targetMoveDir = (transform.right * _input.x + transform.forward * _input.y).normalized;

        float currentSpeed = (_input.y < -0.1f) ? runBackSpeed : moveSpeed;
        
        // 如果没有输入，我们就把水平速度归零，否则角色会像在冰面上一样滑行
        if (_input.magnitude < 0.1f)
        {
            // 保留Y轴速度(重力)，X和Z归零
            _rb.velocity = new Vector3(0, _rb.velocity.y, 0);
        }
        else
        {
            // 设置刚体速度
            // new Vector3(目标X, 保留当前的Y以免打断重力, 目标Z)
            Vector3 targetVelocity = targetMoveDir * currentSpeed;
            _rb.velocity = new Vector3(targetVelocity.x, _rb.velocity.y, targetVelocity.z);
        }
    }

    private void Jump()
    {
        if (_jumpInput)
        {
            // 施加瞬间向上的力
            // 先把当前Y速度清零，保证每次跳起高度一致
            float jumpHeight = 1.0f;
            _rb.velocity = new Vector3(_rb.velocity.x, 0, _rb.velocity.z);
            float gravity = Physics.gravity.y;
            float requiredForce = Mathf.Sqrt(-2 * gravity * jumpHeight);

            _rb.AddForce(Vector3.up * requiredForce, ForceMode.VelocityChange);

            _jumpInput = false;
        
        _jumpInput = false; // 消耗跳跃指令

        }
    }

    private void CheckGround()
    {
        // 从角色底部稍微向上一点的位置，向下发射射线检测
        Vector3 spherePos = transform.position + Vector3.up * 0.1f;
        // 使用 CheckSphere 进行球形检测，比单根射线容错率高
       // _isGrounded = Physics.CheckSphere(spherePos, groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore);
        bool rayCheck = Physics.Raycast(
            spherePos,
            Vector3.down,
            groundCheckRadius + 0.1f,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );
        bool snowCheck = Physics.Raycast(
            spherePos,
            Vector3.down,
            groundCheckRadius + 0.1f,
            snowLayer,
            QueryTriggerInteraction.Ignore
        );
        bool defaultCheck = Physics.Raycast(
            spherePos,
            Vector3.down,
            groundCheckRadius + 0.1f,
            defaultLayer,
            QueryTriggerInteraction.Ignore
        );
        rayCheck = rayCheck || snowCheck || defaultCheck;
        _isGrounded = rayCheck;
    }

    private void UpdateAnimator(float forwardInput)
    {
        // 平滑过渡 Speed 参数
        // 注意：这里用 dampened 值，或者直接传 _input.y，看你想要多快的动画响应
        // 如果用 GetAxisRaw，这里最好用 Damp
        _animator.SetFloat(_animIDSpeed, forwardInput, 0.1f, Time.deltaTime);
        _animator.SetBool(_animIDJump, _jumpInput);
    }
    
    // 调试用的，能在Scene窗口看到地面检测范围
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, groundCheckDistance);
    }
}