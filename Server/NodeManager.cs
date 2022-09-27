using Opc.Ua;
using Opc.Ua.Export;
using Opc.Ua.Server;
using LocalizedText = Opc.Ua.LocalizedText;

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

            const string nodesSetPath = "BeltLightSensor.NodeSet2.xml";
            ImportNodesFromXml(externalReferences, nodesSetPath);
        }
    }

    private FolderState CreateFolder(NodeState? parent, string path, string name)
    {
        var folder = new FolderState(parent);

        folder.SymbolicName = name;
        folder.ReferenceTypeId = ReferenceTypes.Organizes;
        folder.TypeDefinitionId = ObjectTypeIds.FolderType;
        folder.NodeId = new NodeId(path, NamespaceIndex);
        folder.BrowseName = new QualifiedName(path, NamespaceIndex);
        folder.DisplayName = new LocalizedText("en", name);
        folder.WriteMask = AttributeWriteMask.None;
        folder.UserWriteMask = AttributeWriteMask.None;
        folder.EventNotifier = EventNotifiers.None;

        parent?.AddChild(folder);

        return folder;
    }

    private void ImportNodesFromXml(IDictionary<NodeId, IList<IReference>> externalReferences, string nodesSetPath)
    {
        // Code below is from ImportXml found at https://github.com/OPCFoundation/UA-.NETStandard/issues/546

        Stream stream = new FileStream(nodesSetPath, FileMode.Open);
        var nodeSet = UANodeSet.Read(stream);

        foreach (var namespaceUri in nodeSet.NamespaceUris)
            SystemContext.NamespaceUris.GetIndexOrAppend(namespaceUri);

        var predefinedNodes = new NodeStateCollection();
        nodeSet.Import(SystemContext, predefinedNodes);

        foreach (var node in predefinedNodes)
            AddPredefinedNode(SystemContext, node);

        AddReverseReferences(externalReferences);
    }
}