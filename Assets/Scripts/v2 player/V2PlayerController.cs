using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GlobalTypes;
using UnityEngine.InputSystem;

public class V2PlayerController : MonoBehaviour
{
    #region variables/properties
    #region public properties
    public GameObject spriteObject;
    SpriteRenderer spriteRenderer;

    [Header("Player Properties")]
    [Header("Input")]
    public float deadzoneValue;
    public float coyoteTime;
    public float jumpBuffer;

    [Header("Drag")]
    public float minWalkingDrag;
    public float maxWalkingDrag;
    public float maxUsedWalkSpeed;
    [Space]
    public float minRunningDrag;
    public float maxRunningDrag;
    public float maxUsedRunSpeed;
    [Space]
    public float slideDefaultDrag;

    [Header("xMovement")]
    public float walkAccelleration;
    public float maxWalkSpeed;
    public float runAccelleration;
    public float maxRunSpeed;
    public float pivotTime;
    public float creepSpeed;

    [Header("Jumping")]
    public float jumpSpeed;
    public float completeAirControlTime;
    public float reducedAirControlPivotTime;

    [Header("Wall Jumping")]
    public float xWallJumpSpeed;
    public float yWallJumpSpeed;
    public float wallSlideAmount;
    public float wallJumpedNullTime;

    [Header("Sliding")]
    public bool applyDeccelInAir;
    public float minSpeedUsedSlideDeccel;
    public float maxSpeedUsedSlideDeccel;
    public float minSlideDeccel;
    public float maxSlideDeccel;
    public float slideHopPower;
    public float slideHopSlopePower;
    public float requiredMinimumVelocity;

    [Header("Physics")]
    public float gravity;
    public float peakGravity;

    [Header("Other")]
    public float swimSpeed;
    public float doubleJumpSpeed;
    public float wallRunAmount = 8f;
    public float glideTime = 2f;
    public float glideDescentAmount = 2f;
    public float powerJumpSpeed = 40f;
    public float powerJumpWaitTime = 1.5f;
    public float dashSpeed = 20f;
    public float dashTime = 0.2f;
    public float dashCooldownTime = 1f;
    public float groundSlamSpeed = 60f;

    [Space]
    [Space]
    [Space]
    [Space]
    [Header("Player Abilities")]
    public bool canDoubleJump;
    public bool canTripleJump;
    public bool canWallJump;
    public bool canJumpAfterWallJump;
    public bool canWallRun;
    public bool canMultipleWallRun;
    public bool canWallSlide;
    public bool canGlide;
    public bool canGlideAfterWallContact;
    public bool canPowerJump;
    public bool canGroundDash;
    public bool canAirDash;
    public bool canGroundSlam;
    public bool canSlide;
    public bool canSwim;

    [Space]
    [Space]
    [Space]
    [Space]
    [Header("Player State")]
    public bool isSwimming;
    public bool isRunning;
    public bool isJumping;
    public bool isDoubleJumping;
    public bool isTripleJumping;
    public bool isWallJumping;
    public bool isWallRunning;
    public bool isWallSliding;
    public bool isDucking;
    public bool isCreeping;
    public bool isGliding;
    public bool isPowerJumping;
    public bool isDashing;
    public bool isGroundSlamming;
    public bool isInAntiGrav;
    public bool isSliding;
    public bool isSlideJumping;
    #endregion

    #region private properties
    float completeAirControlTimer;

    [HideInInspector] public float jumpBufferTimer;
    [HideInInspector] public float coyoteTimer;

    bool slideFrameOne = false;

    //input flags
    private bool _startJump;
    private bool _releaseJump;
    bool _holdJump;

    private Vector2 _input;
    private V2CharacterController2D _characterController;

    private bool _ableToWallRun = true;

    private CapsuleCollider2D _capsuleCollider;
    private Vector2 _originalColliderSize;
    //TODO: remove later when not needed
    private SpriteRenderer _spriteRenderer;

    private float _currentGlideTime;
    private bool _startGlide = true;

    private float _powerJumpTimer;

    private bool _facingRight;
    private float _dashTimer;

    Vector2 velocity = Vector2.zero;
    #endregion

