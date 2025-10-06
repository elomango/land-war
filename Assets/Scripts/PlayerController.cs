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

    // 영역 점령 관련
    private Vector3 captureStartPosition;
    private List<Vector3> capturePath = new List<Vector3>();
    private LineRenderer pathLine;
    private bool hasLeftSafeZone = false; // 안전지대를 벗어났는지 체크
    private Vector2 previousInputDirection = Vector2.zero; // 이전 입력 방향 (꼭지점 감지용)

    // 동적 안전영역 관련
    private List<Vector2> currentBorderPolygon; // 현재 안전영역 폴리곤

    [Header("Grid Settings")]
    [SerializeField] private float gridSize = 0.05f; // 그리드 크기 (최소 이동 단위)

    void Start()
    {
        halfWidth = playAreaWidth / 2f;   // 5
        halfHeight = playAreaHeight / 2f; // 7
        originalScale = transform.localScale;

        // 초기 폴리곤 데이터 가져오기
        UpdateBorderPolygon();

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
            AnimatePulse();
        }
        else
        {
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

            // 입력 방향 정규화 (4방향으로 스냅)
            Vector2 normalizedInput = NormalizeToFourDirections(input);

            // 방향 전환 감지 → 꼭지점 추가
            if (normalizedInput != previousInputDirection && previousInputDirection != Vector2.zero)
            {
                // 방향이 바뀔 때 현재 위치를 0.05 그리드에 스냅
                Vector3 snappedCorner = SnapToGrid(transform.position);
                transform.position = snappedCorner;

                // 스냅된 위치를 꼭지점으로 추가
                if (capturePath.Count == 0 || Vector3.Distance(snappedCorner, capturePath[capturePath.Count - 1]) >= 0.1f)
                {
                    capturePath.Add(snappedCorner);
                }
            }

            // 이전 방향 업데이트
            previousInputDirection = normalizedInput;

            // 이동
            Vector3 movement = (Vector3)normalizedInput * moveSpeed * Time.deltaTime;
            transform.position += movement;

            // 플레이 영역 안에 제한
            float clampedX = Mathf.Clamp(transform.position.x, -halfWidth, halfWidth);
            float clampedY = Mathf.Clamp(transform.position.y, -halfHeight, halfHeight);
            transform.position = new Vector3(clampedX, clampedY, 0);

            // 노란색 경로 라인 실시간 업데이트 (매 프레임)
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
            // 최소 거리 = 그리드 크기 * 2 (왕복 최소 거리)
            if (hasLeftSafeZone && onSafeZone && Vector3.Distance(transform.position, captureStartPosition) > gridSize * 2f)
            {
                CompleteCaptureTest(snappedPos);
            }
        }
    }

    // 입력을 4방향(상하좌우)으로 정규화
    Vector2 NormalizeToFourDirections(Vector2 input)
    {
        if (input == Vector2.zero)
            return Vector2.zero;

        // X축과 Y축 중 절댓값이 큰 쪽으로 스냅
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
        {
            // 좌우 이동
            return input.x > 0 ? Vector2.right : Vector2.left;
        }
        else
        {
            // 상하 이동
            return input.y > 0 ? Vector2.up : Vector2.down;
        }
    }

    bool IsOnSafeZone(out Vector3 snappedPosition)
    {
        Vector3 pos = transform.position;
        float threshold = gridSize;

        // 동적 폴리곤 기반 테두리 체크
        if (currentBorderPolygon == null || currentBorderPolygon.Count < 2)
        {
            snappedPosition = pos;
            return false;
        }

        Vector2 pos2D = new Vector2(pos.x, pos.y);
        float minDistance = float.MaxValue;
        Vector2 closestPoint = pos2D;

        // 폴리곤의 모든 선분과의 거리 계산
        for (int i = 0; i < currentBorderPolygon.Count; i++)
        {
            Vector2 p1 = currentBorderPolygon[i];
            Vector2 p2 = currentBorderPolygon[(i + 1) % currentBorderPolygon.Count];

            Vector2 closest = ClosestPointOnLineSegment(pos2D, p1, p2);
            float distance = Vector2.Distance(pos2D, closest);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestPoint = closest;
            }
        }

        // threshold 안에 있으면 테두리에 있다고 판단
        if (minDistance < threshold)
        {
            snappedPosition = new Vector3(closestPoint.x, closestPoint.y, 0);
            return true;
        }

        snappedPosition = pos;
        return false;
    }

    void CompleteCaptureTest(Vector3 snappedPosition)
    {
        Debug.Log($"영역 점령 완료! 시작점: {captureStartPosition}, 도착점: {snappedPosition}");

        // 시작점과 도착점을 테두리를 따라 연결 (CloseCapturePathAlongBorder에서 점들을 추가함)
        CloseCapturePathAlongBorder(captureStartPosition, snappedPosition);

        // capturePath 전체 출력 (디버깅용)
        Debug.Log("=== capturePath 전체 출력 ===");
        for (int i = 0; i < capturePath.Count; i++)
        {
            Debug.Log($"  점 {i}: {capturePath[i]}");
        }

        // 경로 검증 (사선 체크)
        ValidatePath();

        // 플레이어 위치도 스냅
        transform.position = snappedPosition;

        // 영역 제거 요청
        if (BlackAreaManager.Instance != null)
        {
            BlackAreaManager.Instance.RemoveCapturedArea(capturePath);

            // 폴리곤 데이터 업데이트 (2단계: 영역 점령 후 데이터 갱신)
            UpdateBorderPolygon();
        }

        // 상태 복귀
        currentState = PlayerState.OnSafeZone;
        ClearPath();
    }

    // 경로 검증: 연속된 점들이 직각(x 또는 y 같음)인지 체크
    void ValidatePath()
    {
        float tolerance = 0.01f; // 부동소수점 오차 허용

        for (int i = 0; i < capturePath.Count - 1; i++)
        {
            Vector3 p1 = capturePath[i];
            Vector3 p2 = capturePath[i + 1];

            bool xSame = Mathf.Abs(p1.x - p2.x) < tolerance;
            bool ySame = Mathf.Abs(p1.y - p2.y) < tolerance;

            // x나 y 중 하나는 반드시 같아야 함 (직각)
            if (!xSame && !ySame)
            {
                Debug.LogError($"❌ 사선 발견! 점 {i} → {i+1}: {p1} → {p2}");
                Debug.LogError($"   X 차이: {Mathf.Abs(p1.x - p2.x):F4}, Y 차이: {Mathf.Abs(p1.y - p2.y):F4}");
            }
        }
    }

    void CloseCapturePathAlongBorder(Vector3 startPos, Vector3 endPos)
    {
        if (currentBorderPolygon == null || currentBorderPolygon.Count < 2)
        {
            Debug.LogError("currentBorderPolygon이 없습니다");
            return;
        }

        // 시작점과 도착점이 어느 선분에 있는지 찾기
        int startEdgeIndex = FindEdgeIndex(startPos);
        int endEdgeIndex = FindEdgeIndex(endPos);

        if (startEdgeIndex == -1 || endEdgeIndex == -1)
        {
            Debug.LogError($"시작점 또는 도착점이 테두리에 없음: startEdge={startEdgeIndex}, endEdge={endEdgeIndex}");
            return;
        }

        // 마지막 움직임과 도착 지점 사이의 미세한 차이 조정
        if (capturePath.Count >= 2)
        {
            Vector3 lastPoint = capturePath[capturePath.Count - 1];
            Vector3 prevPoint = capturePath[capturePath.Count - 2];

            float xDiff = Mathf.Abs(lastPoint.x - prevPoint.x);
            float yDiff = Mathf.Abs(lastPoint.y - prevPoint.y);

            const float kDiffThreshold = 0.1f;

            Debug.Log($"=== 마지막 점 조정 체크 ===");
            Debug.Log($"  prevPoint: {prevPoint}, lastPoint: {lastPoint}, endPos: {endPos}");
            Debug.Log($"  xDiff: {xDiff}, yDiff: {yDiff}");

            // 수평 이동 (Y가 같음)이었고, endPos와 Y 차이가 작으면
            if (yDiff < 0.01f && Mathf.Abs(lastPoint.y - endPos.y) <= kDiffThreshold)
            {
                Debug.Log($"  수평 이동 조정: lastPoint.y {lastPoint.y} → {endPos.y}");
                lastPoint.y = endPos.y;
                capturePath[capturePath.Count - 1] = lastPoint;
            }
            // 수직 이동 (X가 같음)이었고, endPos와 X 차이가 작으면
            else if (xDiff < 0.01f && Mathf.Abs(lastPoint.x - endPos.x) <= kDiffThreshold)
            {
                Debug.Log($"  수직 이동 조정: lastPoint.x {lastPoint.x} → {endPos.x}");
                lastPoint.x = endPos.x;
                capturePath[capturePath.Count - 1] = lastPoint;
            }
            else
            {
                Debug.Log($"  조정 안 함");
            }
        }

        // 사선 방지: 선분 방향에 맞춰 startPos와 endPos 조정
        Vector2 startP1 = currentBorderPolygon[startEdgeIndex];
        Vector2 startP2 = currentBorderPolygon[(startEdgeIndex + 1) % currentBorderPolygon.Count];
        Vector2 endP1 = currentBorderPolygon[endEdgeIndex];
        Vector2 endP2 = currentBorderPolygon[(endEdgeIndex + 1) % currentBorderPolygon.Count];

        bool startIsVertical = Mathf.Abs(startP1.x - startP2.x) < 0.01f;  // 수직 선분
        bool endIsVertical = Mathf.Abs(endP1.x - endP2.x) < 0.01f;        // 수직 선분

        // 방향에 따라 조정
        if (startIsVertical && endIsVertical)
        {
            // 둘 다 수직(X 고정) → Y를 맞춤
            startPos.y = endPos.y;
        }
        else if (!startIsVertical && !endIsVertical)
        {
            // 둘 다 수평(Y 고정) → X를 맞춤
            startPos.x = endPos.x;
        }
        else if (startIsVertical && !endIsVertical)
        {
            // start는 수직(X 고정), end는 수평(Y 고정)
            // startPos의 Y를 endPos의 Y에 맞춤
            startPos.y = endPos.y;
        }
        else
        {
            // start는 수평(Y 고정), end는 수직(X 고정)
            // startPos의 X를 endPos의 X에 맞춤
            startPos.x = endPos.x;
        }

        // 같은 선분에 있으면 직선 연결
        if (startEdgeIndex == endEdgeIndex)
        {
            if (capturePath.Count == 0 || Vector3.Distance(capturePath[capturePath.Count - 1], endPos) > 0.01f)
            {
                capturePath.Add(endPos);
            }
            capturePath.Add(startPos);
            return;
        }

        // 다른 선분 → 시계/반시계 경로 생성

        // 시계방향 경로
        List<Vector3> clockwisePath = new List<Vector3>();
        clockwisePath.Add(endPos);

        int currentIndex = (endEdgeIndex + 1) % currentBorderPolygon.Count;
        while (currentIndex != startEdgeIndex)
        {
            clockwisePath.Add(new Vector3(currentBorderPolygon[currentIndex].x, currentBorderPolygon[currentIndex].y, 0));
            currentIndex = (currentIndex + 1) % currentBorderPolygon.Count;
        }

        clockwisePath.Add(new Vector3(currentBorderPolygon[startEdgeIndex].x, currentBorderPolygon[startEdgeIndex].y, 0));
        clockwisePath.Add(startPos);

        // 반시계방향 경로
        List<Vector3> counterClockwisePath = new List<Vector3>();
        counterClockwisePath.Add(endPos);

        currentIndex = endEdgeIndex;
        int targetIndex = (startEdgeIndex + 1) % currentBorderPolygon.Count;

        while (currentIndex != targetIndex)
        {
            counterClockwisePath.Add(new Vector3(currentBorderPolygon[currentIndex].x, currentBorderPolygon[currentIndex].y, 0));
            currentIndex = (currentIndex - 1 + currentBorderPolygon.Count) % currentBorderPolygon.Count;
        }

        counterClockwisePath.Add(new Vector3(currentBorderPolygon[targetIndex].x, currentBorderPolygon[targetIndex].y, 0));
        counterClockwisePath.Add(startPos);

        // 두 경로 중 작은 영역 선택
        List<Vector3> tempPathCW = new List<Vector3>(capturePath);
        tempPathCW.AddRange(clockwisePath);
        float areaCW = CalculatePolygonArea(tempPathCW);

        List<Vector3> tempPathCCW = new List<Vector3>(capturePath);
        tempPathCCW.AddRange(counterClockwisePath);
        float areaCCW = CalculatePolygonArea(tempPathCCW);

        List<Vector3> smallerPath = Mathf.Abs(areaCW) <= Mathf.Abs(areaCCW) ? clockwisePath : counterClockwisePath;

        Debug.Log($"시계방향: {clockwisePath.Count}개(면적:{areaCW}), 반시계방향: {counterClockwisePath.Count}개(면적:{areaCCW}), 선택: {(smallerPath == clockwisePath ? "시계" : "반시계")}");

        // 선택된 경로 추가
        foreach (var point in smallerPath)
        {
            capturePath.Add(point);
        }
    }

    // 주어진 위치가 어느 선분에 있는지 찾기
    int FindEdgeIndex(Vector3 pos)
    {
        if (currentBorderPolygon == null || currentBorderPolygon.Count < 2)
            return -1;

        float threshold = 0.15f; // IsOnSafeZone과 동일한 임계값
        Vector2 pos2D = new Vector2(pos.x, pos.y);

        for (int i = 0; i < currentBorderPolygon.Count; i++)
        {
            Vector2 p1 = currentBorderPolygon[i];
            Vector2 p2 = currentBorderPolygon[(i + 1) % currentBorderPolygon.Count];

            Vector2 closestPoint = ClosestPointOnLineSegment(pos2D, p1, p2);
            float distance = Vector2.Distance(pos2D, closestPoint);

            if (distance < threshold)
            {
                return i; // 선분 인덱스 반환
            }
        }

        return -1; // 테두리에 없음
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

        // 동적 폴리곤 테두리를 따라 이동
        Vector3 newPos = ClampToBorderDynamic(currentPos, targetPos, direction);

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
            currentState = PlayerState.Capturing;

            // 캡처 시작 위치를 0.05 그리드에 스냅
            captureStartPosition = SnapToGrid(transform.position);
            transform.position = captureStartPosition;

            capturePath.Clear();
            capturePath.Add(captureStartPosition);
            hasLeftSafeZone = false; // 초기화
            previousInputDirection = Vector2.zero; // 방향 초기화
            Debug.Log($"영역 점령 시작! 시작 위치: {captureStartPosition}");
        }
    }

    // 스페이스 버튼/키 뺌 (UI 버튼에서 호출)
    public void OnSpaceReleased()
    {
        if (currentState == PlayerState.Capturing)
        {
            // 즉시 원위치로
            transform.position = captureStartPosition;
            currentState = PlayerState.OnSafeZone;
            ClearPath();
            Debug.Log("영역 점령 취소 - 즉시 복귀");
        }
    }

    void UpdatePathLine()
    {
        // capturePath (꼭지점들) + 현재 플레이어 위치까지 표시
        if (capturePath.Count == 0)
        {
            pathLine.positionCount = 0;
            return;
        }

        // capturePath의 마지막 점이 현재 위치와 다르면 현재 위치 추가
        Vector3[] pathPoints;
        if (capturePath[capturePath.Count - 1] != transform.position)
        {
            pathPoints = new Vector3[capturePath.Count + 1];
            for (int i = 0; i < capturePath.Count; i++)
            {
                pathPoints[i] = capturePath[i];
            }
            pathPoints[capturePath.Count] = transform.position; // 현재 위치 추가
        }
        else
        {
            pathPoints = capturePath.ToArray();
        }

        pathLine.positionCount = pathPoints.Length;
        pathLine.SetPositions(pathPoints);
    }

    void ClearPath()
    {
        capturePath.Clear();
        pathLine.positionCount = 0;
    }

    // ========== 동적 안전영역 관련 함수 ==========

    void UpdateBorderPolygon()
    {
        if (BlackAreaManager.Instance != null)
        {
            currentBorderPolygon = BlackAreaManager.Instance.GetBorderPolygon();
            Debug.Log($"폴리곤 데이터 업데이트: {currentBorderPolygon.Count}개 점");
        }
        else
        {
            Debug.LogWarning("BlackAreaManager.Instance가 없습니다.");
        }
    }

    // 점에서 선분까지의 최단거리 점 계산
    Vector2 ClosestPointOnLineSegment(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
    {
        Vector2 line = lineEnd - lineStart;
        float lineLength = line.magnitude;

        if (lineLength < 0.001f)
            return lineStart;

        Vector2 lineDirection = line / lineLength;
        Vector2 toPoint = point - lineStart;

        float projection = Vector2.Dot(toPoint, lineDirection);
        projection = Mathf.Clamp(projection, 0, lineLength);

        return lineStart + lineDirection * projection;
    }

    // 위치를 0.05 그리드에 스냅
    Vector3 SnapToGrid(Vector3 position)
    {
        float x = Mathf.Round(position.x / gridSize) * gridSize;
        float y = Mathf.Round(position.y / gridSize) * gridSize;
        return new Vector3(x, y, 0);
    }

    // 플레이어 위치에서 가장 가까운 선분의 인덱스 찾기
    int GetClosestEdgeIndex(Vector3 position)
    {
        if (currentBorderPolygon == null || currentBorderPolygon.Count < 2)
        {
            Debug.LogWarning("폴리곤 데이터가 없습니다.");
            return -1;
        }

        Vector2 pos2D = new Vector2(position.x, position.y);
        int closestEdgeIndex = -1;
        float minDistance = float.MaxValue;

        for (int i = 0; i < currentBorderPolygon.Count; i++)
        {
            Vector2 p1 = currentBorderPolygon[i];
            Vector2 p2 = currentBorderPolygon[(i + 1) % currentBorderPolygon.Count];

            Vector2 closest = ClosestPointOnLineSegment(pos2D, p1, p2);
            float distance = Vector2.Distance(pos2D, closest);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestEdgeIndex = i;
            }
        }

        return closestEdgeIndex;
    }

    // 동적 폴리곤 테두리를 따라 이동 (1-4: 선분 위 이동 + 1-5: 모서리 이동)
    Vector3 ClampToBorderDynamic(Vector3 currentPos, Vector3 targetPos, Vector2 direction)
    {
        if (currentBorderPolygon == null || currentBorderPolygon.Count < 2)
        {
            Debug.LogWarning("폴리곤 데이터 없음");
            return currentPos;
        }

        // 현재 가장 가까운 선분 찾기
        int edgeIndex = GetClosestEdgeIndex(currentPos);
        if (edgeIndex == -1)
        {
            Debug.LogWarning("선분을 찾을 수 없음");
            return currentPos;
        }

        Vector2 p1 = currentBorderPolygon[edgeIndex];
        Vector2 p2 = currentBorderPolygon[(edgeIndex + 1) % currentBorderPolygon.Count];

        // 1-5: 모서리 체크 (꼭짓점 근처인지)
        Vector2 currentPos2D = new Vector2(currentPos.x, currentPos.y);
        float vertexThreshold = 0.05f;

        bool atVertex1 = Vector2.Distance(currentPos2D, p1) < vertexThreshold;
        bool atVertex2 = Vector2.Distance(currentPos2D, p2) < vertexThreshold;

        if (atVertex1 || atVertex2)
        {
            // 모서리에 있으면 방향에 따라 인접 선분으로 전환 가능
            Vector2 targetVertex = atVertex1 ? p1 : p2;
            int vertexIndex = atVertex1 ? edgeIndex : (edgeIndex + 1) % currentBorderPolygon.Count;

            // 이동 방향에 맞는 선분 선택
            int newEdgeIndex = SelectEdgeByDirection(vertexIndex, direction, atVertex1);

            if (newEdgeIndex != -1 && newEdgeIndex != edgeIndex)
            {
                edgeIndex = newEdgeIndex;
                p1 = currentBorderPolygon[edgeIndex];
                p2 = currentBorderPolygon[(edgeIndex + 1) % currentBorderPolygon.Count];

                // 선분 전환 후 다시 모서리 체크
                atVertex1 = Vector2.Distance(currentPos2D, p1) < vertexThreshold;
                atVertex2 = Vector2.Distance(currentPos2D, p2) < vertexThreshold;
            }
        }

        // 1-4: targetPos를 현재 선분 위로 클램프
        Vector2 targetPos2D = new Vector2(targetPos.x, targetPos.y);
        Vector2 clampedPos2D = ClosestPointOnLineSegment(targetPos2D, p1, p2);

        return new Vector3(clampedPos2D.x, clampedPos2D.y, 0);
    }

    // 1-5: 꼭짓점에서 이동 방향에 맞는 선분 선택
    int SelectEdgeByDirection(int vertexIndex, Vector2 direction, bool isAtStartVertex)
    {
        if (currentBorderPolygon == null || currentBorderPolygon.Count < 2)
            return -1;

        // 해당 꼭짓점에 연결된 두 선분
        int prevEdge = (vertexIndex - 1 + currentBorderPolygon.Count) % currentBorderPolygon.Count;
        int nextEdge = vertexIndex;

        Vector2 vertex = currentBorderPolygon[vertexIndex];

        // 이전 선분 방향
        Vector2 prevP1 = currentBorderPolygon[prevEdge];
        Vector2 prevP2 = vertex;  // prevEdge의 끝점 = 현재 vertex
        Vector2 prevDirForward = (prevP2 - prevP1).normalized;   // 정방향
        Vector2 prevDirBackward = (prevP1 - prevP2).normalized;  // 역방향

        // 다음 선분 방향
        Vector2 nextP1 = vertex;  // nextEdge의 시작점 = 현재 vertex
        Vector2 nextP2 = currentBorderPolygon[(nextEdge + 1) % currentBorderPolygon.Count];
        Vector2 nextDirForward = (nextP2 - nextP1).normalized;   // 정방향
        Vector2 nextDirBackward = (nextP1 - nextP2).normalized;  // 역방향

        // 이동 방향과의 내적 계산
        float dotPrevForward = Vector2.Dot(direction, prevDirForward);
        float dotPrevBackward = Vector2.Dot(direction, prevDirBackward);
        float dotNextForward = Vector2.Dot(direction, nextDirForward);
        float dotNextBackward = Vector2.Dot(direction, nextDirBackward);

        // isAtStartVertex=true: 현재 선분의 시작점(p1) 근처
        // -> prevEdge의 끝점에 있으므로, prevEdge를 역방향으로 타거나 nextEdge를 정방향으로 탈 수 있음
        if (isAtStartVertex)
        {
            // 가장 큰 내적값 찾기
            if (dotPrevBackward > dotNextForward && dotPrevBackward > 0.1f)
            {
                return prevEdge;
            }
            else if (dotNextForward > 0.1f)
            {
                return nextEdge;
            }
            // 둘 다 안 맞으면 덜 역방향인 쪽 선택
            else if (dotPrevBackward > dotNextForward)
            {
                return prevEdge;
            }
            else
            {
                return nextEdge;
            }
        }
        // isAtStartVertex=false: 현재 선분의 끝점(p2) 근처
        // -> nextEdge의 시작점에 있으므로, nextEdge를 정방향으로 타거나 prevEdge를 역방향으로 탈 수 있음
        else
        {
            // 가장 큰 내적값 찾기
            if (dotNextForward > dotPrevBackward && dotNextForward > 0.1f)
            {
                return nextEdge;
            }
            else if (dotPrevBackward > 0.1f)
            {
                return prevEdge;
            }
            // 둘 다 안 맞으면 덜 역방향인 쪽 선택
            else if (dotNextForward > dotPrevBackward)
            {
                return nextEdge;
            }
            else
            {
                return prevEdge;
            }
        }
    }
}
