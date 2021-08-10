using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Vanguard;

public enum CrouchState {
    None,
    Queued,
    Sneak,
    Slide
}

public enum WallrunState {
    None,
    Left,
    Right,
    Climb
}

public struct WallCheck
{
    public WallrunState State;
    public bool Hit;
    public RaycastHit HitInfo;
}

public class FirstPersonMove : NetworkBehaviour
{
    [Header("Walk Settings")]
    public float speed = 5f;
    public float acceleration = 0.1f;

    private bool isGrounded;
    private Vector3 walkVector;
    private Vector3 immediateWalkVector;
    private RaycastHit groundHit;

    [Header("Jump Settings")]
    public float jumpVelocity = 50f;
    public int maxJumpCount = 2;
    public float airstrafeMultiplier = 0.5f;
    private int jumpCount;

    [Header("Wallrun Settings")]
    public WallrunState wallrunState = WallrunState.None;
    public float baseWallrunSpeed = 4;
    public float wallrunJumpVelocity = 3;

    public float minClimbDistance = 1;
    public AnimationCurve climbVelCurve;
    public float climbVelMultiplier = 4f;
    public float mantleDuration;

    private Vector3 wallNormal;
    private Vector3 wallDirection;
    private float wallrunSpeed;
    private float wallrunTime;
    private bool wallRunEnabled = true;
    private RaycastHit wallHit;
    private float wallrunCheckLength;

    private Transform climbTransform;

    private Vector3 lastMantlePoint;
    private bool lastMantleCheck;
    private Vector3 mantleStartPoint;
    private Vector3 mantleEndPoint;
    private float mantleTime;
    private bool mantling;

    [Header("Crouch Settings")]
    public CrouchState crouchState = CrouchState.None;
    public float slideVelThresh = 2f;
    public float sneakSpeed = 3f;
    public float slideVelBoost = 1.3f;
    public float baseInitialSlideVel = 4f;
    public float minSlideTime = 0.3f;
    public float maxInitialSlideVel = 8f;
    public float slideJumpVelMult = .7f;
    public float crouchColliderHeight = 1.5f;

    public float crouchTime = 0f;

    [Header("Misc")]
    [SerializeField]
    public LayerMask environmentMask;

    private Rigidbody rb;
    private CapsuleCollider collider;
    private FirstPersonLook camManager;

    private Vector2 targetMoveInputVector;
    private Vector3 moveInputVector;
    private bool inputJump;

    public void Jump(InputAction.CallbackContext context) {
        var newValue = context.ReadValue<float>() == 1;

        if (newValue != inputJump) {
            if (newValue == true && jumpCount < maxJumpCount) {
                if (wallrunState != WallrunState.None) {
                    climbTransform = null;
                    Vector3 fwd = transform.forward * wallrunSpeed;
                    if (wallrunState == WallrunState.Climb)
                        fwd *= 0;

                    fwd.y = jumpVelocity;
                    rb.velocity = fwd + (wallNormal * wallrunJumpVelocity);
                    ExitWallrun();
                }
                else {
                    Vector3 jumpDir = rb.velocity;
                    if (Vector3.Distance(immediateWalkVector, Vector3.zero) > 0.5f && crouchState != CrouchState.Slide)
                        jumpDir = immediateWalkVector / speed * rb.velocity.magnitude;
                    jumpDir.y = jumpVelocity;
                    if (crouchState == CrouchState.Slide)
                        jumpDir.y *= slideJumpVelMult;

                    rb.velocity = jumpDir;
                }

                jumpCount++;
            }
        }
        inputJump = newValue;
    }

    private bool inputCrouch;
    public void Crouch(InputAction.CallbackContext context) {
        var newValue = context.ReadValue<float>() == 1;

        if (newValue != inputCrouch) {
            if (newValue == true) {
                if (wallrunState != WallrunState.None) {
                    if (wallrunState == WallrunState.Climb)
                        rb.velocity = Vector3.zero;
                    else
                        rb.velocity = (wallDirection.normalized * rb.velocity.magnitude) + wallNormal;
                    ExitWallrun();
                }
                if (isGrounded) {
                    StartCrouch();
                } else {
                    crouchState = CrouchState.Queued;
                }
            }
            else {
                crouchState = CrouchState.None;
                camManager.targetHeight = 1;
            }
        }
        inputCrouch = newValue;
    }

