using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonClicking : MonoBehaviour
{
    private TouchEvents g_EventManager;

    private GameObject g_IconsPanel;
    private Transform g_ToolsPanel;
    private Transform g_HandsPanel;
    private Transform g_TextsPanel;

    private GameObject g_ButtonsContainer;
    private Transform g_LinesButton;
    private Transform g_PointsButton;

    public bool g_TrackHandsButtonClicked { get; set; }
    public bool g_InitCameraButtonClicked { get; set; }
    public bool g_LineButtonClicked { get; set; }
    public bool g_PointsButtonClicked { get; set; }
    public bool g_PanelButtonClicked { get; set; }
    public bool g_ShowUltrasoundButtonClicked { get; set; }

    private GameObject g_TempPressedObject;

    // Use this for initialization
    void Start()
    {
        assetLoading();
        assetInitialization();
    }

    void Update()
    {

    }

    public void onClickInstrumentsPanelButton()
    {
        if (g_EventManager.g_UserInterface.activeSelf)
        {
            g_ToolsPanel.gameObject.SetActive(true);
            g_HandsPanel.gameObject.SetActive(false);
            g_TextsPanel.gameObject.SetActive(false);
        }
    }

    public void onClickHandsPanelButton()
    {
        if (g_EventManager.g_UserInterface.activeSelf)
        {
            g_ToolsPanel.gameObject.SetActive(false);
            g_HandsPanel.gameObject.SetActive(true);
            g_TextsPanel.gameObject.SetActive(false);
        }
    }

    public void onClickTextsPanelButton()
    {
        if (g_EventManager.g_UserInterface.activeSelf)
        {
            g_ToolsPanel.gameObject.SetActive(false);
            g_HandsPanel.gameObject.SetActive(false);
            g_TextsPanel.gameObject.SetActive(true);
        }
    }

    public void onClickTrackHandsButton()
    {
        if (g_EventManager.g_UserInterface.activeSelf)
        {
            g_TrackHandsButtonClicked = !g_TrackHandsButtonClicked;

            changeButtonColor(g_TrackHandsButtonClicked, EventSystem.current.currentSelectedGameObject, false);
        }
    }

    public void onClickInitCameraButton()
    {
        if (g_EventManager.g_UserInterface.activeSelf)
        {
            g_InitCameraButtonClicked = true;
        }
    }

    public void onClickShowUltrasoundButton()
    {
        if (g_EventManager.g_UserInterface.activeSelf)
        {
            g_ShowUltrasoundButtonClicked = true;
        }
    }

    public void onClickLinesButton()
    {
        if (g_EventManager.g_UserInterface.activeSelf)
        {
            g_LineButtonClicked = !g_LineButtonClicked;
            changeButtonColor(g_LineButtonClicked, g_LinesButton.gameObject, false);

            if (g_PointsButtonClicked)
            {
                g_PointsButtonClicked = !g_PointsButtonClicked;
                changeButtonColor(g_PointsButtonClicked, g_PointsButton.gameObject, false);
            }

            if (g_PanelButtonClicked)
            {
                g_PanelButtonClicked = !g_PanelButtonClicked;
                changeButtonColor(g_PanelButtonClicked, g_TempPressedObject, true);
            }
        }
    }

    public void onClickPointsButton()
    {
        if (g_EventManager.g_UserInterface.activeSelf)
        {
            g_PointsButtonClicked = !g_PointsButtonClicked;
            changeButtonColor(g_PointsButtonClicked, g_PointsButton.gameObject, false);

            if (g_LineButtonClicked)
            {
                g_LineButtonClicked = !g_LineButtonClicked;
                changeButtonColor(g_LineButtonClicked, g_LinesButton.gameObject, false);
            }

            if (g_PanelButtonClicked)
            {
                g_PanelButtonClicked = !g_PanelButtonClicked;
                changeButtonColor(g_PanelButtonClicked, g_TempPressedObject, true);
            }
        }
    }

    public void onClickEraseButton()
    {
        if (g_EventManager.g_UserInterface.activeSelf)
        {
            g_EventManager.EraseSelected();
        }
    }

    public void onClickEraseAllButton()
    {
        if (g_EventManager.g_UserInterface.activeSelf)
        {
            g_EventManager.EraseAll();
        }
    }

    public void onClickExitButton()
    {
        if (g_EventManager.g_UserInterface.activeSelf)
        {
#if ENABLE_WINMD_SUPPORT
        Windows.ApplicationModel.Core.CoreApplication.Exit();
        //deleteTempFiles();
#else
            Application.Quit();
#endif
        }
    }

    public void onClickPanelOptionsButton()
    {
        if (g_EventManager.g_UserInterface.activeSelf)
        {
            g_PanelButtonClicked = !g_PanelButtonClicked;
            g_TempPressedObject = EventSystem.current.currentSelectedGameObject;
            changeButtonColor(g_PanelButtonClicked, g_TempPressedObject, true);
        }

        if (g_LineButtonClicked)
        {
            g_LineButtonClicked = !g_LineButtonClicked;
            changeButtonColor(g_LineButtonClicked, g_LinesButton.gameObject, false);
        }

        if (g_PointsButtonClicked)
        {
            g_PointsButtonClicked = !g_PointsButtonClicked;
            changeButtonColor(g_PointsButtonClicked, g_PointsButton.gameObject, false);
        }
    }

    public string UnselectPanelButton()
    {
        g_PanelButtonClicked = false;
        changeButtonColor(g_PanelButtonClicked, g_TempPressedObject, true);
        return g_TempPressedObject.GetComponent<Image>().sprite.name;

    }

    public void SetObjectForColorChange(bool p_flag, Transform p_selectedObject)
    {
        changeButtonColor(p_flag, p_selectedObject.gameObject, false);
    }

    public void ImageDeselected(GameObject p_selectedObject)
    {
        changeButtonColor(false, p_selectedObject, false);
    }

    private void assetLoading()
    {
        if (g_IconsPanel == null)
        {
            g_IconsPanel = GameObject.Find("Icons Panel");
            if (g_IconsPanel == null)
            {
                Debug.LogError("Could not load Icons Panel");
            }
        }

        if (g_ToolsPanel == null)
        {
            g_ToolsPanel = g_IconsPanel.transform.Find("Instruments Panel");
            if (g_ToolsPanel == null)
            {
                Debug.LogError("Could not load Instruments Panel");
            }
        }

        if (g_HandsPanel == null)
        {
            g_HandsPanel = g_IconsPanel.transform.Find("Hands Panel");
            if (g_HandsPanel == null)
            {
                Debug.LogError("Could not load Hands Panel");
            }
        }

        if (g_TextsPanel == null)
        {
            g_TextsPanel = g_IconsPanel.transform.Find("Texts Panel");
            if (g_TextsPanel == null)
            {
                Debug.LogError("Could not load Texts Panel");
            }
        }

        if (g_ButtonsContainer == null)
        {
            g_ButtonsContainer = GameObject.Find("Buttons Container");
            if (g_ButtonsContainer == null)
            {
                Debug.LogError("Could not load Buttons Container");
            }
        }

        if (g_LinesButton == null)
        {
            g_LinesButton = g_ButtonsContainer.transform.Find("Lines Button");
            if (g_LinesButton == null)
            {
                Debug.LogError("Could not load Lines Button");
            }
        }

        if (g_PointsButton == null)
        {
            g_PointsButton = g_ButtonsContainer.transform.Find("Points Button");
            if (g_PointsButton == null)
            {
                Debug.LogError("Could not load Points Button");
            }
        }
    }

    private void assetInitialization()
    {
        g_EventManager = this.GetComponent<TouchEvents>();

        g_IconsPanel.SetActive(true);
        g_ToolsPanel.gameObject.SetActive(true);
        g_HandsPanel.gameObject.SetActive(false);
        g_TextsPanel.gameObject.SetActive(false);

        g_TrackHandsButtonClicked = false;
        g_LineButtonClicked = false;
        g_PointsButtonClicked = false;
        g_PanelButtonClicked = false;

        g_TempPressedObject = null;
}

    private void changeButtonColor(bool p_flag, GameObject p_selectedObject, bool p_isPanel)
    {
        Color transparentWhite = new Color(Color.white.r, Color.white.g, Color.white.b, 0.6f);
        if (p_selectedObject.GetComponent<Button>() == null)
        {
            if (p_flag)
            {
                Color selected = new Color(0.37f, 0.92f, 0.97f, 0.6f);
                p_selectedObject.GetComponent<Image>().color = selected;

            }
            else
            {
                p_selectedObject.GetComponent<Image>().color = transparentWhite;
            }
        }

        else
        {
            ColorBlock objectCB = p_selectedObject.GetComponent<Button>().colors;

            if (p_flag)
            {
                Color selected = new Color(0.37f, 0.92f, 0.97f);
                p_selectedObject.GetComponent<Image>().color = objectCB.normalColor = selected;
                p_selectedObject.GetComponent<Button>().colors = objectCB;

            }
            else
            {
                Color unselected;
                if (p_isPanel)
                {
                    unselected = Color.white;
                }
                else
                {
                    unselected = new Color(0.78f, 0.78f, 0.78f);
                }

                p_selectedObject.GetComponent<Image>().color = objectCB.normalColor = unselected;
                p_selectedObject.GetComponent<Button>().colors = objectCB;
            }
        }
    }
}
