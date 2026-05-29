using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class DataManager : MonoBehaviour
{
    public static DataManager ins;
    private void Awake()
    {
        ins = this;
        DontDestroyOnLoad(gameObject);
    }
    public bool isLoaded = false;
    public PlayerData dt;
    public const string PLAYER_DATA = "PLAYER_DATA";


    private void OnApplicationPause(bool pause) { SaveData(); }
    private void OnApplicationQuit() { SaveData(); }

    public void LoadData()
    {
        string d = PlayerPrefs.GetString(PLAYER_DATA, "");
        if (d != "")
        {
            dt = JsonUtility.FromJson<PlayerData>(d);
        }
        else
        {
            dt = new PlayerData();
        }

        //loadskin
        //load pet

        // sau khi hoàn thành tất cả các bước load data ở trên
        isLoaded = true;
        // FirebaseManager.Ins.OnSetUserProperty();  
    }

    public void SaveData()
    {
        if (!isLoaded) return;
        string json = JsonUtility.ToJson(dt);
        PlayerPrefs.SetString(PLAYER_DATA, json);
    }
}


[System.Serializable]
public class PlayerData
{
    [Header("--------- Game Setting ---------")]
    public bool isNew = true;
    public bool isMusic = true;
    public bool isSound = true;
    public bool isVibrate = true;
    public bool isNoAds = false;
    public int starRate = -1;


    [Header("--------- Game Params ---------")]
    public int gold = 0;
    public int cup = 0;
    public int level = 0;//Level hiện tại
    public int idSkin = 0; //Skin
    

    [Header("--------- Firebase ---------")]
    public string timeInstall;//Thời điểm cài game
    public int timeLastOpen;//Thời điểm cuối cùng mở game. Tính số ngày kể từ 1/1/1970
    public int timeInstallforFirebase; //Dùng trong hàm bắn Firebase UserProperty. Số ngày tính từ ngày 1/1/1970
    public int daysPlayed = 0;//Số ngày đã User có mở game lên
    public int sessionCount = 0;//Tống số session
    public int playTime = 0;//Tổng số lần nhấn play game
    public int playTime_Session = 0;//Số lần nhấn play game trong 1 session
    public int dieCount_levelCur = 0;//Số lần chết tại level hiện tại
    public int firstDayLevelPlayed = 0;  //Số level đã chơi ở ngày đầu tiên

    //--------- Others ---------
}