using Opc.Ua;
using Opc.Ua.Export;
using Opc.Ua.Server;

namespace BeltLightSensor;

public class NodeManager : CustomNodeManager2

{
    private readonly Datastore? _datastore;
    private readonly string _nodeSetFileName;

    public NodeManager(IServerInternal server, ApplicationConfiguration configuration, Datastore? datastore,
        string nodeSetFileName)
        : base(server, configuration)
    {
        SetNamespaces(Constants.Namespaces.BeltLightSensor);
        _datastore = datastore;
        _nodeSetFileName = nodeSetFileName;
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

    #region Address Space

    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        lock (Server.DiagnosticsLock)
        {
            var capabilities = Server.DiagnosticsNodeManager.GetDefaultHistoryCapabilities();
            capabilities.AccessHistoryDataCapability.Value = true;
            capabilities.MaxReturnDataValues.Value = 1000;
        }

        lock (Lock)
        {
            base.CreateAddressSpace(externalReferences);

            try
            {
                Console.WriteLine("Importing nodes...");
                ImportNodesFromXml(externalReferences, _nodeSetFileName);
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

    #endregion

    #region Services

    public override void Write(OperationContext context, IList<WriteValue> nodesToWrite, IList<ServiceResult> errors)
    {
        Console.WriteLine("Write");
        base.Write(context, nodesToWrite, errors);

        const string nodeName = "ResultsInput";

        foreach (var writeValue in nodesToWrite)
        {
            var browseName = GetBrowseName(writeValue.NodeId);
            if (browseName != nodeName) continue;

            Console.WriteLine($"Write to {nodeName} is called");

            var sensorData = writeValue.FromStringToArray();
            if (sensorData == null) continue;

            Console.WriteLine($"Write to {nodeName} is stored in History");
            _datastore?.AddRecord(sensorData);
            _datastore?.Save();
        }
    }

    protected override void HistoryReadRawModified(ServerSystemContext context, ReadRawModifiedDetails details,
        TimestampsToReturn timestampsToReturn, IList<HistoryReadValueId> nodesToRead, IList<HistoryReadResult> results,
        IList<ServiceResult?> errors, List<NodeHandle> nodesToProcess,
        IDictionary<NodeId, NodeState> cache)
    {
        Console.WriteLine("HistoryReadRawModified");

        const string nodeName = "Results";

        for (var i = 0; i < nodesToRead.Count; i++)
        {
            var handle = nodesToProcess[i];
            var nodeToRead = nodesToRead[handle.Index];
            var result = results[handle.Index];

            var browseName = GetBrowseName(nodeToRead.NodeId);

            // NOTE: only "Results" node is processed
            if (browseName != nodeName) continue;

            // Removing the HistoryUnsupported error that was added by HistoryRead
            errors[i] = null;

            var dataStartTime = TimeZoneInfo.ConvertTimeFromUtc(details.StartTime, TimeZoneInfo.Local);
            var dataEndTime = TimeZoneInfo.ConvertTimeFromUtc(details.EndTime, TimeZoneInfo.Local);
            var valuesPerNode = (int)details.NumValuesPerNode;
            var offset = 0;

            try
            {
                Console.WriteLine($"HistoryReadRaw of Node: {nodeToRead.NodeId}");

                // Read an existing continuation point if provided
                var continuationPoint = LoadContinuationPoint(context, nodeToRead.ContinuationPoint);
                if (nodeToRead.ContinuationPoint != null)
                    if (continuationPoint == null)
                    {
                        errors[i] = StatusCodes.BadContinuationPointInvalid;
                        continue;
                    }

                dataStartTime = continuationPoint?.StartTime ?? dataStartTime;
                dataEndTime = continuationPoint?.EndTime ?? dataEndTime;
                valuesPerNode = continuationPoint?.ValuesPerNode ?? valuesPerNode;
                offset = continuationPoint?.Offset ?? offset;

                // Get historical values
                var data = _datastore?
                               .GetRecordsBetweenTimestamps(dataStartTime, dataEndTime, offset, valuesPerNode)
                               .AsHistoryData()
                           ?? new HistoryData();

                Console.WriteLine($"Returning {data.DataValues.Count} historical items.\n" +
                                  $"\tStart: {dataStartTime}\n" +
                                  $"\tEnd: {dataEndTime}\n" +
                                  $"\tOffset: {offset}\n" +
                                  $"\tValuesPerNode: {valuesPerNode}\n");

                // Set continuation point if needed
                if (data.DataValues.Count == valuesPerNode)
                {
                    var point = new ResultsContinuationPoint
                    {
                        Offset = offset + data.DataValues.Count,
                        StartTime = dataStartTime,
                        EndTime = dataEndTime,
                        ValuesPerNode = valuesPerNode
                    };

                    result.ContinuationPoint = SaveContinuationPoint(context, point);
                }

                // Write results
                result.HistoryData = new ExtensionObject(data);
            }
            catch (Exception e)
            {
                errors[handle.Index] = ServiceResult.Create(e, StatusCodes.BadUnexpectedError,
                    "Unexpected error reading history.");
            }
        }
    }

    #endregion

    #region Continuation Point

    private static ResultsContinuationPoint? LoadContinuationPoint(ServerSystemContext context,
        byte[] continuationPoint)
    {
        var session = context.OperationContext.Session;
        return session?.RestoreHistoryContinuationPoint(continuationPoint) as ResultsContinuationPoint;
    }

    private static byte[]? SaveContinuationPoint(
        ServerSystemContext context,
        ResultsContinuationPoint continuationPoint)
    {
        var session = context.OperationContext.Session;

        if (session == null) return null;

        var id = Guid.NewGuid();
        session.SaveHistoryContinuationPoint(id, continuationPoint);
        return id.ToByteArray();
    }

    private class ResultsContinuationPoint
    {
        public DateTime StartTime { get; init; }
        public DateTime EndTime { get; init; }
        public int Offset { get; init; }
        public int ValuesPerNode { get; init; }
    }

    #endregion

    #region Unsupported History Services For Debugging

    protected override void HistoryUpdate(ServerSystemContext context, Type detailsType,
        IList<HistoryUpdateDetails> nodesToUpdate, IList<HistoryUpdateResult> results, IList<ServiceResult> errors,
        List<NodeHandle> nodesToProcess, IDictionary<NodeId, NodeState> cache)
    {
        Console.WriteLine("HistoryUpdate not implemented");
        base.HistoryUpdate(context, detailsType, nodesToUpdate, results, errors, nodesToProcess, cache);
    }

    protected override void HistoryRead(ServerSystemContext context, HistoryReadDetails details,
        TimestampsToReturn timestampsToReturn, bool releaseContinuationPoints, IList<HistoryReadValueId> nodesToRead,
        IList<HistoryReadResult> results, IList<ServiceResult> errors, List<NodeHandle> nodesToProcess,
        IDictionary<NodeId, NodeState> cache)
    {
        Console.WriteLine("HistoryRead");
        base.HistoryRead(context, details, timestampsToReturn, releaseContinuationPoints, nodesToRead, results, errors,
            nodesToProcess, cache);
    }

    protected override void HistoryReadProcessed(ServerSystemContext context, ReadProcessedDetails details,
        TimestampsToReturn timestampsToReturn, IList<HistoryReadValueId> nodesToRead, IList<HistoryReadResult> results,
        IList<ServiceResult> errors, List<NodeHandle> nodesToProcess,
        IDictionary<NodeId, NodeState> cache)
    {
        Console.WriteLine("HistoryReadProcessed not implemented");
        base.HistoryReadProcessed(context, details, timestampsToReturn, nodesToRead, results, errors, nodesToProcess,
            cache);
    }

    protected override void HistoryReadEvents(ServerSystemContext context, ReadEventDetails details,
        TimestampsToReturn timestampsToReturn,
        IList<HistoryReadValueId> nodesToRead, IList<HistoryReadResult> results, IList<ServiceResult> errors,
        List<NodeHandle> nodesToProcess, IDictionary<NodeId, NodeState> cache)
    {
        Console.WriteLine("HistoryReadEvents not implemented");
        base.HistoryReadEvents(context, details, timestampsToReturn, nodesToRead, results, errors, nodesToProcess,
            cache);
    }

    protected override void HistoryReadAtTime(
        ServerSystemContext context,
        ReadAtTimeDetails details,
        TimestampsToReturn timestampsToReturn,
        IList<HistoryReadValueId> nodesToRead,
        IList<HistoryReadResult> results,
        IList<ServiceResult> errors,
        List<NodeHandle> nodesToProcess,
        IDictionary<NodeId, NodeState> cache)
    {
        Console.WriteLine("HistoryReadAtTime not implemented");
        base.HistoryReadAtTime(context, details, timestampsToReturn, nodesToRead, results, errors, nodesToProcess,
            cache);
    }

    #endregion

    #region Helpers

    public override NodeId New(ISystemContext context, NodeState node)
    {
        if (node is not BaseInstanceState instance || instance.Parent == null) return node.NodeId;

        if (instance.Parent.NodeId.Identifier is string id)
            return new NodeId(id + "_" + instance.SymbolicName, instance.Parent.NodeId.NamespaceIndex);

        return node.NodeId;
    }
    
    private string? GetBrowseName(NodeId writeValueNodeId)
    {
        var node = FindPredefinedNode(writeValueNodeId, typeof(BaseInstanceState));
        return node?.BrowseName.Name;
    }

    private static HistoryData RandomHistoryData()
    {
        var data = new HistoryData();

        for (var i = 0; i < 10; i++)
        {
            var timestamp = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(Random.Shared.Next(0, 30)));
            var value = new DataValue(
                new Variant(Random.Shared.NextSingle()),
                StatusCodes.Good,
                timestamp,
                timestamp);

            data.DataValues.Add(value);
        }

        return data;
    }

    #endregion
}