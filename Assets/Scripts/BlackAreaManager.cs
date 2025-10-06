using UnityEngine;
using System.Collections.Generic;
using System;
using Clipper2Lib;

public class BlackAreaManager : MonoBehaviour
{
    public static BlackAreaManager Instance;

    [Header("Play Area Settings")]
    public float playAreaWidth = 10f;
    public float playAreaHeight = 14f;

    [Header("References")]
    public SafeZoneManager safeZoneManager;

    [Header("Grid Settings")]
    [SerializeField] private float gridSize = 0.05f; // 그리드 크기 (좌표 스냅 단위)

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private List<Vector2> blackPolygon; // 현재 검정 영역 (하나의 폴리곤)

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    void Start()
    {
        InitializeBlackArea();
    }

    void InitializeBlackArea()
    {
        // 초기 검정 영역: 플레이 영역 전체 사각형
        float halfWidth = playAreaWidth / 2f;   // 5
        float halfHeight = playAreaHeight / 2f; // 7

        blackPolygon = new List<Vector2>
        {
            new Vector2(-halfWidth, -halfHeight), // 좌하
            new Vector2(halfWidth, -halfHeight),  // 우하
            new Vector2(halfWidth, halfHeight),   // 우상
            new Vector2(-halfWidth, halfHeight)   // 좌상
        };

        GenerateMesh();
        Debug.Log("검정 영역 초기화 완료");
    }

    void GenerateMesh()
    {
        if (blackPolygon == null || blackPolygon.Count < 3)
        {
            Debug.LogWarning("폴리곤이 없거나 점이 부족합니다");
            return;
        }

        // 폴리곤을 삼각형으로 분할 (Triangulation)
        Mesh mesh = new Mesh();

        // 버텍스 생성
        Vector3[] vertices = new Vector3[blackPolygon.Count];
        for (int i = 0; i < blackPolygon.Count; i++)
        {
            vertices[i] = new Vector3(blackPolygon[i].x, blackPolygon[i].y, 0);
        }

        // 삼각형 인덱스 생성 (간단한 Fan Triangulation)
        int[] triangles = TriangulatePolygon(blackPolygon.Count);

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;
    }

