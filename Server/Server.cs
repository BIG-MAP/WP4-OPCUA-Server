using Opc.Ua;
using Opc.Ua.Server;

namespace BeltLightSensor;

public class Server : StandardServer
{
    protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server,
        ApplicationConfiguration configuration)
    {
        var nodeManagers = new List<INodeManager>
        {
            new NodeManager(server, configuration)
        };

        return new MasterNodeManager(server, configuration, null, nodeManagers.ToArray());
    }

    protected override void OnServerStarted(IServerInternal server)
    {
        base.OnServerStarted(server);

        var endpoints = GetEndpoints()
            .Select(e => e.EndpointUrl)
            .Distinct();

        foreach (var endpoint in endpoints) Console.WriteLine("Endpoint: {0}", endpoint);
    }
}