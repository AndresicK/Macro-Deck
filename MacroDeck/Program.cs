using SuchByte.MacroDeck.Logging;
using SuchByte.MacroDeck.Startup;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SuchByte.MacroDeck.Pipe;

namespace SuchByte.MacroDeck;

internal class Program
{

    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        
        // Register exception event handlers
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += ApplicationThreadException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

        var startParameters = StartParameters.ParseParameters(args);
        CheckRunningInstance(startParameters.IgnorePidCheck);

        Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddSingleton(startParameters);
            })
            .Build()
            .RunAsync();
        
        //MacroDeck.Start(startParameters);
    }
    
    private static void CheckRunningInstance(int ignoredPid)
    {
        
        if (!MutexHelper.IsRunning()) return;
        if (MacroDeckPipeClient.SendShowMainWindowMessage())
        {
            Environment.Exit(0);
            return;
        }

        var proc = Process.GetCurrentProcess();
        var processes = Process.GetProcessesByName(proc.ProcessName).Where(x => ignoredPid == 0 || x.Id != ignoredPid).ToArray();

        // Kill instance if no response
        foreach (var p in processes?.Where(x => x.Id != proc.Id) ?? Array.Empty<Process>())
        {
            try
            {
                p.Kill();
            }
            catch
            {
                // ignored
            }
        }
    }

    private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        MacroDeckLogger.Error(typeof(MacroDeck), "CurrentDomainOnUnhandledException: " + e.ExceptionObject);
    }

    private static void ApplicationThreadException(object sender, ThreadExceptionEventArgs e)
    {
        MacroDeckLogger.Error(typeof(MacroDeck), "ApplicationThreadException: " + e.Exception.Message + Environment.NewLine + e.Exception.StackTrace);
    }
}