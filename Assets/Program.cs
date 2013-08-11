using UnityEngine;
using System;
using System.Collections;
using OM = OpenMetaverse;
using System.IO;

public class Program : MonoBehaviour
{
    [System.NonSerialized]
    public static readonly string AppName = "Radunity";

    OM.GridClient client;
    public OM.GridClient Client { get { return client; } }

    string userDir;
    /// <summary>
    /// System (not grid!) user's dir
    /// </summary>
    public string UserDir { get { return userDir; } }

    /// <summary>
    /// Grid client's user dir for settings and logs
    /// </summary>
    public string ClientDir
    {
        get
        {
            if (client != null && client.Self != null && !string.IsNullOrEmpty(client.Self.Name))
            {
                return Path.Combine(userDir, client.Self.Name);
            }
            else
            {
                return Environment.CurrentDirectory;
            }
        }
    }

    [System.NonSerialized]
    public OM.LoginProgressEventArgs LoginStatus = new OM.LoginProgressEventArgs(OM.LoginStatus.None, string.Empty, string.Empty);
    LoginScreen login;
    OM.LoginStatus lastLoginStatus = OM.LoginStatus.None;
    
    void Start()
    {
        Logger.Init();
        Loom.Initialize();

        client = new OM.GridClient();
        InitializeLoggingAndConfig();
        InitializeClient(client);
        login = (LoginScreen)GetComponent<LoginScreen>();
        login.Visible = true;
    }

