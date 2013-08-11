using OpenMetaverse;
using OpenMetaverse.StructuredData;

public enum StartLocationType
{
    Home,
    Last,
    Custom
};

public class LoginOptions
{
    private string firstName;
    private string lastName;
    private string password;
    private string version = string.Empty;
    private string channel = string.Empty;

    private StartLocationType startLocation = StartLocationType.Home;
    private string startLocationCustom = string.Empty;

    private Grid grid;
    private string gridCustomLoginUri = string.Empty;
    private LastExecStatus lastExecEvent = LastExecStatus.Normal;


    public LoginOptions()
    {

    }

    public static bool IsPasswordMD5(string pass)
    {
        return pass.Length == 35 && pass.StartsWith("$1$");
    }

    public string FirstName
    {
        get { return firstName; }
        set { firstName = value; }
    }

    public string LastName
    {
        get { return lastName; }
        set { lastName = value; }
    }

    public string FullName
    {
        get
        {
            if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
                return string.Empty;
            else
                return firstName + " " + lastName;
        }
    }

    public string Password
    {
        get { return password; }
        set { password = value; }
    }

    public StartLocationType StartLocation
    {
        get { return startLocation; }
        set { startLocation = value; }
    }

    public string StartLocationCustom
    {
        get { return startLocationCustom; }
        set { startLocationCustom = value; }
    }

    public string Channel
    {
        get { return channel; }
        set { channel = value; }
    }

    public string Version
    {
        get { return version; }
        set { version = value; }
    }

    public Grid Grid
    {
        get { return grid; }
        set { grid = value; }
    }

    public string GridCustomLoginUri
    {
        get { return gridCustomLoginUri; }
        set { gridCustomLoginUri = value; }
    }

    public LastExecStatus LastExecEvent
    {
        get { return lastExecEvent; }
        set { lastExecEvent = value; }
    }
}

public class SavedLogin
{
    public string Username;
    public string Password;
    public string GridID;
    public string CustomURI;
    public int StartLocationType;
    public string CustomStartLocation;

    public OSDMap ToOSD()
    {
        OSDMap ret = new OSDMap(4);
        ret["username"] = Username;
        ret["password"] = Password;
        ret["grid"] = GridID;
        ret["custom_url"] = CustomURI;
        ret["location_type"] = StartLocationType;
        ret["custom_location"] = CustomStartLocation;
        return ret;
    }

    public static SavedLogin FromOSD(OSD data)
    {
        if (!(data is OSDMap)) return null;
        OSDMap map = (OSDMap)data;
        SavedLogin ret = new SavedLogin();
        ret.Username = map["username"];
        ret.Password = map["password"];
        ret.GridID = map["grid"];
        ret.CustomURI = map["custom_url"];
        if (map.ContainsKey("location_type"))
        {
            ret.StartLocationType = map["location_type"];
        }
        else
        {
            ret.StartLocationType = 1;
        }
        ret.CustomStartLocation = map["custom_location"];
        return ret;
    }

    /*
    public override string ToString()
    {
        RadegastInstance instance = RadegastInstance.GlobalInstance;
        string gridName;
        if (GridID == "custom_login_uri")
        {
            gridName = "Custom Login URI";
        }
        else if (instance.GridManger.KeyExists(GridID))
        {
            gridName = instance.GridManger[GridID].Name;
        }
        else
        {
            gridName = GridID;
        }
        return string.Format("{0} -- {1}", Username, gridName);
    }
     */
}

public class StartLocationParser
{
    private string location;

    public StartLocationParser(string location)
    {
        // if (location == null) throw new Exception("Location cannot be null.");

        this.location = location;
    }

    private string GetSim(string location)
    {
        if (!location.Contains("/")) return location;

        string[] locSplit = location.Split('/');
        return locSplit[0];
    }

    private int GetX(string location)
    {
        if (!location.Contains("/")) return 128;

        string[] locSplit = location.Split('/');

        int returnResult;
        bool stringToInt = int.TryParse(locSplit[1], out returnResult);

        if (stringToInt)
            return returnResult;
        else
            return 128;
    }

    private int GetY(string location)
    {
        if (!location.Contains("/")) return 128;

        string[] locSplit = location.Split('/');

        if (locSplit.Length > 2)
        {
            int returnResult;
            bool stringToInt = int.TryParse(locSplit[2], out returnResult);

            if (stringToInt)
                return returnResult;
        }

        return 128;
    }

    private int GetZ(string location)
    {
        if (!location.Contains("/")) return 0;

        string[] locSplit = location.Split('/');

        if (locSplit.Length > 3)
        {
            int returnResult;
            bool stringToInt = int.TryParse(locSplit[3], out returnResult);

            if (stringToInt)
                return returnResult;
        }

        return 0;
    }

    public string Sim
    {
        get { return GetSim(location); }
    }

    public int X
    {
        get { return GetX(location); }
    }

    public int Y
    {
        get { return GetY(location); }
    }

    public int Z
    {
        get { return GetZ(location); }
    }
}
