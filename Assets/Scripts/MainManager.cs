using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MainSceneManager : MonoBehaviour
{
    [Header("페이드인 설정")]
    public CanvasGroup mainCanvasGroup; // Canvas Group 사용
    public float fadeInDuration = 0.8f;
    
    void Start()
    {
        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.alpha = 0.7f; // 살짝 투명하게 시작
            StartCoroutine(FadeInMainScene());
        }
    }
    
    IEnumerator FadeInMainScene()
    {
        yield return new WaitForSeconds(0.2f); // 약간의 딜레이
        
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(0.7f, 1f, elapsedTime / fadeInDuration);
            
            mainCanvasGroup.alpha = alpha;
            yield return null;
        }
        
        mainCanvasGroup.alpha = 1f;
    }
}