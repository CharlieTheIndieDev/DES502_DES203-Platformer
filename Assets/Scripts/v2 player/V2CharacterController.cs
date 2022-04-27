using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GlobalTypes;

public class V2CharacterController : MonoBehaviour
{
    public float raycastDistance = 0.2f;
    public LayerMask layerMask;

    public float slopeAngleLimit = 45f;
    public float downForceAdjustment = 1.2f;

    //flags
    public bool below;
    public bool left;
    public bool right;
    public bool above;

    public bool slidingColBelow;
    public bool slidingColAbove;
    public bool slidingColLeft;
    public bool slidingColRight;
    public Vector2 slidingBelowNormal;
    public float slidingBelowAngle;
    public Vector2 slidingLeftNormal;
    public Vector2 slidingRightNormal;

    public GroundType groundType;
    public WallType leftWallType;
    public WallType rightWallType;
    public GroundType ceilingType;

    public bool hitGroundThisFrame;
    public bool hitWallThisFrame;

    private Vector2 _moveAmount;
    private Vector2 _currentPostion;
    private Vector2 _lastPosition;
    [HideInInspector] public Vector2 _moveVelocity; //NEW

    private Rigidbody2D _rigidbody;
    private CapsuleCollider2D _capsuleCollider;
    CapsuleCollider2D _slidingCollider;

    private Vector2[] _raycastPosition = new Vector2[3];
    private RaycastHit2D[] _raycastHits = new RaycastHit2D[3];

    private bool _disableGroundCheck;

    public bool inWater;
    public bool isSubmerged;

    public bool antiGravActive = false; // NEW
    public bool isSliding = false;

    public float _slopeAngle; // CHANGED to public
    public Vector2 _slopeNormal; // CHANGED to public

    public List<Vector2> actualPositions = new List<Vector2>();
    public Vector2 actualVeclocity;

    //TODO: Change to private

    private bool _inAirLastFrame;
    private bool _noSideCollisionLastFrame;

    private Transform _tempMovingPlatform;
    private Vector2 _movingPlatformVelocity;

    // Start is called before the first frame update
    void Start()
    {
        _rigidbody = gameObject.GetComponent<Rigidbody2D>();
        _capsuleCollider = gameObject.GetComponent<CapsuleCollider2D>();
        _slidingCollider = transform.Find("SlidingCollider").gameObject.GetComponent<CapsuleCollider2D>();

        ActualMovementStoring(); //NEW
        ActualMovementStoring(); //NEW
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        _inAirLastFrame = !below;

        _noSideCollisionLastFrame = (!right && !left);

        _lastPosition = _rigidbody.position;

        if (antiGravActive == false && isSliding == false) //NEW
        {
            //slope adjustments
            if (_slopeAngle != 0 && below == true)
            {
                if ((_moveAmount.x > 0f && _slopeAngle > 0f) || (_moveAmount.x < 0f && _slopeAngle < 0f))
                {
                    _moveAmount.y = -Mathf.Abs(Mathf.Tan(_slopeAngle * Mathf.Deg2Rad) * _moveAmount.x);
                    _moveAmount.y *= downForceAdjustment;
                }
            }
        }

        //moving platform adjustment
        if (groundType == GroundType.MovingPlatform)
        {
            //offset the player's movement on the X with the moving platform velocity
            _moveAmount.x += MovingPlatformAdjust().x;

            //if platform is moving down
            if (MovingPlatformAdjust().y < 0f)
            {
                //offset the player's movement on the Y
                _moveAmount.y += MovingPlatformAdjust().y;
                _moveAmount.y *= downForceAdjustment;
            }


        }
        //y dir stuff
        if (groundType == GroundType.CollapsablePlatform)
        {
            if (MovingPlatformAdjust().y < 0f) // are we moving away from player
            {
                _moveAmount.y += MovingPlatformAdjust().y;
                _moveAmount.y *= downForceAdjustment * 4;//adjusts any gap when moving down
            }
        }

        // handling water stuff
        if (!inWater)
        {
            _currentPostion = _lastPosition + _moveAmount;
            _rigidbody.MovePosition(_currentPostion);
        }
        else
        {
            if (_rigidbody.velocity.magnitude < 10f)
            {
                _rigidbody.AddForce(_moveAmount * 300f);
            }

        }

        _moveAmount = Vector2.zero;

        if (!_disableGroundCheck)
        {
            CheckGrounded();
        }

        CheckOtherCollisions();

        if (below && _inAirLastFrame)
        {
            hitGroundThisFrame = true;
        }
        else
        {
            hitGroundThisFrame = false;
        }

        if ((right || left) && _noSideCollisionLastFrame)
        {
            hitWallThisFrame = true;
        }
        else
        {
            hitWallThisFrame = false;
        }

        _moveVelocity = _currentPostion - _lastPosition; //NEW
        ActualMovementStoring(); //NEW
    }

    void ActualMovementStoring()
    {
        actualPositions.Add(_rigidbody.position);

        if (actualPositions.Count > 2)
        {
            actualPositions.RemoveAt(0);

            actualVeclocity = actualPositions[1] - actualPositions[0];
        }
    }