    [HideInInspector] public Vector2 _moveDirection;
    #endregion

    #region Execution
    void Start()
    {
        _characterController = gameObject.GetComponent<V2CharacterController2D>();
        _capsuleCollider = gameObject.GetComponent<CapsuleCollider2D>();

        spriteRenderer = spriteObject.GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        RunInput();
        SlideInputAndState();
        InputBufferingAndCoyote();

        if (isInAntiGrav == false)
        {
            if (_dashTimer > 0)
                _dashTimer -= Time.deltaTime;

            ApplyDeadzones();

            SlidingAndCreeping();

            if (isSliding == false && isCreeping == false && isSwimming == false) { ProcessHorizontalMovement(); }

            if (isSliding == false && isSwimming == false) { Jump(); } // was removed from onground (it has to be like this for coyote time) (cause you're not grounded but can still jump)

            if (_characterController.below) //On the ground
            {
                OnGround();
            }
            else if (_characterController.inWater == true)
            {
                InWater();
            }
            else //In the air
            {
                InAir();
            }

            if (isCreeping == false) { Drag(); }


            _characterController.Move(_moveDirection);
            // Changed (it use to multiply the move direction by time.deltatime)
            // the reason it can't do that is because if we have velocity based movements multiplying it by time.deltatime will also then multiply the current velocity by that
            // what this means is everything (that's supposed to be timescaled) has to be multiplied by time.deltatime individually
            // this has already been implimented in this script
            // this isn't perfect, it would need to be a fixed update for it to be correct
            // it's not detrimental though
        }
    }
    #endregion

    #region Movement 
    #region General Movement
    private void ProcessHorizontalMovement()
    {
        if (!isWallJumping)
        {
            //_moveDirection.x = _input.x; //- Removed

            if (_input.x < 0) // changed
            {
                //transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                spriteRenderer.flipX = true;
                _facingRight = false;
            }
            else if (_input.x > 0) // changed
            {
                //transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                spriteRenderer.flipX = false;
                _facingRight = true;
            }

            if (isDashing)
            {
                if (_facingRight)
                {
                    _moveDirection.x = dashSpeed;
                }
                else
                {
                    _moveDirection.x = -dashSpeed;
                }
                _moveDirection.y = 0;
            }
            else
            {
                //_moveDirection.x *= walkAccelleration; //- Removed

                //new from here
                _moveDirection.x = _characterController._moveVelocity.x;

                // pivot
                if (_characterController.below == true || completeAirControlTimer > 0)
                {
                    if (_input.x > 0 && _characterController._moveVelocity.x < 0)
                    {
                        _moveDirection.x = Vector2.SmoothDamp(_characterController._moveVelocity, new Vector2(0, _characterController._moveVelocity.y), ref velocity, pivotTime).x;
                    }
                    if (_input.x < 0 && _characterController._moveVelocity.x > 0)
                    {
                        _moveDirection.x = Vector2.SmoothDamp(_characterController._moveVelocity, new Vector2(0, _characterController._moveVelocity.y), ref velocity, pivotTime).x;
                    }
                    if (_input.x == 0 && _characterController.below == true)
                    {
                        _moveDirection.x = Vector2.SmoothDamp(_characterController._moveVelocity, new Vector2(0, _characterController._moveVelocity.y), ref velocity, pivotTime).x;
                    }
                }
                else // reduced pivot
                {
                    if (_input.x > 0 && _characterController._moveVelocity.x < 0)
                    {
                        _moveDirection.x = Vector2.SmoothDamp(_characterController._moveVelocity, new Vector2(0, _characterController._moveVelocity.y), ref velocity, reducedAirControlPivotTime).x;
                    }
                    if (_input.x < 0 && _characterController._moveVelocity.x > 0)
                    {
                        _moveDirection.x = Vector2.SmoothDamp(_characterController._moveVelocity, new Vector2(0, _characterController._moveVelocity.y), ref velocity, reducedAirControlPivotTime).x;
                    }
                    if (_input.x == 0 && _characterController.below == true)
                    {
                        _moveDirection.x = Vector2.SmoothDamp(_characterController._moveVelocity, new Vector2(0, _characterController._moveVelocity.y), ref velocity, reducedAirControlPivotTime).x;
                    }
                }

                // movement
                float timeScaledWalkAccelleration = walkAccelleration * Time.deltaTime;
                if (Mathf.Abs(_characterController._moveVelocity.x) < maxWalkSpeed)
                {
                    _moveDirection.x = _moveDirection.x + (timeScaledWalkAccelleration * _input.x);
                }

                if (isRunning == true)
                {
                    float timeScaledRunAccelleration = runAccelleration * Time.deltaTime;
                    if (Mathf.Abs(_characterController._moveVelocity.x) < maxRunSpeed)
                    {
                        _moveDirection.x = _moveDirection.x + (timeScaledRunAccelleration * _input.x);
                    }
                }
                // to here
            }
        }
    }

