using UnityEngine;
using System.Collections.Generic;

public class SafeZoneManager : MonoBehaviour
{
    [Header("Play Area Settings")]
    public float playAreaWidth = 10f;   // 가로 10 유닛
    public float playAreaHeight = 14f;  // 세로 14 유닛

    private LineRenderer lineRenderer;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.alignment = LineAlignment.TransformZ;
        lineRenderer.numCornerVertices = 0;
        lineRenderer.numCapVertices = 0;
        lineRenderer.useWorldSpace = true;  // 월드 좌표 사용

        InitializeBorder();
    }

    void InitializeBorder()
    {
        // 테두리 4개 꼭지점 계산
        float halfWidth = playAreaWidth / 2f;   // 5
        float halfHeight = playAreaHeight / 2f; // 7

        Vector3[] borderPoints = new Vector3[5]
        {
            new Vector3(-halfWidth, halfHeight, 0),   // 좌상 (-5, 7)
            new Vector3(halfWidth, halfHeight, 0),    // 우상 (5, 7)
            new Vector3(halfWidth, -halfHeight, 0),   // 우하 (5, -7)
            new Vector3(-halfWidth, -halfHeight, 0),  // 좌하 (-5, -7)
            new Vector3(-halfWidth, halfHeight, 0)    // 다시 좌상 (닫힌 선)
        };

        // LineRenderer에 좌표 설정
        lineRenderer.positionCount = borderPoints.Length;
        lineRenderer.SetPositions(borderPoints);

        Debug.Log("안전지대 테두리 초기화 완료");
    }

    public void UpdateBorder(List<Vector2> polygonPoints)
    {
        if (polygonPoints == null || polygonPoints.Count < 3)
        {
            Debug.LogWarning("테두리 업데이트 실패: 점이 부족합니다");
            return;
        }

        Debug.Log("=== UpdateBorder 호출 ===");
        Debug.Log($"변경 전 - alignment: {lineRenderer.alignment}, cornerVertices: {lineRenderer.numCornerVertices}, useWorldSpace: {lineRenderer.useWorldSpace}");

        // Vector2 → Vector3 변환 (닫힌 폴리곤으로)
        Vector3[] borderPoints = new Vector3[polygonPoints.Count + 1];
        for (int i = 0; i < polygonPoints.Count; i++)
        {
            borderPoints[i] = new Vector3(polygonPoints[i].x, polygonPoints[i].y, 0);
        }
        // 마지막 점을 첫 점과 연결 (닫힌 선)
        borderPoints[polygonPoints.Count] = new Vector3(polygonPoints[0].x, polygonPoints[0].y, 0);

        // LineRenderer 업데이트
        lineRenderer.alignment = LineAlignment.TransformZ;
        lineRenderer.numCornerVertices = 0;
        lineRenderer.numCapVertices = 0;
        lineRenderer.useWorldSpace = true;

        lineRenderer.positionCount = borderPoints.Length;
        lineRenderer.SetPositions(borderPoints);

        Debug.Log($"변경 후 - alignment: {lineRenderer.alignment}, cornerVertices: {lineRenderer.numCornerVertices}, useWorldSpace: {lineRenderer.useWorldSpace}");

        // widthCurve 확인
        Debug.Log($"widthCurve - 시작(0): {lineRenderer.widthCurve.Evaluate(0)}, 중간(0.5): {lineRenderer.widthCurve.Evaluate(0.5f)}, 끝(1): {lineRenderer.widthCurve.Evaluate(1)}");

        // 왼쪽 테두리 점들 확인 (x좌표가 거의 같아야 함)
        Debug.Log("왼쪽 테두리 점들:");
        for (int i = 0; i < Mathf.Min(10, polygonPoints.Count); i++)
        {
            if (polygonPoints[i].x < -4.5f)  // 왼쪽 테두리 근처
            {
                Debug.Log($"  점 {i}: ({polygonPoints[i].x:F4}, {polygonPoints[i].y:F4})");
            }
        }
    }
}
