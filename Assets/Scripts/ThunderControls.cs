using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class ThunderControls : MonoBehaviour
{
    private Rigidbody _rb;


    public void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public void FixedUpdate()
    {
        //Movement
        MovementUpdate();
        //CameraControl
        CameraUpdate();
        //jumpControl
        RegulateJump();
    }

    #region Movement

    [Header("Movement")] [SerializeField] private Vector2 moveVector;
    [SerializeField] private float maxSpeed, acceleration, currentSpeed;
    [SerializeField] private Transform lookAtTarget;

    public void Move_2D(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            moveVector = context.ReadValue<Vector2>();
        }
        else if (context.canceled)
        {
            moveVector = new Vector3(0, 0, 0);
        }
    }

    private void MovementUpdate()
    {
        if (moveVector == Vector2.zero)
        {
            var velocity = _rb.velocity;
            currentSpeed = velocity.magnitude;
            _rb.velocity = Vector3.Lerp(velocity, new Vector3(0, 0, 0), Time.fixedDeltaTime);
        }
        else
        {
            currentSpeed += Time.fixedDeltaTime * acceleration;
            currentSpeed = Mathf.Clamp(currentSpeed, 0, maxSpeed);
            Vector3 relativeMove = lookAtTarget.right * (moveVector.x * currentSpeed) +
                                   lookAtTarget.forward * (currentSpeed * moveVector.y) +
                                   lookAtTarget.up * _rb.velocity.y;
            _rb.velocity = relativeMove;
        }
    }

    #endregion

    #region Camera

    [Header("Camera")] [SerializeField] private GameObject cameraPivot, lookAtPivot;
    [SerializeField] private Vector2 cameraDirection;
    [SerializeField] private float cameraSpeed;

    private float _xAxisAngle, _yAxisAngle;

    public void Camera_Move(InputAction.CallbackContext context)
    {
        if (context.performed)
            cameraDirection = context.ReadValue<Vector2>();
        else if (context.canceled)
            cameraDirection = new Vector2(0, 0);
    }

    private void CameraUpdate()
    {
        _xAxisAngle += -cameraDirection.y * cameraSpeed * Time.fixedDeltaTime;
        _yAxisAngle += cameraDirection.x * cameraSpeed * Time.fixedDeltaTime;
        _xAxisAngle = Mathf.Clamp(_xAxisAngle, -15f, 65f);
        cameraPivot.transform.rotation = Quaternion.Euler(_xAxisAngle, _yAxisAngle, 0f);
        lookAtPivot.transform.rotation = Quaternion.Euler(0f, _yAxisAngle, 0f);
    }

    #endregion

    #region Jump

    [Header("Jump")] [SerializeField] private float jumpStrength, fallStrength;
    [SerializeField] private bool doublejumped;

    public void Jump(InputAction.CallbackContext context)
    {
        if (context.action.triggered && Physics.Raycast(transform.position, -transform.up, 1.1f))
        {
            doublejumped = false;
            _rb.velocity += new Vector3(0f, jumpStrength, 0f);
            //_rb.AddForce(-transform.up * 5f, ForceMode.Impulse);
            Debug.Log("got pressed");
        }
        else if (context.action.triggered && !doublejumped)
        {
            Debug.Log("Also got Pressed but with Pazaaaas");
            _rb.velocity += new Vector3(0f, jumpStrength, 0f);
            //_rb.AddForce(transform.up*jumpStrength, ForceMode.Impulse);
            doublejumped = true;
        }
    }

    private void RegulateJump()
    {
        if(_rb.velocity.y != 0)
            _rb.AddForce(-transform.up * fallStrength);
    }

    #endregion

    #region Collision Handling

    public void OnCollisionStay(Collision collisionInfo)
    {
        if (!collisionInfo.gameObject.CompareTag("Ground"))
        {
            
        }
    }

    #endregion
}