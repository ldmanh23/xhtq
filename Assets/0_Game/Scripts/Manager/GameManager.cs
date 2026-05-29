using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { MainMenu, GamePlay, Finish}
public class GameManager : MonoBehaviour
{
    public static GameManager ins;
    private static GameState gameState = GameState.MainMenu;
    
    protected void Awake()
    {
        ins = this;
        DontDestroyOnLoad(gameObject);
        
        Input.multiTouchEnabled = false;
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // int maxScreenHeight = 1280;
        // float ratio = (float)Screen.currentResolution.width / (float)Screen.currentResolution.height;
        // if (Screen.currentResolution.height > maxScreenHeight)
        // {
        //     Screen.SetResolution(Mathf.RoundToInt(ratio * (float)maxScreenHeight), maxScreenHeight, true);
        // }

        ChangeState(GameState.MainMenu);
    }

    public static void ChangeState(GameState state)
    {
        gameState = state;
    }

    public static bool IsState(GameState state)
    {
        return gameState == state;
    }

    private void Start()
    {
        StartCoroutine(ie_LoadGame());
    }

    private IEnumerator ie_LoadGame()
    {
        yield return new WaitUntil(() =>
            LoadingScreen.ins != null
            && SoundManager.ins != null
        );

        //SoundManager.PlayMusicBg(SoundManager.ins.bgMusic);

        LoadingScreen.ins.SetPercent(0.35f, 1f);
        DataManager.ins.LoadData();
        //StartCoroutine(CountTime());
        yield return Cache.GetWFS(1f);

        LoadingScreen.ins.SetPercent(0.45f, 1f);
        yield return Cache.GetWFS(1f);

        LoadingScreen.ins.SetPercent(0.7f, 1f);
        yield return Cache.GetWFS(1f);

        yield return Cache.GetWFS(1f);

        LoadingScreen.ins.SetPercent(1f, 1f);

        var sync = SceneManager.LoadSceneAsync("Home");

        yield return new WaitUntil(() => sync.isDone);


        DataManager.ins.SaveData();

        
    }
}
