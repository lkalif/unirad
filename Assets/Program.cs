using UnityEngine;
using System;
using System.Collections;
using OM = OpenMetaverse;
using System.IO;

public class Program : MonoBehaviour {

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

	void Start () {
        client = new OM.GridClient();
        InitializeLoggingAndConfig();
        InitializeClient(client);
        var login = GetComponent<LoginScreen>();
        login.Visible = true;
	}
	
	// Update is called once per frame
	void Update () {
	
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


    void Network_LoginProgress(object sender, OM.LoginProgressEventArgs e)
    {
    }

}