    private void Jump()
    {
        //jumping
        if (jumpBufferTimer > 0 && coyoteTimer > 0)
        {
            _startJump = false;
            jumpBufferTimer = 0;
            coyoteTimer = 0;

            if (canPowerJump && isDucking &&
                _characterController.groundType != GroundType.OneWayPlatform && (_powerJumpTimer > powerJumpWaitTime))
            {
                _moveDirection.y = powerJumpSpeed;
                StartCoroutine("PowerJumpWaiter");
            }
            //check to see if we are on a one way platform
            else if (isDucking && _characterController.groundType == GroundType.OneWayPlatform)
            {
                StartCoroutine(DisableOneWayPlatform(true));
            }
            else
            {
                _moveDirection.y = jumpSpeed;
            }

            isJumping = true;
            _characterController.DisableGroundCheck();
            _characterController.ClearMovingPlatform();
            _ableToWallRun = true;
        }
    }

    void Drag()
    {
        int direction = 1;
        if (_characterController._moveVelocity.x < 0) { direction = -1; }

        if (isSliding == true)
        {
            _moveDirection.x = _moveDirection.x - (slideDefaultDrag * Time.deltaTime * direction);
        }
        else if (isRunning == false) // iswalking true
        {
            float t = Mathf.Clamp(Mathf.Abs(_characterController._moveVelocity.x), 0.0001f, maxUsedWalkSpeed);
            t = t / maxUsedWalkSpeed;

            float dragLerp = Mathf.Lerp(minWalkingDrag, maxWalkingDrag, t);
            dragLerp = dragLerp * Time.deltaTime;

            _moveDirection.x = _moveDirection.x - (dragLerp * direction);
        }
        else if (isRunning == true)
        {
            float t = Mathf.Clamp(Mathf.Abs(_characterController._moveVelocity.x), 0.0001f, maxUsedRunSpeed);
            t = t / maxUsedRunSpeed;

            float dragLerp = Mathf.Lerp(minRunningDrag, maxRunningDrag, t);
            dragLerp = dragLerp * Time.deltaTime;

            _moveDirection.x = _moveDirection.x - (dragLerp * direction);
        }

        // move velocity = itself minus some drag
        // for running and walking it is lerp (faster you are the stronger it is, to a certain point)
    }
    #endregion

    void InWater()
    {
        ClearGroundAbilityFlags();

        // need ability to jump and leave water even if not on ground
        AirJump();

        if (_input.y != 0f && canSwim && !_holdJump)
        {
            if (_input.y > 0 && !_characterController.isSubmerged)
            {
                _moveDirection.y = 0f;
            }
            else
            {
                // smooth motion (disregards frame rate)
                _moveDirection.y = (_input.y * swimSpeed) * Time.deltaTime;
            }

        }

        // natural water behaviour 
        else if (_moveDirection.y < 0 && _input.y == 0f) // if going down and no player input
        {
            ///add own upward force for every frame + no up or down key pressed
            _moveDirection.y += 2f;
        }

        if (_characterController.isSubmerged && canSwim)
        {
            isSwimming = true;
        }
        else
        {
            isSwimming = false;
        }
    }

