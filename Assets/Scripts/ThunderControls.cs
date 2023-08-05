using System.Collections;
using Cinemachine;
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
    }

    #region Speed

    [Header("Movement")] [SerializeField] private Vector2 moveVector;
    [SerializeField] private float maxSpeed, acceleration, currentSpeed, shotLockLimit;
    [SerializeField] private Transform lookAtTarget;

    //TODO: affect Direction change in air
    public void Move_2D(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            var lastMoveVector = moveVector;
            moveVector = context.ReadValue<Vector2>();
            currentSpeed -= ((lastMoveVector - moveVector).magnitude * currentSpeed / 2) * 0.5f;
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
        else if (!dashedCooldown && !grinding)
        {
            var right = lookAtTarget.right;
            var forward = lookAtTarget.forward;
            lastInput = right * moveVector.x + forward * moveVector.y;
            var magnitude = lastInput.magnitude;
            lastInput *= 1 / magnitude;
            currentSpeed += Time.fixedDeltaTime * acceleration;

            //reduces movement limit during shot lock over time so player won't instantly stop at place
            shotLockLimit = _isHoldingShotLock ? Mathf.Lerp(shotLockLimit, 10f, Time.fixedDeltaTime) : maxSpeed;

            currentSpeed = Mathf.Clamp(currentSpeed, 0, _isHoldingShotLock ? shotLockLimit : maxSpeed);

            var localVelocity = _rb.transform.InverseTransformDirection(_rb.velocity);
            Vector3 relativeMove = right * (moveVector.x * currentSpeed) +
                                   forward * (currentSpeed * moveVector.y)
                                   + lookAtTarget.up * localVelocity.y;
            _rb.velocity = relativeMove;
        }
    }

    private bool grinding;

    [SerializeField] private float grindSpeed;

    public void SetGrind(BezierSplines spline)
    {
        StartCoroutine(Grinding(spline));
    }

    private IEnumerator Grinding(BezierSplines spline)
    {
        grinding = true;
        timer = 0.0f;
        while (timer <= spline.CurveCount / grindSpeed)
        {
            var t = timer * grindSpeed / spline.CurveCount;
            var pos = spline.GetPoint(t);
            transform.localPosition = pos;
            transform.LookAt(pos + spline.GetDirections(t));
            timer += Time.deltaTime;
            yield return null;
        }

        grinding = false;
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
    }

    private void CameraUpdate()
    {
        _xAxisAngle += -cameraDirection.y * cameraSpeed * Time.fixedDeltaTime;
        _yAxisAngle += cameraDirection.x * cameraSpeed * Time.fixedDeltaTime;
        _xAxisAngle = Mathf.Clamp(_xAxisAngle, _isHoldingShotLock ? -20f : -15f, 65f);

        cameraPivot.transform.localRotation = Quaternion.Euler(_xAxisAngle, _yAxisAngle, 0f);
        lookAtPivot.transform.localRotation = Quaternion.Euler(0f, _yAxisAngle, 0f);
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
            var up = transform.up;
            _rb.velocity += up * jumpStrength;
        }
        else if (context.action.triggered && !doublejumped)
        {
            _rb.velocity += transform.up * jumpStrength;
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

    #region Dash

    [Header("Dash")] [SerializeField] private float dashSpeed;
    [SerializeField] private Vector3 lastInput;
    [SerializeField] private bool dashed, dashedCooldown;


    //this function will use the moveVector from the Speed region 
    public void Dash(InputAction.CallbackContext context)
    {
        if (_isHoldingShotLock) return;
        if (context.started)
        {
            if (Physics.Raycast(transform.position, -transform.up, 1.1f))
                dashed = false;

            if (dashedCooldown || dashed) return;
            dashed = true;
            //uses same logic as in Speed region to move person
            Vector3 relativeDash = lastInput * dashSpeed;
            _rb.velocity = relativeDash;

            //dash adds speed to current speed
            currentSpeed += maxSpeed * 0.8f;
            currentSpeed = Mathf.Clamp(currentSpeed, 0, maxSpeed);

            StartCoroutine(DashCooldown());
        }
    }

    private IEnumerator DashCooldown()
    {
        dashedCooldown = true;
        yield return new WaitForSeconds(0.5f);
        dashedCooldown = false;
    }

    #endregion

    #region Target System

    [SerializeField] private int maxDistance;
    [SerializeField] private Collider range;
    [SerializeField] private new CinemachineVirtualCamera camera;
    [SerializeField] private float cameraZoomSpeed;

    private bool _isHoldingShotLock;

    public void ShotLockInput(InputAction.CallbackContext context)
    {
        /*
         * Todo: create collider and shape it inside ShotLockInput
         * Todo: create list and objects to be listed
         * Todo: create movement and options for it
         * Todo: create UI
         */

        if (context.performed)
        {
            _isHoldingShotLock = true;
            StartCoroutine(LerpActionShotLockInput(30f, new Vector3(2, 2, 0), 0f));
        }

        if (context.canceled)
        {
            _isHoldingShotLock = false;
            StartCoroutine(LerpActionShotLockInput(60f, new Vector3(0, 0, 0), 2.5f));
        }
    }

    private IEnumerator LerpActionShotLockInput(float newFOV, Vector3 newOffset, float newVal)
    {
        bool firstState = _isHoldingShotLock;
        float lerpTimer = 0f;
        while (lerpTimer < cameraZoomSpeed)
        {
            var fov = camera.m_Lens.FieldOfView;
            var lerpFloat = Mathf.Lerp(fov, newFOV, lerpTimer);
            camera.m_Lens.FieldOfView = lerpFloat;

            if (!firstState.Equals(_isHoldingShotLock)) yield break;

            var cine3RdPerson = camera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();

            var offset = cine3RdPerson.ShoulderOffset;
            var lerpVector = Vector3.Lerp(offset, newOffset, lerpTimer);
            camera.GetComponentInChildren<Cinemachine3rdPersonFollow>().ShoulderOffset = lerpVector;

            var vertArmLength = cine3RdPerson.VerticalArmLength;
            var lerpLength = Mathf.Lerp(vertArmLength, newVal, lerpTimer);
            camera.GetCinemachineComponent<Cinemachine3rdPersonFollow>().VerticalArmLength = lerpLength;

            lerpTimer += Time.fixedDeltaTime;
            yield return null;
        }

        camera.m_Lens.FieldOfView = newFOV;
        camera.GetComponentInChildren<Cinemachine3rdPersonFollow>().ShoulderOffset = newOffset;
    }

    #endregion

    #region Position and Rotation

    private bool _isOnCurvedGround;

    public void OnCollisionStay(Collision collisionInfo)
    {
        if (collisionInfo.gameObject.CompareTag("CurvedGround"))
        {
            _isOnCurvedGround = true;
            var transform1 = transform;
            Ray ray = new Ray(transform1.position, -transform1.up);
            if (Physics.Raycast(ray, out var hit, ground))
            {
                var rotationRef = Quaternion.Lerp(transform.rotation, Quaternion.FromToRotation(Vector3.up,
                    hit.normal), animCurve.Evaluate(timer));
                transform.localRotation = Quaternion.Euler(rotationRef.eulerAngles.x, rotationRef.eulerAngles.y,
                    rotationRef.eulerAngles.z);
            }
        }
        else if (collisionInfo.gameObject.CompareTag("Ground"))
        {
            _isOnCurvedGround = false;
            var transform1 = transform;
            var reference = Quaternion.Lerp(transform1.rotation, Quaternion.Euler(0, 0, 0),
                animCurve.Evaluate(timer));
            transform.localRotation = Quaternion.Euler(reference.eulerAngles.x, reference.eulerAngles.y,
                reference.eulerAngles.z);
        }
    }

    public void OnCollisionExit(Collision other)
    {
        if (other.gameObject.CompareTag("CurvedGround"))
        {
            _isOnCurvedGround = false;
            StartCoroutine(ReturnRotation());
        }
    }

    private IEnumerator ReturnRotation()
    {
        yield return new WaitForSeconds(2f);

        while (!transform.rotation.Equals(Quaternion.Euler(0, 0, 0)))
        {
            if (_isOnCurvedGround) yield break;
                var transform1 = transform;
            var reference = Quaternion.Lerp(transform1.rotation, Quaternion.Euler(0, 0, 0),
                animCurve.Evaluate(timer));
            transform.localRotation = Quaternion.Euler(reference.eulerAngles.x, reference.eulerAngles.y,
                reference.eulerAngles.z);
            yield return null;
        }
    }

    #endregion
}