    public void Move(Vector2 movement)
    {
        _moveAmount += movement;
    }

    private void CheckGrounded()
    {
        RaycastHit2D hit = Physics2D.CapsuleCast(_capsuleCollider.bounds.center, _capsuleCollider.size, CapsuleDirection2D.Vertical, 0f, Vector2.down, raycastDistance, layerMask);
        if (hit.collider)
        {
            groundType = DetermineGroundType(hit.collider);

            _slopeNormal = hit.normal;
            _slopeAngle = Vector2.SignedAngle(_slopeNormal, Vector2.up);

            if (_slopeAngle > slopeAngleLimit || _slopeAngle < -slopeAngleLimit)
            {
                below = false;
            }
            else
            {
                below = true;
            }
        }
        else
        {
            groundType = GroundType.None;
            below = false;
            if (_tempMovingPlatform)
            {
                _tempMovingPlatform = null;
            }
        }


        // this (and the other sliding casts) have to exist because the player is shorter when sliding
        // what this means is if there is a ceiling above them then the default ground cast will catch on that ceiling because it is being casted from the player's would be center if they were standing up
        // but they're not so that cast happens above the player while sliding, being able to get caught on low ceilings
        // you could fix this by having the casts not come out from the center of the collider, and/or have them adjust to the sliding collider based on state, or have the same collider used but resized when sliding, or just have the raycasts be configured manually via inspector
        // but it isn't important, although messy this fix is good enough

        // this is the below check for the sliding collider
        RaycastHit2D slideHit = Physics2D.CapsuleCast(_slidingCollider.bounds.center, _slidingCollider.size, CapsuleDirection2D.Vertical, 0f, Vector2.down, raycastDistance, layerMask);
        if (slideHit.collider != null)
        {
            slidingColBelow = true;

            slidingBelowNormal = slideHit.normal;
            slidingBelowAngle = Vector2.SignedAngle(slidingBelowNormal, Vector2.up);
        }
        else
        {
            slidingColBelow = false;

            slidingBelowNormal = Vector2.zero;
            slidingBelowAngle = 0;
        }
    }

    private void CheckOtherCollisions()
    {
        //check left
        RaycastHit2D leftHit = Physics2D.BoxCast(_capsuleCollider.bounds.center, _capsuleCollider.size * 0.75f, 0f, Vector2.left,
            raycastDistance * 2, layerMask);

        if (leftHit.collider)
        {
            leftWallType = DetermineWallType(leftHit.collider);
            left = true;
        }
        else
        {
            leftWallType = WallType.None;
            left = false;
        }


        //check right
        RaycastHit2D rightHit = Physics2D.BoxCast(_capsuleCollider.bounds.center, _capsuleCollider.size * 0.75f, 0f, Vector2.right,
            raycastDistance * 2, layerMask);

        if (rightHit.collider)
        {
            rightWallType = DetermineWallType(rightHit.collider);
            right = true;
        }
        else
        {
            rightWallType = WallType.None;
            right = false;
        }

        //check above
        RaycastHit2D aboveHit = Physics2D.CapsuleCast(_capsuleCollider.bounds.center, _capsuleCollider.size, CapsuleDirection2D.Vertical,
           0f, Vector2.up, raycastDistance, layerMask);

        if (aboveHit.collider)
        {
            ceilingType = DetermineGroundType(aboveHit.collider);
            above = true;
        }
        else
        {
            ceilingType = GroundType.None;
            above = false;
        }

        #region sliding collider hits
        // check above
        RaycastHit2D aboveSlideHit = Physics2D.CapsuleCast(_slidingCollider.bounds.center, _slidingCollider.size, CapsuleDirection2D.Vertical, 0f, Vector2.up, raycastDistance, layerMask);
        if (aboveSlideHit.collider)
        {
            ceilingType = DetermineGroundType(aboveSlideHit.collider);
            slidingColAbove = true;
        }
        else
        {
            slidingColAbove = false;
        }

        // the reason the normals of the walls is needed is because of a bug
        // if you hit a wall while sliding you don't lose speed
        // you will stop but if the wall moves out the way then you'll launch off at the speed you previously had
        // the normals are used to stop the player's speed (while sliding) if they hit a vertical wall

        //check left
        RaycastHit2D leftSlideHit = Physics2D.BoxCast(_slidingCollider.bounds.center, _slidingCollider.size * 0.75f, 0f, Vector2.left, raycastDistance * 2, layerMask);
        if (leftSlideHit.collider)
        {
            slidingColLeft = true;
            slidingLeftNormal = leftSlideHit.normal;
        }
        else
        {
            slidingColLeft = true;
            slidingLeftNormal = Vector2.zero;
        }

        //check right
        RaycastHit2D rightSlidetHit = Physics2D.BoxCast(_slidingCollider.bounds.center, _slidingCollider.size * 0.75f, 0f, Vector2.right, raycastDistance * 2, layerMask);
        if (rightSlidetHit.collider)
        {
            slidingColRight = true;
            slidingRightNormal = rightSlidetHit.normal;
        }
        else
        {
            slidingColRight = false;
            slidingRightNormal = Vector2.zero;
        }
        #endregion
    }