    void SlidingAndCreeping()
    {
        if (isSliding == true && isSwimming == false)
        {
            if (slideFrameOne == true)
            {
                _moveDirection = _characterController.actualVeclocity;
                slideFrameOne = false;
            }

            ChangeSlidingSprite(true);

            SlideMovement();
            SlideHop();
            SlideGravity();
        }
        else if (isCreeping == true)
        {
            if (slideFrameOne == true)
            {
                slideFrameOne = false;
            }

            ChangeSlidingSprite(true);

            Creeping();
        }
        else
        {
            ChangeSlidingSprite(false);
        }

        SpriteRotater();
    }
    #region Sliding And Creeping
    void SlideMovement()
    {
        if (isSliding == true)
        {
            int direction = 1;
            if (_characterController._moveVelocity.x < 0) { direction = -1; }

            float speedUsed = Mathf.Abs(_characterController.actualVeclocity.x);
            if (speedUsed < minSpeedUsedSlideDeccel)
            {
                speedUsed = minSpeedUsedSlideDeccel;
            }
            speedUsed = Mathf.Clamp(speedUsed, 0.001f, maxSpeedUsedSlideDeccel);
            float t = speedUsed / maxSpeedUsedSlideDeccel;
            float lerpDrag = Mathf.Lerp(minSlideDeccel, maxSlideDeccel, t);

            if (_characterController.slidingColBelow == true && _characterController.below == true)
            {
                _moveDirection.x = _characterController.actualVeclocity.x - (direction * lerpDrag * Time.deltaTime);
            }

            // stop if wall
            if (_characterController.slidingLeftNormal == Vector2.right && _characterController.actualVeclocity.x < 0)
            {
                _moveDirection.x = 0;
            }
            if (_characterController.slidingRightNormal == Vector2.left && _characterController.actualVeclocity.x > 0)
            {
                _moveDirection.x = 0;
            }
        }
    }

    void SlideHop()
    {
        if (jumpBufferTimer > 0 && coyoteTimer > 0)
        {
            _startJump = false;
            jumpBufferTimer = 0;
            coyoteTimer = 0;

            //normal jump
            _moveDirection.y = slideHopPower;

            //push away from slope
            _moveDirection = _moveDirection + (slideHopSlopePower * _characterController.slidingBelowNormal);

            isSlideJumping = true;
            _characterController.DisableGroundCheck();
            _characterController.ClearMovingPlatform();
        }
    }

    void SlideGravity()
    {
        if (_characterController.below == true)
        {
            _moveDirection.y = _characterController.actualVeclocity.y;
        }
        _moveDirection.y = _moveDirection.y - (gravity * Time.deltaTime);
    }

    void ChangeSlidingSprite(bool sliding)
    {
        // in the future it is better to use animation system over changing sprite

        if (sliding == true)
        {
            spriteRenderer.sprite = Resources.Load<Sprite>("directionSpriteUp_crouching");
            _capsuleCollider.enabled = false;
        }
        else
        {
            spriteRenderer.sprite = Resources.Load<Sprite>("Player_normal");
            _capsuleCollider.enabled = true;
        }
    }

    void SpriteRotater()
    {
        // it would be nice to add somekind of visual drag to this so it doesn't snap, maybe

        if (isSliding == true)
        {
            if (_characterController.slidingColBelow == true)
            {
                spriteObject.transform.eulerAngles = new Vector3(0, 0, -_characterController.slidingBelowAngle);
            }
            else
            {
                spriteObject.transform.eulerAngles = new Vector3(0, 0, 0);
            }
        }
        else
        {
            spriteObject.transform.eulerAngles = new Vector3(0, 0, 0);
        }
    }

    void Creeping()
    {
        if (_input.x < 0) // changed
        {
            //transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            spriteRenderer.flipX = true;
            _facingRight = false;
        }
        else if (_input.x > 0) // changed
        {
            //transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            spriteRenderer.flipX = false;
            _facingRight = true;
        }

        _moveDirection.x = _input.x * creepSpeed;
    }
    #endregion

    void OnGround()
    {
        completeAirControlTimer = completeAirControlTime;

        ClearAirAbilityFlags();

        if (isSliding == false)
        {
            _moveDirection.y = 0;
        }
    }
    #region On Ground
    public void ClearAirAbilityFlags() // CHANGED to public (this is so the antigrav can interact with it)
    {
        //clear flags for in air abilities
        isSlideJumping = false;
        isJumping = false;
        isDoubleJumping = false;
        isTripleJumping = false;
        isWallJumping = false;
        _currentGlideTime = glideTime;
        isGroundSlamming = false;
        _startGlide = true;
    }

