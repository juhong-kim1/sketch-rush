using Unity.VisualScripting;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource timerSource;
    [SerializeField] private AudioSource drawingSource;

    [Header("BGM")]
    [SerializeField] private AudioClip lobbyBGM;
    //[SerializeField] private AudioClip gameBGM;
    //[SerializeField] private AudioClip resultBGM;

    [Header("SFX")]
    [SerializeField] private AudioClip pencilButtonClick;
    [SerializeField] private AudioClip correctAnswer;
    [SerializeField] private AudioClip wrongAnswer;
    [SerializeField] private AudioClip timeOut;
    [SerializeField] private AudioClip tickTock;
    [SerializeField] private AudioClip drawing;
    [SerializeField] private AudioClip nextDrawing;
    [SerializeField] private AudioClip roomExit;
    [SerializeField] private AudioClip passwordWrong;

    [Header("Settings")]
    [SerializeField] private float bgmVolume = 0.5f;
    [SerializeField] private float sfxVolume = 0.7f;

    void Awake()
    {
        // �̱��� ����
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // AudioSource �ʱ�ȭ
        if (bgmSource != null)
        {
            bgmSource.loop = true;
            bgmSource.volume = bgmVolume;
        }

        if (sfxSource != null)
        {
            sfxSource.loop = false;
            sfxSource.volume = sfxVolume;
        }

        if (timerSource != null)
        {
            timerSource.loop = true;
        }

        if (drawingSource != null)
        {
            drawingSource.loop = true;
        }
    }

    // ===== BGM =====
    public void PlayLobbyBGM()
    {
        PlayBGM(lobbyBGM);
    }

    //public void PlayGameBGM()
    //{
    //    PlayBGM(gameBGM);
    //}

    //public void PlayResultBGM()
    //{
    //    PlayBGM(resultBGM);
    //}

    private void PlayBGM(AudioClip clip)
    {
        if (bgmSource == null || clip == null) return;

        if (bgmSource.clip == clip && bgmSource.isPlaying)
            return; // �̹� ��� ��

        bgmSource.clip = clip;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource != null)
            bgmSource.Stop();
    }

    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (bgmSource != null)
            bgmSource.volume = bgmVolume;
    }

    public void StartTickTock()
    {
        if (timerSource == null || tickTock == null) return;

        timerSource.clip = tickTock;
        timerSource.Play();
    }

    public void StopTickTock()
    {
        if (timerSource != null)
            timerSource.Stop();
    }

    public void StartDrawingSound()
    {
        if (drawingSource == null || drawing == null) return;

        if (!drawingSource.isPlaying)
        {
            drawingSource.clip = drawing;
            drawingSource.Play();
        }
    }

    public void StopDrawingSound()
    {
        if (drawingSource != null)
            drawingSource.Stop();
    }

    // ===== SFX =====
    public void PlayPencilButtonClick()
    {
        PlaySFX(pencilButtonClick);
    }

    public void PlayCorrectAnswer()
    {
        PlaySFX(correctAnswer);
    }

    public void PlayWrongAnswer()
    {
        PlaySFX(wrongAnswer);
    }

    public void PlayTimeOut()
    {
        PlaySFX(timeOut);
    }

    public void PlayCountdown()
    {
        PlaySFX(tickTock);
    }

    public void PlayDrawing()
    {
        PlaySFX(drawing);
    }

    public void PlayNextDrawingWord()
    {
        PlaySFX(nextDrawing);
    }

    public void PlayRoomExit()
    {
        PlaySFX(roomExit);
    }

    public void PlayPasswordWrong()
    {
        PlaySFX(passwordWrong);
    }

    private void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip);
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null)
            sfxSource.volume = sfxVolume;
    }
}