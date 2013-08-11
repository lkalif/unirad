using UnityEngine;
using System.Collections;
using OM = OpenMetaverse;

public static class GUIBase
{
    static Texture2D panelBG = null;

    public static Texture2D PanelBG
    {
        get
        {
            if (panelBG != null)
            {
                return panelBG;
            }
            else
            {
                panelBG = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                panelBG.SetPixels32(new Color32[1] { new Color32(100, 100, 100, 200) });
                panelBG.Apply();

                return panelBG;
            }
        }
    }

    public static void DrawPanel(Rect rect)
    {
        GUI.DrawTexture(rect, PanelBG);
    }

}
