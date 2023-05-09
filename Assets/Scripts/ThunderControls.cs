using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class ThunderControls : MonoBehaviour
{
    private Rigidbody _rb;
    [SerializeField] private LayerMask ground;

    [SerializeField] private AnimationCurve animCurve;

    [SerializeField] private float timer;

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


        //test functions that will be removed after bug fixes
        DashIndication();
    }

    #region Speed

    [Header("Movement")] [SerializeField] private Vector2 moveVector;
    [SerializeField] private float maxSpeed, acceleration, currentSpeed;
    [SerializeField] private Transform lookAtTarget;

    public void Move_2D(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            var lastMoveVector = moveVector;
            moveVector = context.ReadValue<Vector2>();
            currentSpeed -= ((lastMoveVector - moveVector).magnitude * currentSpeed / 2) * 0.3f;
            //todo: let player slow down before changing direction drastically
        }
        else if (context.canceled)
        {
            moveVector = new Vector2(0, 0);
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
        else if (!dashedCooldown)
        {
            var right = lookAtTarget.right;
            var forward = lookAtTarget.forward;
            lastInput = right * moveVector.x + forward * moveVector.y;
            var magnitude = lastInput.magnitude;
            lastInput *= 1 / magnitude;
            currentSpeed += Time.fixedDeltaTime * acceleration;
            currentSpeed = Mathf.Clamp(currentSpeed, 0, maxSpeed);
            var localVelocity = _rb.transform.InverseTransformDirection(_rb.velocity);
            Vector3 relativeMove = right * (moveVector.x * currentSpeed) +
                                   forward * (currentSpeed * moveVector.y) 
                                   + lookAtTarget.up * localVelocity.y;
            _rb.velocity = relativeMove;
        }
    }

    #endregion

    #region Camera

    [Header("Camera")] [SerializeField] private GameObject cameraPivot;
    [SerializeField] private GameObject lookAtPivot;
    [SerializeField] private Vector2 cameraDirection;
    [SerializeField] private float cameraSpeed;

    private float _xAxisAngle, _yAxisAngle;

    public void Camera_Move(InputAction.CallbackContext context)
    {
        if (context.performed)
            cameraDirection = context.ReadValue<Vector2>();
        else if (context.canceled)
            cameraDirection = new Vector2(0, 0);
        Debug.Log(cameraPivot.transform.localRotation);
    }

    private void CameraUpdate()
    {
        _xAxisAngle += -cameraDirection.y * cameraSpeed * Time.fixedDeltaTime;
        _yAxisAngle += cameraDirection.x * cameraSpeed * Time.fixedDeltaTime;
        _xAxisAngle = Mathf.Clamp(_xAxisAngle, -15f, 65f);
        cameraPivot.transform.localRotation = Quaternion.Euler(_xAxisAngle, _yAxisAngle, transform.localRotation.z);
        lookAtPivot.transform.localRotation = Quaternion.Euler(0f, _yAxisAngle, transform.localRotation.z);
    }

    #endregion

    #region Jump

    [Header("Jump")] [SerializeField] private float jumpStrength;
    [SerializeField] private float fallStrength;
    [SerializeField] private bool doublejumped;
    [SerializeField] private float gravity;

    public void Jump(InputAction.CallbackContext context)
    {
        if (context.action.triggered && Physics.Raycast(transform.position, -transform.up, 1.1f))
        {
            dashed = false;
            doublejumped = false;
            _rb.velocity += transform.up * jumpStrength;
            //_rb.AddForce(-transform.up * 5f, ForceMode.Impulse);
            Debug.Log("got pressed");
        }
        else if (context.action.triggered && !doublejumped)
        {
            Debug.Log("Also got Pressed but with Pazaaaas");
            _rb.velocity += transform.up * jumpStrength;
            //_rb.AddForce(transform.up*jumpStrength, ForceMode.Impulse);
            doublejumped = true;
        }
    }

    private void RegulateJump()
    {
        _rb.AddForce(-transform.up * gravity);
        if (_rb.velocity.y != 0)
            _rb.AddForce(-transform.up * fallStrength);
    }

    #endregion

    #region GroundRotation

    #endregion

    #region Dash

    [Header("Dash")] [SerializeField] private float dashSpeed;
    [SerializeField] private Vector3 lastInput;
    [SerializeField] private bool dashed, dashedCooldown;


    //todo: remove debugStuff after finishing system and bugfixes
    [SerializeField] private GameObject dashIndicator;


    //this function will use the moveVector from the Speed region 
    public void Dash(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            if (Physics.Raycast(transform.position, -transform.up, 1.1f))
                dashed = false;

            if (dashedCooldown || dashed) return;
            dashed = true;
            Debug.Log("pressed  " + lastInput);
            //uses same logic as in Speed region to move person
            Vector3 relativeDash = lastInput * dashSpeed;
            _rb.velocity = relativeDash;

            //dash adds speed to current speed
            currentSpeed += maxSpeed * 0.8f;
            currentSpeed = Mathf.Clamp(currentSpeed, 0, maxSpeed);

            StartCoroutine(DashCooldown());
        }
    }

    private void DashIndication()
    {
        var position = new Vector3(lastInput.x, 0, lastInput.z) + lookAtPivot.transform.position;
        dashIndicator.transform.position = position;
    }

    public IEnumerator DashCooldown()
    {
        dashedCooldown = true;
        yield return new WaitForSeconds(0.5f);
        dashedCooldown = false;
    }

    #endregion

    public void OnCollisionStay(Collision collisionInfo)
    {
        if (collisionInfo.gameObject.CompareTag("CurvedGround"))
        {
            Ray ray = new Ray(transform.position, -transform.up);
            RaycastHit hit = new RaycastHit();
            Quaternion RotationRef = Quaternion.Euler(0,0,0);
            if (Physics.Raycast(ray, out hit, ground))
            {
                RotationRef = Quaternion.Lerp(transform.rotation,Quaternion.FromToRotation(Vector3.up,
                                    hit.normal), animCurve.Evaluate(timer));
                transform.localRotation = Quaternion.Euler(RotationRef.eulerAngles.x, RotationRef.eulerAngles.y,
                    RotationRef.eulerAngles.z);
            }
        }
    }
}