using UnityEngine;

public class CameraMove : MonoBehaviour
{
    public float dragSpeed = 500f;
    public float zoomSpeed = 100f;
    public float minZoom = 5f;
    public float maxZoom = 50f;

    public Vector2 moveThreshold = new Vector2(100, 100); // X, Z axis limit

    private Vector3 dragOrigin;
    private Vector3 origin;

    private void Start()
    {
        origin = transform.position;
    }

    void Update()
    {
        HandleMouseDrag();
        HandleZoom();
    }

    void HandleMouseDrag()
    {
        if (Input.GetMouseButtonDown(2)) // Middle Mouse Button Press
        {
            dragOrigin = Input.mousePosition;
        }

        if (Input.GetMouseButton(2)) // Middle Mouse Button Hold
        {
            MoveAround();
        }
    }

    void MoveAround()
    {
        Vector3 difference = Camera.main.ScreenToViewportPoint(Input.mousePosition - dragOrigin);
        Vector3 move = new Vector3(-difference.x * dragSpeed * Camera.main.fieldOfView * (1.6f / 1.0f) * 1.6f, 0, -difference.y * dragSpeed * Camera.main.fieldOfView);

        transform.Translate(move, Space.World);

        // Clamp position within moveThreshold
        Vector3 clampedPosition = transform.position;
        clampedPosition.x = Mathf.Clamp(clampedPosition.x, origin.x - moveThreshold.x, origin.x + moveThreshold.x);
        clampedPosition.z = Mathf.Clamp(clampedPosition.z, origin.z - moveThreshold.y * 0.8f, origin.z + moveThreshold.y * 1.2f);
        transform.position = clampedPosition;

        dragOrigin = Input.mousePosition;
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        Camera.main.fieldOfView -= scroll * zoomSpeed;
        Camera.main.fieldOfView = Mathf.Clamp(Camera.main.fieldOfView, minZoom, maxZoom);
    }
}