    bool CapsuleGrounded()
    {
        Vector3 bottomCirclePosition = transform.position + new Vector3(_capsuleCollider.offset.x, -(_capsuleCollider.offset.y / 2));

        RaycastHit2D circleCast = Physics2D.CircleCast(bottomCirclePosition, _capsuleCollider.size.x, Vector2.zero, 0, _characterController.layerMask);
        if (circleCast.collider != null)
        {
            Debug.Log("true");
            return true;
        }
        else
        {
            return false;
        }
    }
    #endregion 

    void InAir()
    {
        completeAirControlTimer = completeAirControlTimer - Time.deltaTime;

        ClearGroundAbilityFlags();

        if (isSliding == false && isCreeping == false)
        {
            AirJump();

            WallRunning();

            GravityCalculations();
        }
    }
    #region In Air
    public void ClearGroundAbilityFlags() // CHANGED to public (this is so the antigrav can interact with it)
    {
        _powerJumpTimer = 0f;
    }

    private void AirJump()
    {
        if (_releaseJump)
        {
            _releaseJump = false;

            if (_moveDirection.y > 0 && isSliding == false)
            {
                _moveDirection.y *= 0.5f;
            }

        }

        //pressed jump button in air
        if (_startJump)
        {
            #region Water Jump
            if (_characterController.inWater)
            {
                isDoubleJumping = false;
                isTripleJumping = false;
                _moveDirection.y = jumpSpeed; // lets us do a regular jump

                _startJump = false;
            }
            #endregion

            #region TripleJump
            if (canTripleJump && (!_characterController.left && !_characterController.right))
            {
                if (isDoubleJumping && !isTripleJumping)
                {
                    _moveDirection.y = doubleJumpSpeed;
                    isTripleJumping = true;

                    _startJump = false;
                }
            }
            #endregion

            #region Double Jump
            if (canDoubleJump && (!_characterController.left && !_characterController.right))
            {
                if (!isDoubleJumping)
                {
                    _moveDirection.y = doubleJumpSpeed;
                    isDoubleJumping = true;
                    _startJump = false;
                }
            }
            #endregion

            #region Wall Jump
            if (canWallJump && (_characterController.left || _characterController.right))
            {
                coyoteTimer = 0;
                jumpBufferTimer = 0;
                completeAirControlTimer = 0;

                if (_moveDirection.x <= 0 && _characterController.left)
                {
                    _moveDirection.x = xWallJumpSpeed;
                    _moveDirection.y = yWallJumpSpeed;
                    //transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                    spriteRenderer.flipX = false;
                }
                else if (_moveDirection.x >= 0 && _characterController.right)
                {
                    _moveDirection.x = -xWallJumpSpeed;
                    _moveDirection.y = yWallJumpSpeed;
                    // transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    spriteRenderer.flipX = true;
                }

                _startJump = false;

                //isWallJumping = true;

                StartCoroutine("WallJumpWaiter");

                if (canJumpAfterWallJump)
                {
                    isDoubleJumping = false;
                    isTripleJumping = false;
                }
            }
            #endregion
        }
    }

    private void WallRunning()
    {
        //wall running
        if (canWallRun && (_characterController.left || _characterController.right))
        {
            if (_input.y > 0 && _ableToWallRun)
            {
                _moveDirection.y = wallRunAmount * Time.deltaTime;

                if (_characterController.left)
                {
                    transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                }
                else if (_characterController.right)
                {
                    transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                }

                StartCoroutine("WallRunWaiter");
            }
        }
        else
        {
            if (canMultipleWallRun)
            {
                StopCoroutine("WallRunWaiter");
                _ableToWallRun = true;
                isWallRunning = false;
            }
        }

        //canGlideAfterWallContact
        if ((_characterController.left || _characterController.right) && canWallRun)
        {
            if (canGlideAfterWallContact)
            {
                _currentGlideTime = glideTime;
            }
            else
            {
                _currentGlideTime = 0;
            }
        }
    }

