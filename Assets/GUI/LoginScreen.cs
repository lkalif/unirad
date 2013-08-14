using UnityEngine;
using System;
using System.Collections;
using OM = OpenMetaverse;

public class LoginScreen : MonoBehaviour
{
    [System.NonSerialized]
    public bool Visible;
    Instance Instance;
    Texture2D splash;
    float panelHeightTarget = 150f;
    float animTime = 2f;
    float panelHeight = 0f;
    bool opening = true;
    bool closing;
    bool showStatus;

    [NonSerialized]
    public string username = "Tester Anton";
    [NonSerialized]
    public string password = "tester";
    [NonSerialized]
    public bool rememberPassword;
    [NonSerialized]
    public bool logingIn;

    bool failed;

    void Start()
    {
        Instance = (Instance)GetComponent<Instance>();
        splash = (Texture2D)Resources.Load("radegast-main screen2");
    }

    void Update()
    {
        if (!Visible) return;

        if (Instance.LoginStatus.Status == OM.LoginStatus.Failed)
        {
            closing = false;
            opening = true;
            logingIn = false;
            failed = true;
            showStatus = true;
        }

        if (opening)
        {
            panelHeight = Mathf.Lerp(panelHeight, panelHeightTarget, Time.deltaTime * 10 / animTime);
            if (panelHeightTarget - panelHeight < 0.5f)
            {
                opening = false;
                panelHeight = panelHeightTarget;
            }
        }

        if (closing)
        {
            panelHeight = Mathf.Lerp(panelHeight, 0f, Time.deltaTime * 10 / animTime);
            if (panelHeight < 0.5f)
            {
                closing = false;
                panelHeight = 0f;
            }
        }
    }

    int counter;

    void OnGUI()
    {
        if (!Visible) return;

        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), splash, ScaleMode.StretchToFill);
        Rect loginPanelRect = new Rect(0, Screen.height - panelHeight, Screen.width, Screen.height);
        GUIBase.DrawPanel(loginPanelRect);

        float columnWidth = Screen.width / 4;
        float columnPadding = 5;

        GUILayout.BeginArea(new Rect(columnWidth * 0 + columnPadding, Screen.height - panelHeight, columnWidth - columnPadding * 2, Screen.height));
        {
            GUILayout.BeginVertical();
            {
                GUILayout.Space(20f);
                GUILayout.Label("Unirad 1.0.0");
                GUILayout.Space(70f);

                if (logingIn)
                {
                    if (GUILayout.Button("Login in..."))
                    {
                        //logingIn = false;
                    }
                }
                else
                {
                    if (GUILayout.Button(failed ? "Retry" : "Login"))
                    {
                        Instance.BeginLogin();
                        logingIn = true;
                        closing = true;
                        showStatus = true;
                        counter++;
                    }
                }
            }
            GUILayout.EndVertical();
        }
        GUILayout.EndArea();

        GUILayout.BeginArea(new Rect(columnWidth * 2 + columnPadding, Screen.height - panelHeight, columnWidth - columnPadding * 2, Screen.height));
        {
            GUILayout.BeginVertical();
            {
                GUILayout.Space(10f);
                GUILayout.Label("Username");
                username = GUILayout.TextField(username);
                rememberPassword = GUILayout.Toggle(rememberPassword, "Remember password");
                GUILayout.Label("Grid");
                GUILayout.Button("Second Life (Agni)");
            }
            GUILayout.EndVertical();
        }
        GUILayout.EndArea();

        GUILayout.BeginArea(new Rect(columnWidth * 3 + columnPadding, Screen.height - panelHeight, columnWidth - columnPadding * 2, Screen.height));
        {
            GUILayout.BeginVertical();
            {
                GUILayout.Space(10f);
                GUILayout.Label("Password");
                password = GUILayout.PasswordField(password, '*');
                GUILayout.Space(22f);
                GUILayout.Label("Login Location");
                GUILayout.Button("My Last Location");
            }
            GUILayout.EndVertical();
        }
        GUILayout.EndArea();

        if (showStatus)
        {
            float statusWidth = 300f;
            float statusHeight = 100f;

            Rect statusRect = new Rect(
                (Screen.width - statusWidth) / 2f,
                (Screen.height - statusHeight) / 2f,
                statusWidth,
                statusHeight);

            GUIBase.DrawPanel(new Rect(statusRect.xMin - 5f, statusRect.yMin, statusWidth + 2 * 5f, statusHeight + 2 * 5f));
            GUILayout.BeginArea(statusRect);
            {
                var style = new GUIStyle();
                if (Instance.LoginStatus.Status == OM.LoginStatus.Failed)
                {
                    style.normal.textColor =  new Color(1f, .8f, .8f);
                }
                else
                {
                    style.normal.textColor = Color.white;
                    style.alignment = TextAnchor.MiddleCenter;
                    style.fontSize = 24;
                    style.fixedHeight = statusHeight;
                }

                GUILayout.Label(Instance.LoginStatus.Message, style);
            }
            GUILayout.EndArea();
        }

    }
}
