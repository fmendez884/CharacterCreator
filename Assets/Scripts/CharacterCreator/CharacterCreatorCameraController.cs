using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class CharacterCreatorCameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private string fallbackTargetName = "CharacterRoot";
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.4f, 0f);

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 350f;
    [SerializeField] private bool invertY;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Pan")]
    [SerializeField] private float panSpeed = 2.5f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = .25f;
    [SerializeField] private float minDistance = .5f;
    [SerializeField] private float maxDistance = 6f;

    [Header("Input")]
    [SerializeField] private int rotateMouseButton = 0;
    [SerializeField] private int panMouseButton = 1;
    [SerializeField] private bool ignoreWhenPointerOverUI = true;

    private float yaw;
    private float pitch;
    private float distance = 3f;
    private Vector3 panOffset;
    private bool initialized;

    private void OnEnable()
    {
        TryResolveTarget();
        EnsureInitialized();
    }

    private void LateUpdate()
    {
        if (!TryResolveTarget())
            return;

        if (ignoreWhenPointerOverUI && IsPointerOverUI())
            return;

        EnsureInitialized();
        HandleInput();
        ApplyCameraTransform();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        initialized = false;
    }

    private void HandleInput()
    {
        if (Input.GetMouseButton(rotateMouseButton))
        {
            float mouseX = Input.GetAxisRaw("Mouse X");
            float mouseY = Input.GetAxisRaw("Mouse Y");
            yaw += mouseX * rotationSpeed * Time.deltaTime;
            float pitchDelta = mouseY * rotationSpeed * Time.deltaTime;
            pitch += invertY ? pitchDelta : -pitchDelta;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        if (Input.GetMouseButton(panMouseButton))
        {
            float mouseX = Input.GetAxisRaw("Mouse X");
            float mouseY = Input.GetAxisRaw("Mouse Y");
            float scale = panSpeed * Mathf.Max(distance, 0.01f) * Time.deltaTime;
            panOffset += (-transform.right * mouseX + -transform.up * mouseY) * scale;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
            distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
    }

    private void ApplyCameraTransform()
    {
        Vector3 pivot = GetPivot();
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 position = pivot - rotation * Vector3.forward * distance;
        transform.SetPositionAndRotation(position, rotation);
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        Vector3 pivot = GetPivot();
        Vector3 toCamera = transform.position - pivot;
        float startDistance = toCamera.magnitude;
        if (startDistance > 0.001f)
            distance = Mathf.Clamp(startDistance, minDistance, maxDistance);

        Quaternion lookRotation = toCamera.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(-toCamera.normalized, Vector3.up)
            : transform.rotation;

        Vector3 angles = lookRotation.eulerAngles;
        pitch = NormalizeAngle(angles.x);
        yaw = NormalizeAngle(angles.y);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        initialized = true;
    }

    private bool TryResolveTarget()
    {
        if (target != null)
            return true;

        if (!string.IsNullOrWhiteSpace(fallbackTargetName))
        {
            var named = GameObject.Find(fallbackTargetName);
            if (named != null)
                target = named.transform;
        }

        if (target == null)
        {
            var creator = FindObjectOfType<CharacterCreator>();
            if (creator != null)
                target = creator.transform;
        }

        return target != null;
    }

    private Vector3 GetPivot()
    {
        if (target == null)
            return transform.position + panOffset;

        return target.TransformPoint(targetOffset) + panOffset;
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
            angle -= 360f;
        return angle;
    }
}