    void GravityCalculations()
    {
        //detects if something above player
        if (_moveDirection.y > 0f && _characterController.above)
        {
            if (_characterController.ceilingType == GroundType.OneWayPlatform)
            {
                StartCoroutine(DisableOneWayPlatform(false));
            }
            else
            {
                _moveDirection.y = 0f;
            }

        }

        //apply wall slide adjustment
        if (canWallSlide && isWallJumping == false && ((_characterController.left && _input.x < 0) || (_characterController.right && _input.x > 0))) // NEW (is wall jumping clause added)
        {
            if (_characterController.hitWallThisFrame)
            {
                _moveDirection.y = 0;
            }


            if (_moveDirection.y <= 0)
            {
                _moveDirection.y -= (gravity * wallSlideAmount) * Time.deltaTime;
            }
            else
            {
                _moveDirection.y -= gravity * Time.deltaTime;
            }

        }
        else if (canGlide && _input.y > 0f && _moveDirection.y < 0.2f) // glide adjustment
        {
            if (_currentGlideTime > 0f)
            {
                isGliding = true;

                if (_startGlide)
                {
                    _moveDirection.y = 0;
                    _startGlide = false;
                }

                _moveDirection.y -= glideDescentAmount * Time.deltaTime;
                _currentGlideTime -= Time.deltaTime;
            }
            else
            {
                isGliding = false;
                _moveDirection.y -= gravity * Time.deltaTime;
            }

        }
        //else if (canGroundSlam  && !isPowerJumping && _input.y < 0f && _moveDirection.y < 0f) // ground slam
        else if (isGroundSlamming && !isPowerJumping && _moveDirection.y < 0f)
        {
            _moveDirection.y = -groundSlamSpeed * Time.deltaTime;
        }
        else if (!isDashing) //regular gravity
        {
            _moveDirection.y -= gravity * Time.deltaTime;

            if (_characterController.actualVeclocity.y < 0 && coyoteTimer < 0)
            {
                _moveDirection.y -= peakGravity * Time.deltaTime;
            }
        }
    }
    #endregion
    #endregion

    #region Input
    private void ApplyDeadzones()
    {
        if (_input.x > -deadzoneValue && _input.x < deadzoneValue)
            _input.x = 0f;

        if (_input.y > -deadzoneValue && _input.y < deadzoneValue)
            _input.y = 0f;
    }

