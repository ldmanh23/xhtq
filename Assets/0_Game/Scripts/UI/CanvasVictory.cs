using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CanvasVictory : UICanvas
{
    public void HomeBtn()
    {
        UIManager.ins.CloseUI(UIID.UICVictory);
        var sync = SceneManager.LoadSceneAsync("Home");
    }
        
}
