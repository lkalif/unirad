using UnityEngine;
using System;
using System.Collections;
using OM = OpenMetaverse;
using System.IO;

public class Instance : MonoBehaviour
{
    [System.NonSerialized]
    public static readonly string AppName = "Radunity";

    static Instance singleton = null;
    public static Instance Singleton()
    {
        if (singleton != null)
        {
            return singleton;
        }
        else
        {
            var inst = (GameObject)GameObject.Find("Instance");
            if (inst != null)
            {
                singleton = (Instance)inst.GetComponent<Instance>();
                return singleton;
            }

        }
        return null;
    }

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

    [NonSerialized]
    public OM.LoginProgressEventArgs LoginStatus = new OM.LoginProgressEventArgs(OM.LoginStatus.None, string.Empty, string.Empty);
    [NonSerialized]
    public GameObject CurrentSim;

    LoginScreen login = null;
    OM.LoginStatus lastLoginStatus = OM.LoginStatus.None;
    State currentState;
    SLCamera slCamera;

    void Start()
    {
        Logger.Init();
        Loom.Initialize(gameObject);

        client = new OM.GridClient();
        InitializeLoggingAndConfig();
        InitializeClient(client);

        currentState = State.Login;
        slCamera = Camera.main.GetComponent<SLCamera>();
    }

    void OnApplicationQuit()
    {
        if (client != null && client.Network.Connected)
        {
            Debug.Log("Application exiting, shutting down");
            client.Network.Logout();
        }
    }

    enum State
    {
        Login,
        LoggedIn,
        Running,
        Test
    }

    // Update is called once per frame
    void Update()
    {
        switch (currentState)
        {
            case State.Login:
                {
                    if (login == null)
                    {
                        login = gameObject.AddComponent<LoginScreen>();
                        login.Visible = true;
                    }

                    if (lastLoginStatus != LoginStatus.Status)
                    {
                        lastLoginStatus = this.LoginStatus.Status;
                        Debug.Log("Login status changed to: " + lastLoginStatus.ToString() + " " + LoginStatus.Message);
                    }

                    if (lastLoginStatus == OM.LoginStatus.Success)
                    {
                        currentState = State.LoggedIn;
                    }
                }
                break;

            case State.LoggedIn:
                {
                    Destroy(login);
                    currentState = State.Running;
                }
                break;

            case State.Running:
                {
                    client.Self.Movement.Camera.Far = 128f;
                    client.Self.Movement.Camera.LookAt(
                        client.Self.SimPosition + new OM.Vector3(-5, 0, 0) * client.Self.Movement.BodyRotation,
                        client.Self.SimPosition
                    );

                    client.Self.Movement.AtPos = Input.GetAxis("Vertical") > 0;
                    client.Self.Movement.AtNeg = Input.GetAxis("Vertical") < 0;
                    
                    float h = Input.GetAxis("Horizontal");
                    if (Input.GetKey(KeyCode.LeftShift))
                    {
                        client.Self.Movement.LeftPos = h < 0;
                        client.Self.Movement.LeftNeg = h > 0;
                    }
                    else
                    {
                        client.Self.Movement.TurnLeft = h < 0;
                        client.Self.Movement.TurnRight = h > 0;
                    }

                    if (h != 0)
                    {
                        client.Self.Movement.BodyRotation = client.Self.Movement.BodyRotation * OM.Quaternion.CreateFromAxisAngle(OM.Vector3.UnitZ, -h * Time.deltaTime);
                    }
                    //client.Self.Movement.AtPos = true;
                }
                break;

            case State.Test:
                {
                    Network_SimChanged(null, null);
                    currentState = State.Running;
                }
                break;
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
        client.Network.SimChanged += new EventHandler<OM.SimChangedEventArgs>(Network_SimChanged);
        client.Terrain.LandPatchReceived += new EventHandler<OM.LandPatchReceivedEventArgs>(Terrain_LandPatchReceived);
        client.Objects.ObjectUpdate += new EventHandler<OM.PrimEventArgs>(Objects_ObjectUpdate);
        client.Objects.TerseObjectUpdate += new EventHandler<OM.TerseObjectUpdateEventArgs>(Objects_TerseObjectUpdate);
    }

    void UnregisterClientEvents(OM.GridClient client)
    {
        client.Network.LoginProgress -= new System.EventHandler<OM.LoginProgressEventArgs>(Network_LoginProgress);
        client.Network.SimChanged -= new EventHandler<OM.SimChangedEventArgs>(Network_SimChanged);
        client.Terrain.LandPatchReceived -= new EventHandler<OM.LandPatchReceivedEventArgs>(Terrain_LandPatchReceived);
        client.Objects.ObjectUpdate -= new EventHandler<OM.PrimEventArgs>(Objects_ObjectUpdate);
        client.Objects.TerseObjectUpdate -= new EventHandler<OM.TerseObjectUpdateEventArgs>(Objects_TerseObjectUpdate);
    }

    void Objects_TerseObjectUpdate(object sender, OM.TerseObjectUpdateEventArgs e)
    {
        ProcessObjectUpdate(e.Simulator, e.Prim);
    }

    void Objects_ObjectUpdate(object sender, OM.PrimEventArgs e)
    {
        ProcessObjectUpdate(e.Simulator, e.Prim);
    }

    void ProcessObjectUpdate(OM.Simulator sim, OM.Primitive prim)
    {
        Loom.QueueOnMainThread(() =>
        {
            if (prim.LocalID == client.Self.LocalID)
            {
                OM.Vector3 origPos = client.Self.SimPosition + new OM.Vector3(-5, 0, 3) * client.Self.Movement.BodyRotation;
                Vector3 pos = new Vector3(origPos.X, origPos.Z, origPos.Y);
                slCamera.target.position = pos;
                slCamera.focalPoint = new Vector3(client.Self.SimPosition.X, client.Self.SimPosition.Z + 3, client.Self.SimPosition.Y);
                slCamera.target.LookAt(slCamera.focalPoint);
            }
        });
    }

    void Terrain_LandPatchReceived(object sender, OM.LandPatchReceivedEventArgs e)
    {
        Loom.QueueOnMainThread(() =>
        {
            if (CurrentSim == null) return;
            CurrentSim.GetComponent<Region>().Terrain.Modified = true;
        });
    }

    void Network_SimChanged(object sender, OM.SimChangedEventArgs e)
    {
        Loom.QueueOnMainThread(() =>
        {
            if (CurrentSim != null)
            {
                Destroy(CurrentSim);
            }
            CurrentSim = new GameObject("Current Sim");
            CurrentSim.transform.position = Vector3.zero;
            CurrentSim.transform.rotation = Quaternion.identity;
            var region = CurrentSim.AddComponent<Region>();
            region.Sim = Client.Network.CurrentSim;
        });
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