    void InputBufferingAndCoyote()
    {
        jumpBufferTimer = jumpBufferTimer - Time.deltaTime;

        if (_startJump == true)
        {
            jumpBufferTimer = jumpBuffer;
        }

        if (_characterController.below == true)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer = coyoteTimer - Time.deltaTime;
        }
    }

    void SlideInputAndState()
    {
        RaycastHit2D headSpaceChecker;
        float capsuleColliderCircleRadius = _capsuleCollider.size.x / 2;
        Vector2 ccTopCirclePoint = new Vector2(transform.position.x, transform.position.y) + _capsuleCollider.offset + new Vector2(0, capsuleColliderCircleRadius);

        headSpaceChecker = Physics2D.CircleCast(ccTopCirclePoint, capsuleColliderCircleRadius, Vector2.up, 0, _characterController.layerMask);

        if (Keyboard.current.sKey.isPressed == true || (headSpaceChecker.collider != null && (isSliding == true || isCreeping == true)))
        {
            if ((_characterController.below == true || isSliding == true) && Mathf.Abs(_characterController.actualVeclocity.x) > requiredMinimumVelocity && isCreeping == false)
            {
                isSliding = true;
                isCreeping = false;
            }
            else if (_characterController.below == true && _characterController._slopeAngle != 0)
            {
                isSliding = true;
                isCreeping = false;
            }
            else if (_characterController.below == true)
            {
                isSliding = false;
                isCreeping = true;
            }
        }
        else
        {
            isSliding = false;
            isCreeping = false;

            slideFrameOne = true;
        }

        if (_startJump == true && isCreeping == true)
        {
            isCreeping = false;
            isSliding = false;
        }

        // transfer state to character controller 
        if (isSliding == true) _characterController.isSliding = true;
        else _characterController.isSliding = false;

        // if ceiling hit then jumping no
        // this is relevent in spaces where the ceiling is so low that the grounding raycast will still detect ground even if the player jumps
        if (_characterController.slidingColAbove == true) { isSlideJumping = false; }
    }

    void RunInput()
    {
        if (Keyboard.current.leftShiftKey.isPressed == true && isSliding == false && isCreeping == false)
        {
            isRunning = true;
        }
        else
        {
            isRunning = false;
        }
    }

    #region Methods
    public void OnMovement(InputAction.CallbackContext context)
    {
        _input = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            _startJump = true;
            _releaseJump = false;
            _holdJump = true;
        }
        else if (context.canceled)
        {
            _releaseJump = true;
            _startJump = false;
            _holdJump = false;
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (context.started && _dashTimer <= 0)
        {
            if ((canAirDash && !_characterController.below)
                || (canGroundDash && _characterController.below))
            {
                StartCoroutine("Dash");
            }
        }
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.performed && _input.y < 0f)
        {
            if (canGroundSlam)
            {
                isGroundSlamming = true;
            }
        }
    }
    #endregion
    #endregion

    #region Coroutines
    IEnumerator WallJumpWaiter()
    {
        isWallJumping = true;
        yield return new WaitForSeconds(wallJumpedNullTime);
        isWallJumping = false;
    }

    IEnumerator WallRunWaiter()
    {
        isWallRunning = true;
        yield return new WaitForSeconds(0.5f);
        isWallRunning = false;
        if (!isWallJumping)
        {
            _ableToWallRun = false;
        }

    }

    IEnumerator ClearDuckingState()
    {
        yield return new WaitForSeconds(0.05f);

        /*
        RaycastHit2D hitCeiling = Physics2D.CapsuleCast(_capsuleCollider.bounds.center, transform.localScale,
            CapsuleDirection2D.Vertical, 0f, Vector2.up, _originalColliderSize.y / 2, _characterController.layerMask);
        
        if (!hitCeiling.collider) { 
            _capsuleCollider.size = _originalColliderSize;
            //transform.position = new Vector2(transform.position.x, transform.position.y + (_originalColliderSize.y / 4));
            _spriteRenderer.sprite = Resources.Load<Sprite>("Player_normal");
            isDucking = false;
            isCreeping = false;
        }
        */
    }

    IEnumerator PowerJumpWaiter()
    {
        isPowerJumping = true;
        yield return new WaitForSeconds(0.8f);
        isPowerJumping = false;
    }

    IEnumerator Dash()
    {
        isDashing = true;
        yield return new WaitForSeconds(dashTime);
        isDashing = false;
        _dashTimer = dashCooldownTime;
    }

    IEnumerator DisableOneWayPlatform(bool checkBelow)
    {
        bool originalCanGroundSlam = canGroundSlam;
        GameObject tempOneWayPlatform = null;

        if (checkBelow)
        {
            Vector2 raycastBelow = transform.position - new Vector3(0, _capsuleCollider.size.y * 0.5f, 0);
            RaycastHit2D hit = Physics2D.Raycast(raycastBelow, Vector2.down,
                _characterController.raycastDistance, _characterController.layerMask);
            if (hit.collider)
            {
                tempOneWayPlatform = hit.collider.gameObject;
            }

        }
        else
        {
            Vector2 raycastAbove = transform.position + new Vector3(0, _capsuleCollider.size.y * 0.5f, 0);
            RaycastHit2D hit = Physics2D.Raycast(raycastAbove, Vector2.up,
                _characterController.raycastDistance, _characterController.layerMask);
            if (hit.collider)
            {
                tempOneWayPlatform = hit.collider.gameObject;
            }
        }

        if (tempOneWayPlatform)
        {
            tempOneWayPlatform.GetComponent<EdgeCollider2D>().enabled = false;
            canGroundSlam = false;
        }

        yield return new WaitForSeconds(0.25f);

        if (tempOneWayPlatform)
        {
            tempOneWayPlatform.GetComponent<EdgeCollider2D>().enabled = true;
            canGroundSlam = originalCanGroundSlam;
        }

    }

    #endregion
}