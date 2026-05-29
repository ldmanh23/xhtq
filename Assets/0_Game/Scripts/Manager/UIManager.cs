using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Numerics;

public enum UIID
{
    UICGamePlay = 0,
    UICBlockRaycast = 1,
    UICMainMenu = 2,
    UICSetting = 3,
    UICFail = 4,
    UICVictory = 5,
}


public class UIManager : MonoBehaviour
{
    public static UIManager ins;
    private Dictionary<UIID, UICanvas> UICanvas = new Dictionary<UIID, UICanvas>();
    public Transform CanvasParentTF;
    public UICanvas[] uICanvas;

    private void Awake()
    {
        ins = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        for (int i = 0; i < uICanvas.Length; i++)
        {
            UICanvas.Add(uICanvas[i].UI_ID, uICanvas[i]);
        }
    }

    #region Canvas

    public bool IsOpenedUI(UIID ID)
    {
        return UICanvas.ContainsKey(ID) && UICanvas[ID] != null && UICanvas[ID].gameObject.activeInHierarchy;
    }

    public UICanvas GetUI(UIID ID)
    {
        // if (!UICanvas.ContainsKey(ID) || UICanvas[ID] == null)
        // {
        //     UICanvas canvas = Instantiate(Resources.Load<UICanvas>("UI/" + ID.ToString()), CanvasParentTF);
        //     canvas.gameObject.SetActive(false);
        //     UICanvas[ID] = canvas;
        // }

        return UICanvas[ID];
    } 
    
    public T GetUI<T>(UIID ID) where T : UICanvas
    {
        return GetUI(ID) as T;
    }

    public UICanvas OpenUI(UIID ID)
    {
        UICanvas canvas = GetUI(ID);

        canvas.Setup();
        canvas.Open();

        return canvas;
    }  
    
    public T OpenUI<T>(UIID ID) where T : UICanvas
    {
        return OpenUI(ID) as T;
    }

    public bool IsOpened(UIID ID)
    {
        return UICanvas.ContainsKey(ID) && UICanvas[ID] != null;
    }

    #endregion

    #region Back Button

    private Dictionary<UICanvas, UnityAction> BackActionEvents = new Dictionary<UICanvas, UnityAction>();
    private List<UICanvas> backCanvas = new List<UICanvas>();
    UICanvas BackTopUI {
        get
        {
            UICanvas canvas = null;
            if (backCanvas.Count > 0)
            {
                canvas = backCanvas[backCanvas.Count - 1];
            }

            return canvas;
        }
    }


    private void LateUpdate()
    {
        if (Input.GetKey(KeyCode.Escape) && BackTopUI != null)
        {
            BackActionEvents[BackTopUI]?.Invoke();
        }
    }

    public void PushBackAction(UICanvas canvas, UnityAction action)
    {
        if (!BackActionEvents.ContainsKey(canvas))
        {
            BackActionEvents.Add(canvas, action);
        }
    }

    public void AddBackUI(UICanvas canvas)
    {
        if (!backCanvas.Contains(canvas))
        {
            backCanvas.Add(canvas);
        }
    }

    public void RemoveBackUI(UICanvas canvas)
    {
        backCanvas.Remove(canvas);
    }

    /// <summary>
    /// CLear backey when comeback index UI canvas
    /// </summary>
    public void ClearBackKey()
    {
        backCanvas.Clear();
    }

    #endregion

    public void CloseUI(UIID ID)
    {
        if (IsOpened(ID))
        {
            GetUI(ID).Close();
        }
    }

}
