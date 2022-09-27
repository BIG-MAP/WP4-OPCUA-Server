using Opc.Ua;
using Opc.Ua.Configuration;

namespace BeltLightSensor;

public static class Program
{
    public static async Task<int> Main()
    {
        var configuration = Configuration.Default();

        var passwordProvider = new CertificatePasswordProvider("");

        var application = new ApplicationInstance
        {
            CertificatePasswordProvider = passwordProvider,
            ApplicationConfiguration = configuration
        };

        var server = new Server();

        await application.Start(server).ConfigureAwait(false);

        var quitEvent = CtrlCHandler();
        quitEvent.WaitOne(-1);

        return 0;
    }

    private static ManualResetEvent CtrlCHandler()
    {
        var quitEvent = new ManualResetEvent(false);
        try
        {
            Console.CancelKeyPress += (_, eArgs) =>
            {
                quitEvent.Set();
                eArgs.Cancel = true;
            };
        }
        catch
        {
            // intentionally left blank
        }

        return quitEvent;
    }
}