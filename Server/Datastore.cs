using Newtonsoft.Json;
using Opc.Ua;

namespace BeltLightSensor;

public class Datastore
{
    public Datastore(string fileName = "datastore.db")
    {
        var folder = Environment.CurrentDirectory;
        DbPath = Path.Combine(folder, fileName);

        Records = new List<BeltLightResults>();

        if (File.Exists(DbPath))
            if (!Load())
                throw new Exception("Failed to load datastore");

        Console.WriteLine($"Datastore path: {DbPath}");
    }

    public List<BeltLightResults>? Records { get; private set; }

    public string DbPath { get; }

    public bool Save()
    {
        Console.WriteLine("Datastore: Saving");

        var json = JsonConvert.SerializeObject(Records);
        try
        {
            File.WriteAllText(DbPath, json);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error saving datastore: {e.Message}");
            return false;
        }

        return true;
    }

    public bool Load()
    {
        Console.WriteLine("Datastore: Loading");

        if (!File.Exists(DbPath)) return false;

        var json = File.ReadAllText(DbPath);
        Records = JsonConvert.DeserializeObject<List<BeltLightResults>>(json);

        return true;
    }

    public void AddRecord(BeltLightResults record)
    {
        Records?.Add(record);
    }

    public IEnumerable<BeltLightResults> GetRecordsBetweenTimestamps(DateTime start, DateTime end)
    {
        return Records?
            .Where(r => r.Timestamp >= start && r.Timestamp <= end)
            .ToList() ?? new List<BeltLightResults>();
    }

    public IEnumerable<BeltLightResults> GetRecordsBetweenTimestamps(DateTime start, DateTime end, int offset,
        int limit)
    {
        return Records?
            .Where(r => r.Timestamp >= start && r.Timestamp <= end)
            .Skip(offset)
            .Take(limit)
            .ToList() ?? new List<BeltLightResults>();
    }
}

public record BeltLightResults(float[][] Values)
{
    public DateTime Timestamp { get; } = DateTime.Now;
}

public static class CustomExtensions
{
    public static HistoryData AsHistoryData(this IEnumerable<BeltLightResults> records)
    {
        var data = new HistoryData();

        foreach (var item in records)
            data.DataValues.Add(new DataValue(
                new Variant(item.Values),
                StatusCodes.Good,
                item.Timestamp,
                item.Timestamp));

        return data;
    }

    public static BeltLightResults? AsBeltLightResults(this WriteValue item)
    {
        return item.Value.Value is float[][] values ? new BeltLightResults(values) : null;
    }

    public static BeltLightResults? FromStringToArray(this WriteValue item)
    {
        var str = item.Value.Value is string values ? values : null;
        if (str == null) return null;

        str = str.TrimEnd(';');

        var valuesArray = str.Split(';')
            .Select(s => s.Split(',')
                .Select(float.Parse)
                .ToArray())
            .ToArray();

        return new BeltLightResults(valuesArray);
    }
}