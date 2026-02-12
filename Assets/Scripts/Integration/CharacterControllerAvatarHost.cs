using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class CharacterControllerAvatarHost : CharacterAvatarHostAdapter
{
    [Header("Avatar")]
    [SerializeField] private Transform avatarAnchor;
    [SerializeField] private bool autoCreateAnchor = true;
    [SerializeField] private string avatarAnchorName = "AvatarAnchor";
    [SerializeField] private bool snapAvatarLocalPose = true;
    [SerializeField] private Vector3 avatarLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 avatarLocalEuler = Vector3.zero;

    [Header("Sample Movement")]
    [SerializeField] private bool enableSampleMovement = true;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float turnSpeed = 540f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundStickVelocity = -2f;
    [SerializeField] private string horizontalAxis = "Horizontal";
    [SerializeField] private string verticalAxis = "Vertical";

    private CharacterController controller;
    private float verticalVelocity;

    public override Transform AvatarAnchor => EnsureAvatarAnchor();

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        EnsureAvatarAnchor();
    }

    private void Update()
    {
        if (!enableSampleMovement || controller == null)
            return;

        float x = Input.GetAxisRaw(horizontalAxis);
        float z = Input.GetAxisRaw(verticalAxis);
        var planarInput = new Vector3(x, 0f, z);
        planarInput = Vector3.ClampMagnitude(planarInput, 1f);

        if (planarInput.sqrMagnitude > 0.0001f)
        {
            var targetRotation = Quaternion.LookRotation(planarInput, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = groundStickVelocity;

        verticalVelocity += gravity * Time.deltaTime;
        var move = transform.forward * (planarInput.magnitude * moveSpeed);
        move.y = verticalVelocity;
        controller.Move(move * Time.deltaTime);
    }

    public override void AttachAvatar(Transform avatarRoot)
    {
        if (avatarRoot == null)
            return;

        var anchor = EnsureAvatarAnchor();
        if (anchor == null)
            return;

        avatarRoot.SetParent(anchor, worldPositionStays: false);
        if (!snapAvatarLocalPose)
            return;

        avatarRoot.localPosition = avatarLocalPosition;
        avatarRoot.localRotation = Quaternion.Euler(avatarLocalEuler);
    }

    private Transform EnsureAvatarAnchor()
    {
        if (avatarAnchor != null)
            return avatarAnchor;

        if (!autoCreateAnchor)
            return null;

        var existing = transform.Find(avatarAnchorName);
        if (existing != null)
        {
            avatarAnchor = existing;
            return avatarAnchor;
        }

        var anchorGo = new GameObject(avatarAnchorName);
        avatarAnchor = anchorGo.transform;
        avatarAnchor.SetParent(transform, worldPositionStays: false);
        avatarAnchor.localPosition = Vector3.zero;
        avatarAnchor.localRotation = Quaternion.identity;
        return avatarAnchor;
    }
}
