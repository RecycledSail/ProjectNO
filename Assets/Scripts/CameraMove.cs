using UnityEngine;

public class CameraMove : MonoBehaviour
{
    // 드래그 속도, 줌 속도, 최소/최대 줌 FOV
    public float dragSpeed = 500f;
    public float zoomSpeed = 100f;
    public float minZoom = 5f;
    public float maxZoom = 50f;

    // 카메라 이동 한계
    public Vector2 moveThreshold = new Vector2(100, 100); // X, Z axis limit

    // 이전 Update때의 마우스 위치 (현재 Update때의 위치와 비교해서 얼마나 움직이는지 사용)
    private Vector3 dragOrigin;

    // 초기 카메라의 위치 (moveThreshold와 같이 사용)
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

    /// <summary>
    /// 마우스 드래깅 핸들링 메서드
    /// </summary>
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

    /// <summary>
    /// 카메라를 이동시키는 메서드
    /// moveThreshold까지만 움직일 수 있음 (맵 밖으로 너무 벗어나는 것 방지)
    /// </summary>
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

    /// <summary>
    /// FOV를 변경하여 줌인/줌아웃을 하는 메서드
    /// 최소 minZoom, 최대 maxZoom FOV까지만 줌인/줌아웃 가능
    /// </summary>
    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        Camera.main.fieldOfView -= scroll * zoomSpeed;
        Camera.main.fieldOfView = Mathf.Clamp(Camera.main.fieldOfView, minZoom, maxZoom);
    }
}
