using Opc.Ua;
using Opc.Ua.Export;
using Opc.Ua.Server;

namespace BeltLightSensor;

public class NodeManager : CustomNodeManager2

{
    public NodeManager(IServerInternal server, ApplicationConfiguration configuration)
        : base(server, configuration)
    {
        SetNamespaces(Constants.Namespaces.BeltLightSensor);
    }

    public override NodeId New(ISystemContext context, NodeState node)
    {
        if (node is not BaseInstanceState instance || instance.Parent == null) return node.NodeId;

        if (instance.Parent.NodeId.Identifier is string id)
            return new NodeId(id + "_" + instance.SymbolicName, instance.Parent.NodeId.NamespaceIndex);

        return node.NodeId;
    }

    protected override NodeHandle? GetManagerHandle(ServerSystemContext context, NodeId nodeId,
        IDictionary<NodeId, NodeState> cache)
    {
        lock (Lock)
        {
            // quickly exclude nodes that are not in the namespace.
            if (!IsNodeIdInNamespace(nodeId)) return null;

            if (!PredefinedNodes.TryGetValue(nodeId, out var node)) return null;

            var handle = new NodeHandle
            {
                NodeId = nodeId,
                Node = node,
                Validated = true
            };

            return handle;
        }
    }

    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        lock (Lock)
        {
            base.CreateAddressSpace(externalReferences);

            try
            {
                Console.WriteLine("Importing nodes...");
                const string nodesSetPath = "BeltLightSensor.NodeSet2.xml";
                ImportNodesFromXml(externalReferences, nodesSetPath);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    private void ImportNodesFromXml(IDictionary<NodeId, IList<IReference>> externalReferences, string nodesSetPath)
    {
        // Code below is from ImportXml found at https://github.com/OPCFoundation/UA-.NETStandard/issues/546

        Console.WriteLine("Importing nodes from " + nodesSetPath);

        if (!File.Exists(nodesSetPath))
            throw new FileNotFoundException("The file " + nodesSetPath + " does not exist.");

        Stream stream = new FileStream(nodesSetPath, FileMode.Open);
        var nodeSet = UANodeSet.Read(stream);

        foreach (var namespaceUri in nodeSet.NamespaceUris)
            SystemContext.NamespaceUris.GetIndexOrAppend(namespaceUri);

        var predefinedNodes = new NodeStateCollection();
        nodeSet.Import(SystemContext, predefinedNodes);

        foreach (var node in predefinedNodes)
        {
            AddPredefinedNode(SystemContext, node);
            Console.WriteLine("\tImported node: " + node.BrowseName);
        }

        AddReverseReferences(externalReferences);
    }
}