    void Start()
    {
        if (!isLocalPlayer)
        {
            GetComponent<FirstPersonLook>().pilotActionControls.Disable();
        }
        else
        {
            // TODO: *.performed and *.canceled are mapped to the same function because it's used as a toggle.
            //    We should probably find a better way to handle this (refactor out JumpStart() and JumpEnd()
            //    functions ?)
            GetComponent<FirstPersonLook>().pilotActionControls.VanguardPilot.Jump.performed += Jump;
            GetComponent<FirstPersonLook>().pilotActionControls.VanguardPilot.Jump.canceled += Jump;
            GetComponent<FirstPersonLook>().pilotActionControls.VanguardPilot.Crouch.performed += Crouch;
            GetComponent<FirstPersonLook>().pilotActionControls.VanguardPilot.Crouch.canceled += Crouch;
        }

        rb = GetComponent<Rigidbody>();
        camManager = GetComponent<FirstPersonLook>();
        collider = GetComponent<CapsuleCollider>();
        wallrunCheckLength = collider.radius;
    }

    void FixedUpdate() {
        if (!isLocalPlayer)
        {
            return;
        }

        targetMoveInputVector = GetComponent<FirstPersonLook>().pilotActionControls.VanguardPilot.Walk.ReadValue<Vector2>();
        moveInputVector = Vector2.Lerp(moveInputVector, targetMoveInputVector, Time.fixedDeltaTime * 1/acceleration);
        bool newIsGrounded = Physics.Raycast(transform.position, Vector3.down, out groundHit, (transform.localScale.y * collider.height) * 0.53f, environmentMask);
        if (newIsGrounded != isGrounded) {
            if (newIsGrounded)
                OnEnterGround();
            else
                OnExitGround();
        }
        isGrounded = newIsGrounded;
        WallRunCheck();
        
        Vector3 targetVelocity = rb.velocity;

        walkVector = Vector3.Cross(transform.TransformDirection(new Vector3(moveInputVector.y, 0, -moveInputVector.x)), isGrounded ? groundHit.normal : Vector3.up) * speed;
        immediateWalkVector = Vector3.Cross(transform.TransformDirection(new Vector3(targetMoveInputVector.y, 0, -targetMoveInputVector.x)), isGrounded ? groundHit.normal : Vector3.up) * speed;
        
        if (isGrounded) {
            if (crouchState != CrouchState.Slide) {
                // WALK + SNEAK
                targetVelocity = crouchState == CrouchState.Sneak ? (walkVector / speed) * sneakSpeed : walkVector;
                targetVelocity.y = rb.velocity.y;
            }
        }
        else {
            switch (wallrunState) {
                case WallrunState.None:
                    if (mantling) {
                        // MANTLE
                        mantleTime += Time.fixedDeltaTime;
                        if (mantleTime / mantleDuration >= 1)
                            mantling = false;

                        if (mantleTime / mantleDuration < 0.5f)
                            rb.position = Vector3.Lerp(mantleStartPoint, mantleEndPoint, (mantleTime * 2) / mantleDuration);
                        else
                            rb.position += transform.forward * Time.fixedDeltaTime;
                    }
                    else {
                        // AIRSTRAFE
                        targetVelocity = (rb.velocity + (immediateWalkVector.normalized * airstrafeMultiplier)).normalized * rb.velocity.magnitude;
                        targetVelocity.y = rb.velocity.y;
                    }
                    break;
                case WallrunState.Left:
                case WallrunState.Right:
                    // WALLRUN
                    targetVelocity = -(wallNormal * Vector3.Distance(wallHit.point, transform.position)) + (wallDirection * wallrunSpeed * Mathf.Lerp(1.3f, 1, Mathf.Min(wallrunTime, 1)));
                    targetVelocity.y = rb.velocity.y / 2;
                    break;
                case WallrunState.Climb:
                    // CLIMB
                    if (wallrunTime < climbVelCurve.keys[climbVelCurve.length - 1].time) {
                        targetVelocity = -wallNormal + (Vector3.up * climbVelMultiplier * climbVelCurve.Evaluate(wallrunTime));
                    } else {
                        wallrunState = WallrunState.None;
                        ExitWallrun();
                    }
                    break;
            }
        }

        //Debug.DrawLine(transform.position, transform.position + wallDirection, Color.red);
        if (crouchState != CrouchState.Slide) {
            rb.velocity = targetVelocity;
        }
        else if (crouchTime > minSlideTime){
            Vector3 velocityXZ = Vector3.Scale(rb.velocity, new Vector3(1, 0, 1));
            if (velocityXZ.magnitude < slideVelThresh)
                crouchState = CrouchState.Sneak;
        }

        if (crouchState == CrouchState.Slide || crouchState == CrouchState.Sneak) {
            camManager.targetHeight = 0.5f;
            crouchTime += Time.fixedDeltaTime;
        }

    }

