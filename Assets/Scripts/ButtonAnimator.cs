using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;  // 씬 관리용 추가
using System.Collections;

public class ButtonAnimator : MonoBehaviour
{
    [Header("애니메이션 설정")]
    public float scaleMultiplier = 1.5f;
    public float animationDuration = 0.2f;
    
    [Header("사운드 설정")]
    public AudioSource audioSource;
    public AudioClip clickSound;
    
    [Header("씬 전환 설정")]
    public string targetSceneName = "GameScene";  // 이동할 씬 이름
    public float transitionDelay = 0.3f;         // 애니메이션 후 지연시간
    
    private Vector3 originalScale;
    private Button button;
    
    void Start()
    {
        button = GetComponent<Button>();
        originalScale = transform.localScale;
        
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }
    }
    
    public void OnButtonClick()
    {
        // 사운드 재생
        PlayClickSound();
        
        // 애니메이션 실행 후 씬 전환
        StartCoroutine(AnimationAndTransition());
    }
    
    void PlayClickSound()
    {
        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
        }
    }
    
    IEnumerator AnimationAndTransition()
    {
        // 1단계: 버튼 애니메이션 실행
        yield return StartCoroutine(ScaleAnimation());
        
        // 2단계: 잠시 대기 (사운드나 시각적 피드백을 위해)
        yield return new WaitForSeconds(transitionDelay);
        
        // 3단계: 게임 씬으로 전환
        SceneManager.LoadScene(targetSceneName);
    }
    
    IEnumerator ScaleAnimation()
    {
        // 확대
        yield return StartCoroutine(ScaleTo(originalScale * scaleMultiplier, animationDuration / 2));
        
        // 원래 크기로
        yield return StartCoroutine(ScaleTo(originalScale, animationDuration / 2));
    }
    
    IEnumerator ScaleTo(Vector3 targetScale, float duration)
    {
        Vector3 startScale = transform.localScale;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            progress = Mathf.SmoothStep(0f, 1f, progress);
            
            transform.localScale = Vector3.Lerp(startScale, targetScale, progress);
            yield return null;
        }
        
        transform.localScale = targetScale;
    }
}