    int[] TriangulatePolygon(int vertexCount)
    {
        if (vertexCount < 3) return new int[0];

        // Earcut 알고리즘 (오목/볼록 폴리곤 모두 지원)
        List<int> indices = new List<int>();
        List<int> remaining = new List<int>();

        for (int i = 0; i < vertexCount; i++)
            remaining.Add(i);

        while (remaining.Count > 3)
        {
            bool earFound = false;

            for (int i = 0; i < remaining.Count; i++)
            {
                int prev = remaining[(i - 1 + remaining.Count) % remaining.Count];
                int curr = remaining[i];
                int next = remaining[(i + 1) % remaining.Count];

                Vector2 a = blackPolygon[prev];
                Vector2 b = blackPolygon[curr];
                Vector2 c = blackPolygon[next];

                // 귀(ear) 판정: 볼록한 삼각형이고 내부에 다른 점이 없어야 함
                if (IsConvex(a, b, c) && !ContainsOtherPoints(a, b, c, remaining, prev, curr, next))
                {
                    indices.Add(prev);
                    indices.Add(curr);
                    indices.Add(next);
                    remaining.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }

            if (!earFound)
            {
                // 귀를 못 찾으면 Fan Triangulation으로 폴백
                Debug.LogWarning("Earcut 실패, Fan Triangulation 사용");
                return FallbackTriangulation(vertexCount);
            }
        }

        // 마지막 삼각형
        if (remaining.Count == 3)
        {
            indices.Add(remaining[0]);
            indices.Add(remaining[1]);
            indices.Add(remaining[2]);
        }

        return indices.ToArray();
    }

    bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
    {
        float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        return cross > 0; // 반시계 방향
    }

    bool ContainsOtherPoints(Vector2 a, Vector2 b, Vector2 c, List<int> points, int skipA, int skipB, int skipC)
    {
        foreach (int idx in points)
        {
            if (idx == skipA || idx == skipB || idx == skipC) continue;

            Vector2 p = blackPolygon[idx];
            if (IsPointInTriangle(p, a, b, c))
                return true;
        }
        return false;
    }

    bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);

        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(hasNeg && hasPos);
    }

    float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    int[] FallbackTriangulation(int vertexCount)
    {
        int triangleCount = vertexCount - 2;
        int[] triangles = new int[triangleCount * 3];

        for (int i = 0; i < triangleCount; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        return triangles;
    }

    // 영역 점령 시 호출
    public void RemoveCapturedArea(List<Vector3> capturePath)
    {
        if (capturePath == null || capturePath.Count < 3)
        {
            Debug.LogWarning("캡처 경로가 부족합니다");
            return;
        }

        // Unity Vector3 → Clipper2 PathD 변환
        PathD capturePoly = new PathD();
        foreach (var point in capturePath)
        {
            capturePoly.Add(new PointD(point.x, point.y));
        }

        // 폴리곤 방향 확인 및 수정 (반시계방향이어야 함)
        double captureArea = Clipper.Area(capturePoly);
        if (captureArea < 0)
        {
            capturePoly.Reverse();
        }

        // 현재 검정 영역을 Clipper2 PathD로 변환
        PathD blackPoly = new PathD();
        foreach (var point in blackPolygon)
        {
            blackPoly.Add(new PointD(point.x, point.y));
        }

        // 1단계: 캡처 경로와 검정 영역의 교집합 구하기 (실제 점령 영역)
        PathsD intersection = Clipper.Intersect(
            new PathsD { blackPoly },
            new PathsD { capturePoly },
            FillRule.NonZero
        );

        if (intersection.Count == 0)
        {
            Debug.LogWarning("캡처 영역이 검정 영역과 겹치지 않습니다.");
            return;
        }

        // 2단계: 검정 영역에서 점령 영역 제거
        PathsD solution = Clipper.Difference(
            new PathsD { blackPoly },
            intersection,
            FillRule.NonZero
        );

        // 결과가 없으면 (전체 영역 제거됨)
        if (solution.Count == 0)
        {
            Debug.Log("모든 영역이 제거되었습니다!");
            blackPolygon.Clear();
            GenerateMesh();
            return;
        }

        // 원본 검정 영역 넓이
        double originalArea = Math.Abs(Clipper.Area(blackPoly));

        // 원본과 가장 가까운(가장 큰) 폴리곤 찾기 = 영역이 제거된 결과
        PathD resultPoly = null;
        double resultArea = 0;
        int resultIndex = -1;

        for (int i = 0; i < solution.Count; i++)
        {
            double area = Math.Abs(Clipper.Area(solution[i]));

            // 가장 큰 폴리곤 선택 (원본보다는 작아야 함)
            if (area < originalArea && area > resultArea)
            {
                resultArea = area;
                resultPoly = solution[i];
                resultIndex = i;
            }
        }

        // 결과를 못 찾았으면, 혹시 원본이 그대로 있는지 확인
        if (resultPoly == null)
        {
            // 모든 폴리곤이 원본보다 크거나 같다면, 가장 작은 것 선택
            for (int i = 0; i < solution.Count; i++)
            {
                double area = Math.Abs(Clipper.Area(solution[i]));
                if (resultPoly == null || area < resultArea)
                {
                    resultArea = area;
                    resultPoly = solution[i];
                    resultIndex = i;
                }
            }
        }

        if (resultPoly == null)
        {
            Debug.LogWarning("유효한 결과 폴리곤을 찾지 못했습니다.");
            return;
        }

        // 폴리곤 방향 확인 (양수 넓이여야 함)
        if (Clipper.Area(resultPoly) < 0)
        {
            resultPoly.Reverse();
        }

        // Clipper2 PathD → Unity Vector2 변환 (grid에 스냅)
        blackPolygon.Clear();
        foreach (var point in resultPoly)
        {
            float x = Mathf.Round((float)point.x / gridSize) * gridSize;
            float y = Mathf.Round((float)point.y / gridSize) * gridSize;
            blackPolygon.Add(new Vector2(x, y));
        }

        // 중복/근접 점 제거
        RemoveDuplicatePoints(blackPolygon, gridSize * 0.5f);

        GenerateMesh();

        // SafeZoneManager 테두리 업데이트
        if (safeZoneManager != null)
        {
            safeZoneManager.UpdateBorder(blackPolygon);
        }
    }

    // 현재 검정 영역 폴리곤 반환 (안전영역 = 이 폴리곤의 테두리)
    public List<Vector2> GetBorderPolygon()
    {
        return blackPolygon;
    }

    // 중복/근접 점 제거
    void RemoveDuplicatePoints(List<Vector2> polygon, float threshold)
    {
        if (polygon.Count < 2) return;

        for (int i = polygon.Count - 1; i > 0; i--)
        {
            Vector2 current = polygon[i];
            Vector2 prev = polygon[i - 1];

            // 연속된 점이 매우 가까우면 제거
            if (Vector2.Distance(current, prev) < threshold)
            {
                polygon.RemoveAt(i);
            }
        }

        // 마지막 점과 첫 점도 체크 (닫힌 폴리곤)
        if (polygon.Count >= 2)
        {
            Vector2 first = polygon[0];
            Vector2 last = polygon[polygon.Count - 1];
            if (Vector2.Distance(first, last) < threshold)
            {
                polygon.RemoveAt(polygon.Count - 1);
            }
        }
    }
}
