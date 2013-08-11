﻿using UnityEngine;
using System.Collections;
using OM = OpenMetaverse;

public class LoginScreen : MonoBehaviour
{

    public bool Visible;

    Texture2D splash;
    float panelHeightTarget = 150f;
    float animDampenig = 8f;
    float panelHeight = 0f;
    bool opening = true;
    bool closing;

    string username = string.Empty;
    string password = string.Empty;
    bool rememberPassword;
    bool logingIn;

    void Start()
    {
        splash = (Texture2D)Resources.Load("radegast-main screen2");
    }

    void Update()
    {
        if (opening)
        {
            panelHeight = Mathf.Lerp(panelHeight, panelHeightTarget, animDampenig * Time.deltaTime);
            if (panelHeightTarget - panelHeight < 0.5f)
            {
                opening = false;
                panelHeight = panelHeightTarget;
            }
        }

        if (closing)
        {
            panelHeight = Mathf.Lerp(panelHeight, 0f, animDampenig * Time.deltaTime);
            if (panelHeight < 0.5f)
            {
                closing = false;
                panelHeight = 0f;
                opening = true;
            }
        }
    }

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
                        logingIn = false;
                    }
                }
                else
                {
                    if (GUILayout.Button("Login"))
                    {
                        logingIn = true;
                        closing = true;
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
                rememberPassword = GUILayout.Toggle(rememberPassword, "Remember password");
                GUILayout.Label("Login Location");
                GUILayout.Button("My Last Location");
            }
            GUILayout.EndVertical();
        }
        GUILayout.EndArea();

    }
}