    void WallRunCheck() {
        Vector3 wallrunForwardOffset = transform.forward / 4;

        if (wallRunEnabled && !isGrounded) {
            // REALLY WANT TO FIND A WAY TO DO THIS WITHOUT ALL THE RAYCASTS >:(
            
            RaycastHit rightHit;
            bool rightCheck = (wallrunState == WallrunState.None && Physics.Raycast(transform.position, transform.right - wallrunForwardOffset, out rightHit, wallrunCheckLength, environmentMask)) ||
                Physics.Raycast(transform.position, transform.right + wallrunForwardOffset, out rightHit, wallrunCheckLength, environmentMask) ||
                Physics.Raycast(transform.position, transform.right, out rightHit, wallrunCheckLength, environmentMask);
            
            RaycastHit leftHit;
            bool leftCheck = (wallrunState == WallrunState.None && Physics.Raycast(transform.position, -transform.right - wallrunForwardOffset, out leftHit, wallrunCheckLength, environmentMask)) ||
                Physics.Raycast(transform.position, -transform.right + wallrunForwardOffset, out leftHit, wallrunCheckLength, environmentMask) ||
                Physics.Raycast(transform.position, -transform.right, out leftHit, wallrunCheckLength, environmentMask);
            
            RaycastHit fwdHit;
            bool fwdCheck = Physics.Raycast(transform.position, transform.forward, out fwdHit, wallrunCheckLength, environmentMask) || 
                (wallrunState != WallrunState.None && Physics.Raycast(transform.position - (Vector3.up * ((transform.localScale.y * collider.height) - collider.radius)), transform.forward, out fwdHit, wallrunCheckLength-0.1f, environmentMask));

            List<WallCheck> checks = new List<WallCheck>() {
                new WallCheck() { State = WallrunState.Left, Hit = leftCheck, HitInfo = leftHit },
                new WallCheck() { State = WallrunState.Right, Hit = rightCheck, HitInfo = rightHit },
                new WallCheck() { State = WallrunState.Climb, Hit = fwdCheck, HitInfo = fwdHit },
            };

            if (checks.Any(check => check.Hit)) {
                if (wallrunState == WallrunState.None) {
                    wallrunSpeed = Mathf.Max(rb.velocity.magnitude, baseWallrunSpeed);
                    wallrunTime = 0;
                }
                
                WallrunState oldState = wallrunState;

                // TODO: add camera lock to normal
                foreach (WallCheck check in checks) {
                    if (check.Hit && Mathf.Abs(Vector3.Dot(check.HitInfo.normal, Vector3.up)) < 0.1f) {
                        if (wallrunState == WallrunState.None)
                            wallrunState = check.State;
                        wallNormal = check.HitInfo.normal;
                        wallDirection = Vector3.Cross(wallNormal, Vector3.up);
                        wallHit = check.HitInfo;
                    }
                }
                OnEnterWallrun();
                if (wallrunState != WallrunState.Climb) {
                    wallDirection = Vector3.Cross(wallNormal, Vector3.up);
                    camManager.targetDutch = -15 * Vector3.Dot(transform.forward, wallDirection);
                    wallDirection *= Vector3.Dot(transform.forward, wallDirection);
                }
                else {
                    if (
                        (oldState != WallrunState.None || (climbTransform == null && oldState == WallrunState.None)) &&
                        targetMoveInputVector.y > 0
                    ) {
                        camManager.SetLookEnabled(false);
                        climbTransform = wallHit.transform;
                        camManager.xRotation = Mathf.Lerp(camManager.xRotation, 10, Time.fixedDeltaTime * -10);
                        camManager.cam.transform.rotation = Quaternion.Euler(new Vector3(camManager.xRotation, camManager.yRotation, 0));

                        RaycastHit mantleHit;
                        bool mantleCheck = Physics.Raycast(
                            transform.position + (Vector3.up * (transform.localScale.y * collider.height * 0.4f)), 
                            transform.forward, 
                            out mantleHit, 
                            wallrunCheckLength, 
                            environmentMask
                        );

                        if (!mantleCheck) {
                            if (lastMantleCheck) {
                                wallrunState = WallrunState.None;
                                lastMantlePoint = transform.position;

                                mantling = true;
                                mantleStartPoint = transform.position;
                                mantleEndPoint = new Vector3(transform.position.x, lastMantlePoint.y + (transform.localScale.y * collider.height), transform.position.z) + (transform.forward/10);
                                mantleTime = 0;

                                ExitWallrun();
                            }
                        }
                        else {
                            lastMantlePoint = mantleHit.point;
                            lastMantleCheck = mantleCheck;
                        }
                    }
                    else {
                        wallrunState = WallrunState.None;
                        ExitWallrun();
                    }
                }

                wallrunTime += Time.fixedDeltaTime;
            }
            else {
                if (wallrunState != WallrunState.None) {
                    wallrunState = WallrunState.None;
                    ExitWallrun();
                }
            }
        } 
        else {
            if (wallrunState != WallrunState.None) {
                wallrunState = WallrunState.None;
                ExitWallrun();
            }
        }

        if (wallrunState == WallrunState.None)
            camManager.targetDutch = 0;
    }

