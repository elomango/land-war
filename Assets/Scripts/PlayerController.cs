using UnityEngine;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    public enum PlayerState { OnSafeZone, Capturing, Returning }

    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Animation Settings")]
    public float pulseSpeed = 1f; // 1초에 1번
    public float minScale = 0.8f;
    public float maxScale = 1.2f;

    [Header("References")]
    public VirtualJoystick joystick;
    public SafeZoneManager safeZone;

    [Header("Play Area Bounds")]
    public float playAreaWidth = 10f;
    public float playAreaHeight = 14f;

    private PlayerState currentState = PlayerState.OnSafeZone;
    private float halfWidth;
    private float halfHeight;
    private Vector3 originalScale;
    private bool isMoving = false;

    // 영역 점령 관련
    private Vector3 captureStartPosition;
    private List<Vector3> capturePath = new List<Vector3>();
    private LineRenderer pathLine;
    private bool spacePressed = false;
    private bool hasLeftSafeZone = false; // 안전지대를 벗어났는지 체크

    void Start()
    {
        halfWidth = playAreaWidth / 2f;   // 5
        halfHeight = playAreaHeight / 2f; // 7
        originalScale = transform.localScale;

        // LineRenderer 설정
        pathLine = gameObject.AddComponent<LineRenderer>();
        pathLine.startWidth = 0.05f;
        pathLine.endWidth = 0.05f;
        pathLine.material = new Material(Shader.Find("Sprites/Default"));
        pathLine.startColor = Color.yellow;
        pathLine.endColor = Color.yellow;
        pathLine.positionCount = 0;
    }

    void Update()
    {
        // 키보드 스페이스바 입력 처리
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnSpacePressed();
        }
        if (Input.GetKeyUp(KeyCode.Space))
        {
            OnSpaceReleased();
        }

        // State에 따라 다른 동작
        switch (currentState)
        {
            case PlayerState.OnSafeZone:
                UpdateOnSafeZone();
                break;
            case PlayerState.Capturing:
                UpdateCapturing();
                break;
            case PlayerState.Returning:
                UpdateReturning();
                break;
        }
    }

    void UpdateOnSafeZone()
    {
        Vector2 input = GetInput();

        if (input == Vector2.zero)
        {
            isMoving = false;
            AnimatePulse();
        }
        else
        {
            isMoving = true;
            transform.localScale = originalScale;
            MoveOnBorder(input);
        }
    }

    void UpdateCapturing()
    {
        // 조이스틱 방향으로 직진
        Vector2 input = GetInput();

        if (input != Vector2.zero)
        {
            transform.localScale = originalScale;
            Vector3 movement = (Vector3)input * moveSpeed * Time.deltaTime;
            transform.position += movement;

            // 경로 기록
            capturePath.Add(transform.position);
            UpdatePathLine();

            // 안전지대를 벗어났는지 체크
            Vector3 snappedPos;
            bool onSafeZone = IsOnSafeZone(out snappedPos);

            if (!onSafeZone)
            {
                // 안전지대를 벗어남
                hasLeftSafeZone = true;
            }

            // 점령 완료 조건: 안전지대를 벗어났다가 다시 돌아옴 + 시작점에서 충분히 멀어짐
            if (hasLeftSafeZone && onSafeZone && Vector3.Distance(transform.position, captureStartPosition) > 0.5f)
            {
                CompleteCaptureTest(snappedPos);
            }
        }
    }

    bool IsOnSafeZone(out Vector3 snappedPosition)
    {
        Vector3 pos = transform.position;
        float x = pos.x;
        float y = pos.y;

        // 테두리에 닿았는지 체크 (약간의 오차 허용)
        float threshold = 0.15f;

        // 4개 테두리까지의 거리 계산
        float distBottom = Mathf.Abs(y - (-halfHeight));
        float distTop = Mathf.Abs(y - halfHeight);
        float distLeft = Mathf.Abs(x - (-halfWidth));
        float distRight = Mathf.Abs(x - halfWidth);

        // threshold 안에 있는 테두리 확인
        bool onBottomEdge = distBottom < threshold;
        bool onTopEdge = distTop < threshold;
        bool onLeftEdge = distLeft < threshold;
        bool onRightEdge = distRight < threshold;

        // 테두리에 닿지 않음
        if (!onBottomEdge && !onTopEdge && !onLeftEdge && !onRightEdge)
        {
            snappedPosition = pos;
            return false;
        }

        // 스냅된 위치 계산 (가장 가까운 테두리로)
        float snappedX = x;
        float snappedY = y;

        // Y축 스냅 (상단/하단)
        if (onBottomEdge && onTopEdge)
        {
            // 둘 다 threshold 안에 있으면 더 가까운 쪽
            snappedY = (distBottom < distTop) ? -halfHeight : halfHeight;
        }
        else if (onBottomEdge)
        {
            snappedY = -halfHeight;
        }
        else if (onTopEdge)
        {
            snappedY = halfHeight;
        }

        // X축 스냅 (좌측/우측)
        if (onLeftEdge && onRightEdge)
        {
            // 둘 다 threshold 안에 있으면 더 가까운 쪽
            snappedX = (distLeft < distRight) ? -halfWidth : halfWidth;
        }
        else if (onLeftEdge)
        {
            snappedX = -halfWidth;
        }
        else if (onRightEdge)
        {
            snappedX = halfWidth;
        }

        snappedPosition = new Vector3(snappedX, snappedY, 0);
        return true;
    }

    void CompleteCaptureTest(Vector3 snappedPosition)
    {
        Debug.Log($"영역 점령 완료! 시작점: {captureStartPosition}, 도착점: {snappedPosition}");

        // 스냅된 도착점을 경로에 추가
        capturePath.Add(snappedPosition);

        // 시작점과 도착점을 테두리를 따라 연결
        CloseCapturePathAlongBorder(captureStartPosition, snappedPosition);

        // 플레이어 위치도 스냅
        transform.position = snappedPosition;

        // 영역 제거 요청
        if (BlackAreaManager.Instance != null)
        {
            BlackAreaManager.Instance.RemoveCapturedArea(capturePath);
        }

        // 상태 복귀
        currentState = PlayerState.OnSafeZone;
        ClearPath();
    }

    void CloseCapturePathAlongBorder(Vector3 startPos, Vector3 endPos)
    {
        // 4개 모서리
        Vector3 bottomLeft = new Vector3(-halfWidth, -halfHeight, 0);
        Vector3 bottomRight = new Vector3(halfWidth, -halfHeight, 0);
        Vector3 topRight = new Vector3(halfWidth, halfHeight, 0);
        Vector3 topLeft = new Vector3(-halfWidth, halfHeight, 0);

        // 시작점과 도착점이 어느 변에 있는지 판단
        int startEdge = GetEdge(startPos);
        int endEdge = GetEdge(endPos);

        // 같은 변에 있으면 그냥 시작점 추가해서 닫기
        if (startEdge == endEdge)
        {
            capturePath.Add(startPos);
            return;
        }

        // 다른 변에 있으면 테두리를 따라 연결
        // 시계방향과 반시계방향 두 경로를 만들고 작은 영역 선택
        List<Vector3> clockwisePath = GetBorderPath(endPos, startPos, true);
        List<Vector3> counterClockwisePath = GetBorderPath(endPos, startPos, false);

        // 두 경로의 면적 계산
        List<Vector3> tempPathCW = new List<Vector3>(capturePath);
        tempPathCW.AddRange(clockwisePath);
        float areaCW = CalculatePolygonArea(tempPathCW);

        List<Vector3> tempPathCCW = new List<Vector3>(capturePath);
        tempPathCCW.AddRange(counterClockwisePath);
        float areaCCW = CalculatePolygonArea(tempPathCCW);

        // 면적이 작은 쪽 선택
        List<Vector3> smallerPath = Mathf.Abs(areaCW) <= Mathf.Abs(areaCCW) ? clockwisePath : counterClockwisePath;

        Debug.Log($"시계방향: {clockwisePath.Count}개(면적:{areaCW}), 반시계방향: {counterClockwisePath.Count}개(면적:{areaCCW}), 선택: {(smallerPath == clockwisePath ? "시계" : "반시계")}");

        // 선택된 경로 추가
        foreach (var point in smallerPath)
        {
            capturePath.Add(point);
        }
    }

    float CalculatePolygonArea(List<Vector3> polygon)
    {
        if (polygon.Count < 3) return 0;

        // Shoelace formula (신발끈 공식)
        float area = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector3 current = polygon[i];
            Vector3 next = polygon[(i + 1) % polygon.Count];
            area += (current.x * next.y) - (next.x * current.y);
        }
        return area / 2f;
    }

    int GetEdge(Vector3 pos)
    {
        // 0: 하단, 1: 우측, 2: 상단, 3: 좌측
        if (Mathf.Approximately(pos.y, -halfHeight)) return 0; // 하단
        if (Mathf.Approximately(pos.x, halfWidth)) return 1;   // 우측
        if (Mathf.Approximately(pos.y, halfHeight)) return 2;  // 상단
        if (Mathf.Approximately(pos.x, -halfWidth)) return 3;  // 좌측
        return -1; // 테두리가 아님
    }

    List<Vector3> GetBorderPath(Vector3 from, Vector3 to, bool clockwise)
    {
        List<Vector3> path = new List<Vector3>();

        // 4개 모서리
        Vector3[] corners = new Vector3[]
        {
            new Vector3(-halfWidth, -halfHeight, 0), // 0: 좌하
            new Vector3(halfWidth, -halfHeight, 0),  // 1: 우하
            new Vector3(halfWidth, halfHeight, 0),   // 2: 우상
            new Vector3(-halfWidth, halfHeight, 0)   // 3: 좌상
        };

        int fromEdge = GetEdge(from);
        int toEdge = GetEdge(to);

        // 모서리 순서 (시계방향: 0→1→2→3, 반시계방향: 0→3→2→1)
        int current = fromEdge;
        int target = toEdge;

        while (current != target)
        {
            // 다음 모서리로 이동
            if (clockwise)
            {
                current = (current + 1) % 4;
            }
            else
            {
                current = (current - 1 + 4) % 4;
            }

            // 해당 모서리 추가
            path.Add(corners[current]);
        }

        // 도착점 추가
        path.Add(to);

        return path;
    }

    void UpdateReturning()
    {
        // 시작 위치로 복귀
        float speed = moveSpeed * 2f; // 복귀는 빠르게
        transform.position = Vector3.MoveTowards(transform.position, captureStartPosition, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, captureStartPosition) < 0.01f)
        {
            transform.position = captureStartPosition;
            currentState = PlayerState.OnSafeZone;
            ClearPath();
        }
    }

    void AnimatePulse()
    {
        // Sin 파형으로 0.8 ~ 1.2 사이 진동
        float scale = Mathf.Lerp(minScale, maxScale, (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2) + 1f) / 2f);
        transform.localScale = originalScale * scale;
    }

    Vector2 GetInput()
    {
        // 조이스틱 입력
        Vector2 joystickInput = joystick.Direction;

        // 키보드 입력 (WASD + 화살표키)
        Vector2 keyboardInput = Vector2.zero;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            keyboardInput = Vector2.up;
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            keyboardInput = Vector2.down;
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            keyboardInput = Vector2.left;
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            keyboardInput = Vector2.right;

        // 조이스틱 입력 우선, 없으면 키보드 입력
        return joystickInput != Vector2.zero ? joystickInput : keyboardInput;
    }

    void MoveOnBorder(Vector2 direction)
    {
        Vector3 currentPos = transform.position;
        Vector3 targetPos = currentPos + (Vector3)direction * moveSpeed * Time.deltaTime;

        // 어느 테두리에 있는지 판단하고 이동 제한
        Vector3 newPos = ClampToBorder(currentPos, targetPos, direction);

        transform.position = newPos;
    }

    Vector3 ClampToBorder(Vector3 currentPos, Vector3 targetPos, Vector2 direction)
    {
        float x = currentPos.x;
        float y = currentPos.y;

        // 현재 어느 테두리에 있는지 판단
        bool onBottomEdge = Mathf.Approximately(y, -halfHeight);
        bool onTopEdge = Mathf.Approximately(y, halfHeight);
        bool onLeftEdge = Mathf.Approximately(x, -halfWidth);
        bool onRightEdge = Mathf.Approximately(x, halfWidth);

        // 하단 테두리 (y = -7)
        if (onBottomEdge)
        {
            if (direction == Vector2.right || direction == Vector2.left)
            {
                // 좌우 이동 가능
                float newX = Mathf.Clamp(targetPos.x, -halfWidth, halfWidth);
                return new Vector3(newX, -halfHeight, 0);
            }
            else if (direction == Vector2.up)
            {
                // 모서리에서만 위로 이동 가능
                if (Mathf.Approximately(x, -halfWidth) || Mathf.Approximately(x, halfWidth))
                {
                    float newY = Mathf.Clamp(targetPos.y, -halfHeight, halfHeight);
                    return new Vector3(x, newY, 0);
                }
            }
        }

        // 상단 테두리 (y = 7)
        else if (onTopEdge)
        {
            if (direction == Vector2.right || direction == Vector2.left)
            {
                // 좌우 이동 가능
                float newX = Mathf.Clamp(targetPos.x, -halfWidth, halfWidth);
                return new Vector3(newX, halfHeight, 0);
            }
            else if (direction == Vector2.down)
            {
                // 모서리에서만 아래로 이동 가능
                if (Mathf.Approximately(x, -halfWidth) || Mathf.Approximately(x, halfWidth))
                {
                    float newY = Mathf.Clamp(targetPos.y, -halfHeight, halfHeight);
                    return new Vector3(x, newY, 0);
                }
            }
        }

        // 좌측 테두리 (x = -5)
        else if (onLeftEdge)
        {
            if (direction == Vector2.up || direction == Vector2.down)
            {
                // 상하 이동 가능
                float newY = Mathf.Clamp(targetPos.y, -halfHeight, halfHeight);
                return new Vector3(-halfWidth, newY, 0);
            }
            else if (direction == Vector2.right)
            {
                // 모서리에서만 오른쪽 이동 가능
                if (Mathf.Approximately(y, -halfHeight) || Mathf.Approximately(y, halfHeight))
                {
                    float newX = Mathf.Clamp(targetPos.x, -halfWidth, halfWidth);
                    return new Vector3(newX, y, 0);
                }
            }
        }

        // 우측 테두리 (x = 5)
        else if (onRightEdge)
        {
            if (direction == Vector2.up || direction == Vector2.down)
            {
                // 상하 이동 가능
                float newY = Mathf.Clamp(targetPos.y, -halfHeight, halfHeight);
                return new Vector3(halfWidth, newY, 0);
            }
            else if (direction == Vector2.left)
            {
                // 모서리에서만 왼쪽 이동 가능
                if (Mathf.Approximately(y, -halfHeight) || Mathf.Approximately(y, halfHeight))
                {
                    float newX = Mathf.Clamp(targetPos.x, -halfWidth, halfWidth);
                    return new Vector3(newX, y, 0);
                }
            }
        }

        // 이동 불가능한 경우 현재 위치 유지
        return currentPos;
    }

    // 스페이스 버튼/키 눌림 (UI 버튼에서 호출)
    public void OnSpacePressed()
    {
        if (currentState == PlayerState.OnSafeZone)
        {
            spacePressed = true;
            currentState = PlayerState.Capturing;
            captureStartPosition = transform.position;
            capturePath.Clear();
            capturePath.Add(captureStartPosition);
            hasLeftSafeZone = false; // 초기화
            Debug.Log("영역 점령 시작!");
        }
    }

    // 스페이스 버튼/키 뗌 (UI 버튼에서 호출)
    public void OnSpaceReleased()
    {
        if (currentState == PlayerState.Capturing)
        {
            spacePressed = false;
            // 즉시 원위치로
            transform.position = captureStartPosition;
            currentState = PlayerState.OnSafeZone;
            ClearPath();
            Debug.Log("영역 점령 취소 - 즉시 복귀");
        }
    }

    void UpdatePathLine()
    {
        pathLine.positionCount = capturePath.Count;
        pathLine.SetPositions(capturePath.ToArray());
    }

    void ClearPath()
    {
        capturePath.Clear();
        pathLine.positionCount = 0;
    }
}
