using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

[RequireComponent(typeof(Rigidbody))]
public class ThunderControls : MonoBehaviour
{
    private Rigidbody rb;
    [SerializeField] private LayerMask ground;

    [SerializeField] private AnimationCurve animCurve;

    [SerializeField] private float timer;

    public void Awake()
    {
        rb = GetComponent<Rigidbody>();
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

    private bool restricted;
    
    public void Move_2D(InputAction.CallbackContext context)
    {
        if (context.performed && !restricted)
        {
            StopCoroutine(BufferMoveVector(new Vector2(0,0)));
            var nextMoveVector = context.ReadValue<Vector2>();
            if (Mathf.Abs(Vector2.Angle(moveVector, nextMoveVector)) > 30f && currentSpeed > maxSpeed*4/5)
            {
                Debug.Log($"Slowing with angle {Vector2.Angle(moveVector, nextMoveVector)}");
                StartCoroutine(SlowDown(0.5f));
                Debug.Log("NOW!!!");
            }
            moveVector = context.ReadValue<Vector2>();
        }
        else if (context.canceled)
        {
            StartCoroutine(BufferMoveVector(new Vector2(0,0)));
        }
    }

    private IEnumerator BufferMoveVector(Vector2 nextMoveVector)
    {
        yield return new WaitForSeconds(0.5f);
        moveVector = nextMoveVector;
    }

    private IEnumerator SlowDown(float duration)
    {
        restricted = true;
        var saveCurrentSpeed = currentSpeed;
        float fixedTimer = 0;
        while (fixedTimer<= duration)
        {
            currentSpeed -= Time.fixedDeltaTime * saveCurrentSpeed / duration;
            fixedTimer += Time.fixedDeltaTime;
            yield return null;
        }
        restricted = false;
        currentSpeed = saveCurrentSpeed;
    }

    private IEnumerator RestrictMovement(float duration)
    {
        restricted = true;
        Debug.Log($"{restricted}");
        yield return new WaitForSeconds(duration);
        Debug.Log($"{restricted}");
        restricted = false;
    }

    private void MovementUpdate()
    {
        if (restricted)
        {
            currentSpeed = rb.velocity.magnitude;
        }
        else if (moveVector == Vector2.zero)
        {
            var velocity = rb.velocity;
            currentSpeed = velocity.magnitude;
            rb.velocity = Vector3.Lerp(velocity, new Vector3(0, 0, 0), Time.fixedDeltaTime);
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
            shotLockLimit = isHoldingShotLock ? Mathf.Lerp(shotLockLimit, 10f, Time.fixedDeltaTime) : maxSpeed;

            currentSpeed = Mathf.Clamp(currentSpeed, 0, isHoldingShotLock ? shotLockLimit : maxSpeed);

            var localVelocity = rb.transform.InverseTransformDirection(rb.velocity);
            
            //wall running in the sense of moving along the wall
            if (againstWall)
            {
                
            }
            
            Vector3 relativeMove = right * (moveVector.x * currentSpeed) +
                                   forward * (currentSpeed * moveVector.y)
                                   + lookAtTarget.up * localVelocity.y;
            rb.velocity = relativeMove;
        }
    }

    private bool grinding;

    [SerializeField] private float grindSpeed;

   
    private void Grinding(Spline spline, Vector3 position)
    {
        grinding = true;
        
        //figure out how to get the 
    }

    #endregion

    #region Camera

    [Header("Camera")] [SerializeField] private GameObject cameraPivot;
    [SerializeField] private GameObject lookAtPivot;
    [SerializeField] private Vector2 cameraDirection;
    [SerializeField] private float cameraSpeed;

    private float xAxisAngle, yAxisAngle;

    public void Camera_Move(InputAction.CallbackContext context)
    {
        if (context.performed)
            cameraDirection = context.ReadValue<Vector2>();
        else if (context.canceled)
            cameraDirection = new Vector2(0, 0);
    }

    private void CameraUpdate()
    {
        xAxisAngle += -cameraDirection.y * cameraSpeed * Time.fixedDeltaTime;
        yAxisAngle += cameraDirection.x * cameraSpeed * Time.fixedDeltaTime;
        xAxisAngle = Mathf.Clamp(xAxisAngle, isHoldingShotLock ? -20f : -15f, 65f);

        cameraPivot.transform.localRotation = Quaternion.Euler(xAxisAngle, yAxisAngle, 0f);
        lookAtPivot.transform.localRotation = Quaternion.Euler(0f, yAxisAngle, 0f);
    }

    #endregion

    #region Jump

    [Header("Jump")] 
    [SerializeField] private float jumpStrength;
    [SerializeField] private float gravity;
    
    private bool doublejumped;
    private bool againstWall;


    public void Jump(InputAction.CallbackContext context)
    {
        if (context.action.triggered && againstWall)
        {
            StartCoroutine(RestrictMovement(1f));
            rb.velocity += Vector3.Normalize(wallNormal + transform.up) * jumpStrength * 2;
            doublejumped = true;
        }
        else if (context.action.triggered && Physics.Raycast(transform.position, -transform.up, 1.1f))
        {
            doublejumped = false;
            var up = transform.up;
            var dif = 0 - rb.velocity.y;
            if(dif > 0)
                up *= 2.5f *  jumpStrength;
            else
                up *= jumpStrength;
            rb.velocity += up;
        }
        else if (context.action.triggered && !doublejumped)
        {
            rb.velocity += transform.up * jumpStrength;
            doublejumped = true;
        }
    }

    private void RegulateJump()
    {
        rb.AddForce(-transform.up * gravity, ForceMode.Acceleration);
    }

    #endregion

    #region Dash

    [Header("Dash")] [SerializeField] private float dashSpeed;
    [SerializeField] private Vector3 lastInput;
    [SerializeField] private bool dashed, dashedCooldown;


    //this function will use the moveVector from the Speed region 
    public void Dash(InputAction.CallbackContext context)
    {
        if (isHoldingShotLock) return;
        if (context.started)
        {
            if (Physics.Raycast(transform.position, -transform.up, 1.1f))
                dashed = false;

            if (dashedCooldown || dashed) return;
            dashed = true;
            //uses same logic as in Speed region to move person
            Vector3 relativeDash = lastInput * dashSpeed;
            rb.velocity = relativeDash;

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

    private bool isHoldingShotLock;

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
            isHoldingShotLock = true;
            StartCoroutine(LerpActionShotLockInput(30f, new Vector3(2, 2, 0), 0f));
        }

        if (context.canceled)
        {
            isHoldingShotLock = false;
            StartCoroutine(LerpActionShotLockInput(60f, new Vector3(0, 0, 0), 2.5f));
        }
    }

    private IEnumerator LerpActionShotLockInput(float newFOV, Vector3 newOffset, float newVal)
    {
        bool firstState = isHoldingShotLock;
        float lerpTimer = 0f;
        while (lerpTimer < cameraZoomSpeed)
        {
            var fov = camera.m_Lens.FieldOfView;
            var lerpFloat = Mathf.Lerp(fov, newFOV, lerpTimer);
            camera.m_Lens.FieldOfView = lerpFloat;

            if (!firstState.Equals(isHoldingShotLock)) yield break;

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

    private bool isOnCurvedGround;

    private Vector3 wallNormal;

    public void OnCollisionStay(Collision collisionInfo)
    {
        if (collisionInfo.gameObject.CompareTag("CurvedGround"))
        {
            isOnCurvedGround = true;
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
            isOnCurvedGround = false;
            var transform1 = transform;
            var reference = Quaternion.Lerp(transform1.rotation, Quaternion.Euler(0, 0, 0),
                animCurve.Evaluate(timer));
            transform.localRotation = Quaternion.Euler(reference.eulerAngles.x, reference.eulerAngles.y,
                reference.eulerAngles.z);
        }
        else if (collisionInfo.gameObject.CompareTag("Wall"))
        {
            doublejumped = false;
            againstWall = true;
            wallNormal = Vector3.zero;
            isOnCurvedGround = false;
            var transform1 = transform;
            var reference = Quaternion.Lerp(transform1.rotation, Quaternion.Euler(0, 0, 0),
                animCurve.Evaluate(timer));
            transform.localRotation = Quaternion.Euler(reference.eulerAngles.x, reference.eulerAngles.y,
                reference.eulerAngles.z);
            rb.velocity += Vector3.down * Time.fixedDeltaTime;
            foreach (var contact in collisionInfo.contacts)
            {
                wallNormal += contact.normal;
            }
        }
    }

    public void OnCollisionExit(Collision other)
    {
        if (other.gameObject.CompareTag("CurvedGround"))
        {
            isOnCurvedGround = false;
            StartCoroutine(ReturnRotation());
        }
        else if (other.gameObject.CompareTag("Wall"))
        {
            againstWall = false;
        }
    }

    private IEnumerator ReturnRotation()
    {
        yield return new WaitForSeconds(0.3f);

        while (!transform.rotation.Equals(Quaternion.Euler(0, 0, 0)))
        {
            if (isOnCurvedGround) yield break;
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