    void OnEnterGround() {
        jumpCount = 0;
        climbTransform = null;
        if (crouchState == CrouchState.Queued) {
            StartCrouch();
        }
    }

    void OnExitGround() {
        if (crouchState != CrouchState.None) {
            crouchState = CrouchState.Queued;
            camManager.targetHeight = 1;
        }
    }

    void EnableWallrun() {
        wallRunEnabled = true;
    }

    void OnEnterWallrun() {
        jumpCount = 0;
        //camManager.SetLookEnabled(false);
    }

    void ExitWallrun() {
        wallRunEnabled = false;
        camManager.SetLookEnabled(true);
        Invoke("EnableWallrun", 0.5f);
        //camManager.SetLookEnabled(true);
    }

    void StartCrouch() {
        if (crouchState == CrouchState.Queued) {
            StartSlide();
        } 
        else if (crouchState == CrouchState.None) {
            Vector3 velocityXZ = Vector3.Scale(rb.velocity, new Vector3(1, 0, 1));
            if (velocityXZ.magnitude > slideVelThresh)
                StartSlide();
            else
                crouchState = CrouchState.Sneak;
        }
    }

    void StartSlide() {
        crouchTime = 0;
        crouchState = CrouchState.Slide;
        //rb.velocity = Vector3.Cross(Quaternion.Euler(0, 90, 0) * transform.forward, groundHit.normal).normalized * Mathf.Clamp(rb.velocity.magnitude * slideVelBoost, baseInitialSlideVel, maxInitialSlideVel);
        rb.velocity = Vector3.Cross(
            Vector3.Scale(
                Quaternion.Euler(0, 90, 0) * rb.velocity, 
                new Vector3(1, 0, 1)
            ), groundHit.normal
        ).normalized * Mathf.Clamp(
            Vector3.Scale(rb.velocity, new Vector3(1, 0, 1)).magnitude * slideVelBoost, 
            baseInitialSlideVel, 
            maxInitialSlideVel
        );
    }

    private GUIStyle speedometerStyle = null;
    void OnGUI()
    {
        if (Application.isEditor)
        {
            if (speedometerStyle == null) {
                speedometerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 40,
                    fontStyle = FontStyle.BoldAndItalic
                };
            }
            
            speedometerStyle.normal.textColor = Color.black;
            string text = Mathf.Round(Vector3.Scale(rb.velocity, new Vector3(1, 0, 1)).magnitude * 3.6f).ToString() + " KM/H";
            GUI.Label(new Rect(12, -2, 300, 50), text, speedometerStyle);
            GUI.Label(new Rect(12, 2, 300, 50), text, speedometerStyle);
            GUI.Label(new Rect(8, -2, 300, 50), text, speedometerStyle);
            GUI.Label(new Rect(8, 2, 300, 50), text, speedometerStyle);
            
            speedometerStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(10, 0, 300, 50), text, speedometerStyle);
        }
    }
}
