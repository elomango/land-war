using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SplashManager : MonoBehaviour
{
    [Header("로딩 설정")]
    public float loadingTime = 3f; // 로딩 시간 (초)
    
    void Start()
    {
        // 지정된 시간 후에 메인 씬으로 이동
        StartCoroutine(LoadMainScene());
        ApplySafeArea();
    }
    
    IEnumerator LoadMainScene()
    {
        // 로딩 시간만큼 대기
        yield return new WaitForSeconds(loadingTime);
        
        // 메인 씬으로 바로 전환
        SceneManager.LoadScene("MainScene"); // 또는 SceneManager.LoadScene(1);
    }

    void ApplySafeArea()
    {
        var safeArea = Screen.safeArea;
        var canvas = GetComponent<Canvas>();
    }
}