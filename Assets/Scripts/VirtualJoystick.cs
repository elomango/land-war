using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("Settings")]
    public float deadZone = 20f; // 중심에서 이 거리 이하면 입력 무시

    private RectTransform rectTransform;
    private Vector2 inputDirection;
    private bool isPressed = false;

    // PlayerController가 이 값을 읽어갑니다
    public Vector2 Direction => inputDirection;
    public bool IsPressed => isPressed;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        UpdateDirection(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateDirection(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        inputDirection = Vector2.zero;
    }

    void UpdateDirection(PointerEventData eventData)
    {
        // 조이스틱 중심점 계산
        Vector2 center = rectTransform.position;

        // 터치 위치와 중심점의 차이
        Vector2 touchPosition = eventData.position;
        Vector2 offset = touchPosition - center;

        // 거리 체크
        if (offset.magnitude < deadZone)
        {
            inputDirection = Vector2.zero;
            return;
        }

        // 4방향 입력으로 변환
        inputDirection = GetDirection4Way(offset);
    }

    // 4방향 (상하좌우)
    Vector2 GetDirection4Way(Vector2 offset)
    {
        float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;

        // 각도를 0~360으로 정규화
        if (angle < 0) angle += 360f;

        // 4방향 판정
        if (angle >= 315f || angle < 45f)
            return Vector2.right;
        else if (angle >= 45f && angle < 135f)
            return Vector2.up;
        else if (angle >= 135f && angle < 225f)
            return Vector2.left;
        else
            return Vector2.down;
    }
}
