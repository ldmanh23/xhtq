using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CanvasMainMenu : UICanvas
{
    public override void Open()
    {
        base.Open();
    }
    
    public override void Close()
    {
        base.Close();
    }

    public void PlayBtn()
    {
        UIManager.ins.CloseUI(UIID.UICMainMenu);
        var sync = SceneManager.LoadSceneAsync("GamePlay");     
    }
        

}