    /*
    private void CheckGrounded()
    {
        Vector2 raycastOrigin = _rigidbody.position - new Vector2(0, _capsuleCollider.size.y * 0.5f);

        _raycastPosition[0] = raycastOrigin + (Vector2.left * _capsuleCollider.size.x * 0.25f + Vector2.up * 0.1f);
        _raycastPosition[1] = raycastOrigin;
        _raycastPosition[2] = raycastOrigin + (Vector2.right * _capsuleCollider.size.x * 0.25f + Vector2.up * 0.1f);

        DrawDebugRays(Vector2.down, Color.green);

        int numberOfGroundHits = 0;

        for (int i = 0; i < _raycastPosition.Length; i++)
        {
            RaycastHit2D hit = Physics2D.Raycast(_raycastPosition[i], Vector2.down, raycastDistance, layerMask);

            if (hit.collider)
            {
                _raycastHits[i] = hit;
                numberOfGroundHits++;
            }
        }

        if (numberOfGroundHits > 0)
        {
            if (_raycastHits[1].collider)
            {
                groundType = DetermineGroundType(_raycastHits[1].collider);
                _slopeNormal = _raycastHits[1].normal;
                _slopeAngle = Vector2.SignedAngle(_slopeNormal, Vector2.up);
            }
            else
            {
                for (int i = 0; i  < _raycastHits.Length; i++)
                {
                    if (_raycastHits[i].collider)
                    {
                        groundType = DetermineGroundType(_raycastHits[i].collider);
                        _slopeNormal = _raycastHits[i].normal;
                        _slopeAngle = Vector2.SignedAngle(_slopeNormal, Vector2.up);
                    }
                }
            }

            if (_slopeAngle > slopeAngleLimit || _slopeAngle < -slopeAngleLimit)
            {
                below = false;
            }
            else
            {
                below = true;
            }

        }
        else
        {
            groundType = GroundType.None;
            below = false;
        }

        System.Array.Clear(_raycastHits, 0, _raycastHits.Length);

    }*/

    private void DrawDebugRays(Vector2 direction, Color color)
    {
        for (int i = 0; i < _raycastPosition.Length; i++)
        {
            Debug.DrawRay(_raycastPosition[i], direction * raycastDistance, color);
        }
    }

    public void DisableGroundCheck()
    {
        below = false;
        _disableGroundCheck = true;
        StartCoroutine("EnableGroundCheck");
    }

    IEnumerator EnableGroundCheck()
    {
        yield return new WaitForSeconds(0.1f);
        _disableGroundCheck = false;
    }

    private GroundType DetermineGroundType(Collider2D collider)
    {
        if (collider.GetComponent<GroundEffector>())
        {
            GroundEffector groundEffector = collider.GetComponent<GroundEffector>();
            if (groundType == GroundType.MovingPlatform || groundType == GroundType.CollapsablePlatform)
            {
                if (!_tempMovingPlatform)
                {
                    _tempMovingPlatform = collider.transform;

                    if (groundType == GroundType.CollapsablePlatform)
                    {
                        _tempMovingPlatform.GetComponent<CollapsablePlatform>().CollapsePlatform();
                    }
                }
            }

            return groundEffector.groundType;
        }
        else
        {
            if (_tempMovingPlatform)
            {
                _tempMovingPlatform = null;
            }

            return GroundType.LevelGeometry;
        }
    }

    private WallType DetermineWallType(Collider2D collider)
    {
        if (collider.GetComponent<WallEffector>())
        {
            WallEffector wallEffector = collider.GetComponent<WallEffector>();
            return wallEffector.wallType;
        }
        else
        {
            return WallType.Normal;
        }
    }

    private Vector2 MovingPlatformAdjust()
    {
        if (_tempMovingPlatform && groundType == GroundType.MovingPlatform)
        {
            _movingPlatformVelocity = _tempMovingPlatform.GetComponent<MovingPlatform>().difference;
            return _movingPlatformVelocity;
        }
        else if (_tempMovingPlatform && groundType == GroundType.CollapsablePlatform)
        {
            _movingPlatformVelocity = _tempMovingPlatform.GetComponent<CollapsablePlatform>().difference;
            return _movingPlatformVelocity;
        }
        else
        {
            return Vector2.zero;
        }
    }


    //make sure we don't gain extra height when falling from moving platform
    public void ClearMovingPlatform()
    {
        if (_tempMovingPlatform)
        {
            _tempMovingPlatform = null;
        }
    }


    // water physics
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.GetComponent<BuoyancyEffector2D>())
        {
            inWater = true;
        }
    }

    // checks if fully submerged or not Also only triggers if buoyancy effector exists
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.bounds.Contains(_capsuleCollider.bounds.min) &&
            collision.bounds.Contains(_capsuleCollider.bounds.max) &&
            collision.gameObject.GetComponent<BuoyancyEffector2D>())
        {
            isSubmerged = true;
        }
        else
        {
            isSubmerged = false;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.GetComponent<BuoyancyEffector2D>())
        {
            _rigidbody.velocity = Vector2.zero;
            inWater = false;
        }
    }
}