    void OnApplicationQuit()
    {
        if (client != null && client.Network.Connected)
        {
            Debug.Log("Application exiting, shutting down");
            client.Network.Logout();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (lastLoginStatus != LoginStatus.Status)
        {
            lastLoginStatus = this.LoginStatus.Status;
            Debug.Log("Login status changed to: " + lastLoginStatus.ToString() + " " + LoginStatus.Message);
        }
    }

    public void BeginLogin()
    {
        LoginStatus = new OM.LoginProgressEventArgs(OM.LoginStatus.None, "Logging in...", "");
        string username = login.username;
        
        string[] parts = System.Text.RegularExpressions.Regex.Split(username.Trim(), @"[. ]+");
        LoginOptions LoginOptions = new LoginOptions();

        if (parts.Length == 2)
        {
            LoginOptions.FirstName = parts[0];
            LoginOptions.LastName = parts[1];
        }
        else
        {
            LoginOptions.FirstName = username.Trim();
            LoginOptions.LastName = "Resident";
        }

        LoginOptions.Password = login.password;
        LoginOptions.Channel = AppName;
        LoginOptions.Version = AppName + " 1.0.0";
        bool AgreeToTos = true;

        /*
        switch (cbxLocation.SelectedIndex)
        {
            case -1: //Custom
                netcom.LoginOptions.StartLocation = StartLocationType.Custom;
                netcom.LoginOptions.StartLocationCustom = cbxLocation.Text;
                break;

            case 0: //Home
                netcom.LoginOptions.StartLocation = StartLocationType.Home;
                break;

            case 1: //Last
                netcom.LoginOptions.StartLocation = StartLocationType.Last;
                break;
        }
        */

        LoginOptions.StartLocation = StartLocationType.Last;

        /*
        if (cbxGrid.SelectedIndex == cbxGrid.Items.Count - 1) // custom login uri
        {
            if (txtCustomLoginUri.TextLength == 0 || txtCustomLoginUri.Text.Trim().Length == 0)
            {
                MessageBox.Show("You must specify the Login Uri to connect to a custom grid.", Properties.Resources.ProgramName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            netcom.LoginOptions.Grid = new Grid("custom", "Custom", txtCustomLoginUri.Text);
            netcom.LoginOptions.GridCustomLoginUri = txtCustomLoginUri.Text;
        }
        else
        {
            netcom.LoginOptions.Grid = cbxGrid.SelectedItem as Grid;
        }
        */

        var gridManager = new GridManager();
        gridManager.LoadGrids();
        LoginOptions.Grid = gridManager.Grids[1]; // aditi for now

        if (LoginOptions.Grid.Platform != "SecondLife")
        {
            Client.Settings.MULTIPLE_SIMS = true;
        }

        /*
        instance.Client.Settings.HTTP_INVENTORY = !instance.GlobalSettings["disable_http_inventory"];
         */

        // netcom.Login();

        LoginOptions.LastExecEvent = OM.LastExecStatus.Normal;

        if (string.IsNullOrEmpty(LoginOptions.FirstName) ||
            string.IsNullOrEmpty(LoginOptions.LastName) ||
            string.IsNullOrEmpty(LoginOptions.Password))
        {
            LoginStatus = new OM.LoginProgressEventArgs(OM.LoginStatus.Failed, "One or more fields are blank.", string.Empty);
            return;
        }

        string startLocation = string.Empty;

        switch (LoginOptions.StartLocation)
        {
            case StartLocationType.Home: startLocation = "home"; break;
            case StartLocationType.Last: startLocation = "last"; break;

            case StartLocationType.Custom:
                startLocation = LoginOptions.StartLocationCustom.Trim();

                StartLocationParser parser = new StartLocationParser(startLocation);
                startLocation = Grid.StartLocation(parser.Sim, parser.X, parser.Y, parser.Z);

                break;
        }

        string password;

        if (LoginOptions.IsPasswordMD5(LoginOptions.Password))
        {
            password = LoginOptions.Password;
        }
        else
        {
            if (LoginOptions.Password.Length > 16)
            {
                password = OM.Utils.MD5(LoginOptions.Password.Substring(0, 16));
            }
            else
            {
                password = OM.Utils.MD5(LoginOptions.Password);
            }
        }

        OM.LoginParams loginParams = client.Network.DefaultLoginParams(
            LoginOptions.FirstName, LoginOptions.LastName, password,
            LoginOptions.Channel, LoginOptions.Version);

        var grid = LoginOptions.Grid;
        loginParams.Start = startLocation;
        loginParams.AgreeToTos = AgreeToTos;
        loginParams.URI = grid.LoginURI;
        loginParams.LastExecEvent = LoginOptions.LastExecEvent;
        client.Network.BeginLogin(loginParams);

    }

    void InitializeClient(OM.GridClient client)
    {
        client.Settings.MULTIPLE_SIMS = false;

        client.Settings.USE_INTERPOLATION_TIMER = false;
        client.Settings.ALWAYS_REQUEST_OBJECTS = true;
        client.Settings.ALWAYS_DECODE_OBJECTS = true;
        client.Settings.OBJECT_TRACKING = true;
        client.Settings.ENABLE_SIMSTATS = true;
        client.Settings.FETCH_MISSING_INVENTORY = true;
        client.Settings.SEND_AGENT_THROTTLE = true;
        client.Settings.SEND_AGENT_UPDATES = true;
        client.Settings.STORE_LAND_PATCHES = true;

        client.Settings.USE_ASSET_CACHE = true;
        client.Settings.ASSET_CACHE_DIR = Path.Combine(userDir, "cache");
        client.Assets.Cache.AutoPruneEnabled = false;

        client.Throttle.Total = 5000000f;
        client.Settings.THROTTLE_OUTGOING_PACKETS = false;
        client.Settings.LOGIN_TIMEOUT = 120 * 1000;
        client.Settings.SIMULATOR_TIMEOUT = 180 * 1000;
        client.Settings.MAX_CONCURRENT_TEXTURE_DOWNLOADS = 20;

        client.Self.Movement.AutoResetControls = false;
        client.Self.Movement.UpdateInterval = 250;

        RegisterClientEvents(client);
    }

    void RegisterClientEvents(OM.GridClient client)
    {
        client.Network.LoginProgress += new System.EventHandler<OM.LoginProgressEventArgs>(Network_LoginProgress);
    }

    void UnregisterClientEvents(OM.GridClient client)
    {
        client.Network.LoginProgress -= new System.EventHandler<OM.LoginProgressEventArgs>(Network_LoginProgress);
    }

    void Network_LoginProgress(object sender, OM.LoginProgressEventArgs e)
    {
        LoginStatus = e;
    }

    void InitializeLoggingAndConfig()
    {
        try
        {
            userDir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), AppName);
            Debug.Log(userDir);
            if (!Directory.Exists(userDir))
            {
                Directory.CreateDirectory(userDir);
            }
        }
        catch (Exception)
        {
            userDir = System.Environment.CurrentDirectory;
            Debug.Log(userDir);
        };
        /*
        globalLogFile = Path.Combine(userDir, Properties.Resources.ProgramName + ".log");
        globalSettings = new Settings(Path.Combine(userDir, "settings.xml"));
        frmSettings.InitSettigs(globalSettings, monoRuntime);
        */
    }
}
