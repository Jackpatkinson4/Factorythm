using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls all the player's logic.
/// </summary>
public class PlayerController : MonoBehaviour {
    private Rigidbody2D _myRb;
    private BoxCollider2D _myCollider;
    private SmoothSprite _mySS;
    private SmoothRotate _mySRot;

    private Room _curRoom;

    private bool _isHoldingZ;
    private bool _isHoldingX;
    private InteractableStateMachine _ism;
    private PlayerInputActions _pia;

    public bool RhythmLocked = true;

    public bool CanPlaceHeadMine;
    public bool CanPlaceStemMine;


    void Start() {
        _myRb = GetComponent<Rigidbody2D>();
        _myCollider = GetComponent<BoxCollider2D>();
        _mySS = GetComponentInChildren<SmoothSprite>();
        _mySRot = GetComponentInChildren<SmoothRotate>();
        _ism = GetComponent<InteractableStateMachine>();
        _pia = new PlayerInputActions();

        _pia.Player.Enable();
        _pia.Player.Interact.performed += Interact;
        _pia.Player.Interact.canceled += Interact;
        _pia.Player.Delete.performed += Delete;
        _pia.Player.Delete.canceled += Delete;
        _pia.Player.Movement.started += Movement;
    }

    void Update() {
        _ism.SetZPressed(_isHoldingZ);
        _ism.SetXPressed(_isHoldingX);

        this.CheckTileOn();

        if (Input.GetKeyDown(KeyCode.Escape)) {
            RestartGame();
        }
    }

    public void RestartGame() {
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
        StopCoroutine("SmoothSprite._moveCoroutine");
    }

    private void FixedUpdate() {
        Collider2D roomCollider = CheckRoomOverlap();
        if (roomCollider) {
            if (!_curRoom) {
                _curRoom = roomCollider.GetComponent<Room>();
                _curRoom.OnPlayerEnter(this);
            }
        }
        else {
            if (_curRoom) {
                _curRoom.OnPlayerExit(this);
            }

            _curRoom = null;
        }
    }

    /// <summary>
    /// Checks whether there is a room at the position defined by [position]+[<paramref name="offset"/>].
    /// Used to check whether there's an un-enterable room.
    /// </summary>
    /// <returns>True if can move there, false if not</returns>
    private bool CanMoveTo(Vector2 offset) {
        Collider2D roomCollider = CheckRoomOverlap(offset);
        if (roomCollider) {
            Room room = roomCollider.GetComponent<Room>();
            return room.CanPlayerEnter(this);
        }

        return true;
    }

    /// <summary>
    /// Checks what rooms the player is in. Used for calling <see cref="Room.OnPlayerEnter(PlayerController)"/> and
    /// for seeing whether the player can enter a room.
    /// </summary>
    /// <param name="offset">Offset from player's position.</param>
    /// <returns>The collider of the room the player is in</returns>
    private Collider2D CheckRoomOverlap(Vector3 offset) {
        Vector3
            alpha = new Vector3(0.05f,
                0.05f); //So the player touching the edge of the collider isn't counted as an overlap
        Vector2 topLeftCorner = _myCollider.bounds.min + offset + alpha;
        Vector2 topRightCorner = _myCollider.bounds.max + offset - alpha;
        Collider2D overlapCollider = Physics2D.OverlapArea(
            topLeftCorner,
            topRightCorner,
            LayerMask.GetMask("Room")
        );
        return overlapCollider;
    }

    /// <summary>
    /// Overload of <see cref="CheckRoomOverlap(UnityEngine.Vector3)"/>
    /// </summary>
    private Collider2D CheckRoomOverlap() {
        return CheckRoomOverlap(new Vector3(0, 0, 0));
    }
  
    public void Tick() {
        // _mySRot.Alternate();
    }

    public Interactable OnInteractable() {
        return Helper.OnComponent<Interactable>(transform.position);
    }

    public Destructable OnDestructable()
    {
        return Helper.OnComponent<Destructable>(transform.position);
    }

    #region Actions

    private void Movement(InputAction.CallbackContext context) {
        Vector2 inputVector = _pia.Player.Movement.ReadValue<Vector2>();
        Vector3 newPos = Vector3.zero;
        // bool moved = false;
        // bool attemptMove = inputVector != Vector2.zero;

        bool onBeat;

        if (!Conductor.Instance.RhythmLock) {
            onBeat = true;
            // Conductor.Instance.MachineTick();
        }
        else {
            if (this.RhythmLocked) {
                onBeat = Conductor.Instance.AttemptMove();
            }
            else {
                onBeat = true;
            }
        }

        if (onBeat) {
            if (this != null) {
                int offsetX = (inputVector.x > 0 ? 1 : (inputVector.x < 0 ? -1 : 0));
                int offsetY = (inputVector.y > 0 ? 1 : (inputVector.y < 0 ? -1 : 0));
                Vector2 offset = new Vector2(offsetX, offsetY);
                newPos = _myRb.position + offset;

                if (CanMoveTo(offset)) {
                    _mySS.Move(newPos);
                    _myRb.MovePosition(newPos);
                    _ism.Move(newPos);
                }
                else {
                    Vector2 delta = newPos - transform.position;
                    newPos = transform.position + (Vector3) delta * 0.2f;
                    _mySS.BounceAnimate(transform.position, newPos);
                }
            }
        }
    }

    private void Interact(InputAction.CallbackContext context) {
        _isHoldingZ = context.performed;
    }

    private void Delete(InputAction.CallbackContext context) {
        _isHoldingX = context.performed;
    }

    #endregion
    
    /// <summary>
    /// [March 12th, 2022] Update: Added a return value for this method if extension is needed for more than just
    /// head and stem tile tags.
    /// </summary>
    /// <returns> the Raycast's hit ray</returns>
    public RaycastHit2D CheckTileOn()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            new Vector3(0, 0, 1),
            10.0f,
            LayerMask.GetMask("Default"));
        if (hit.transform != null) {
            if (hit.transform.gameObject.CompareTag("StemTiles")) {
                CanPlaceStemMine = true;
                CanPlaceHeadMine = false;
            }
            else if (hit.transform.gameObject.CompareTag("HeadTiles")) {
                CanPlaceStemMine = false;
                CanPlaceHeadMine = true;
            }
            else {
                CanPlaceStemMine = false;
                CanPlaceHeadMine = false;
            }
        }
        else {
            CanPlaceStemMine = false;
            CanPlaceHeadMine = false;
        }

        return hit;
    }
}