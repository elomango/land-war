using UnityEngine;
using System.Collections;

public class FloatingAnimation : MonoBehaviour
{
    [Header("애니메이션 설정")]
    public float floatSpeed = 2f;           // 애니메이션 속도
    public float floatAmplitude = 30f;      // 위아래 움직임 거리 (픽셀)
    public bool autoStart = true;           // 자동 시작 여부
    
    [Header("랜덤 설정")]
    public bool randomStartPosition = true;  // 랜덤 시작 위치
    public float randomDelay = 0f;          // 시작 지연 시간 (초)
    
    private Vector3 originalPosition;       // 원래 위치 저장
    private bool isFloating = false;        // 애니메이션 실행 중인지
    
    void Start()
    {
        // 원래 위치 저장
        originalPosition = transform.localPosition;
        
        if (autoStart)
        {
            // 랜덤 지연 후 시작
            if (randomDelay > 0)
            {
                Invoke("StartFloating", randomDelay);
            }
            else
            {
                StartFloating();
            }
        }
    }
    
    public void StartFloating()
    {
        if (!isFloating)
        {
            isFloating = true;
            StartCoroutine(FloatingEffect());
        }
    }
    
    public void StopFloating()
    {
        isFloating = false;
        StopAllCoroutines();
        
        // 원래 위치로 부드럽게 복귀
        StartCoroutine(ReturnToOriginal());
    }
    
    IEnumerator FloatingEffect()
    {
        float randomOffset = 0f;
        
        // 랜덤 시작 위치 설정
        if (randomStartPosition)
        {
            randomOffset = Random.Range(0f, Mathf.PI * 2);
        }
        
        while (isFloating)
        {
            // Sin 함수를 사용한 부드러운 위아래 움직임
            float yOffset = Mathf.Sin((Time.time * floatSpeed) + randomOffset) * floatAmplitude;
            
            // 새로운 위치 계산
            Vector3 newPosition = originalPosition + new Vector3(0, yOffset, 0);
            transform.localPosition = newPosition;
            
            yield return null; // 다음 프레임까지 대기
        }
    }
    
    IEnumerator ReturnToOriginal()
    {
        float duration = 0.5f; // 복귀 시간
        Vector3 startPosition = transform.localPosition;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            
            // 부드럽게 원래 위치로 복귀
            transform.localPosition = Vector3.Lerp(startPosition, originalPosition, progress);
            yield return null;
        }
        
        transform.localPosition = originalPosition;
    }
}