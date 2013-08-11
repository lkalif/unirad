using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Config;
using log4net.Layout;
using UE = UnityEngine;

public class Logger : AppenderSkeleton
{
    public static void Init()
    {
        var appender = new Logger();
        appender.Layout = new PatternLayout("%timestamp [%thread] %-5level - %message%newline");
        BasicConfigurator.Configure(appender);
    }
   
    protected override void Append(LoggingEvent le)
    {
        if (le.Level == Level.Info || le.Level == Level.Debug)
        {
            UE.Debug.Log("LIBOMV: " + le.MessageObject.ToString());
        }
        else
        {
            UE.Debug.LogWarning("LIBOMV: " + le.MessageObject.ToString());
        }
    }
}
