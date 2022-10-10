namespace BeltLightSensor;

public static class Constants
{
    public static class Namespaces
    {
        public const string OpcUa = "http://opcfoundation.org/UA/";
        public const string OpcUaTypes = "http://opcfoundation.org/UA/2008/02/Types.xsd";
        public const string OpcUaNodeSet = "http://opcfoundation.org/UA/2011/03/UANodeSet.xsd";
        public const string BeltLightSensor = "https://big-map.eu/WP4/BeltLightSensor/";
    }

    public static class Folders
    {
        public const uint LLE = 5003;
    }

    public static class Objects
    {
        public const uint BeltLightSensor = 5006;
        public const uint BeltLightSensorOutput = 5012;
        public const uint BeltLightSensorSettings = 5009;
    }

    public static class Variables
    {
        public const uint BeltLightSensorData = 6003;
        public const uint BeltLightSensorSettingsDeltaLEDs = 6009;
        public const uint BeltLightSensorSettingsNumOfLEDs = 6006;
        public const uint BeltLightSensorSettingsTravelDistance = 6012;
    }

    public static class Database
    {
        public const string Path = "datastore.sqlite";
        public const string Table = "light_sensor";
    }
}