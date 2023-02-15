using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TwinklyWPF.Utilities;

namespace TwinklyWPF
{
    [Serializable]
    public class AppSettings
    {
        public WINDOWPLACEMENT MainWindowPlacement { get; set; }

        public bool AutoStart { get; set; } = true;
        
        public string SetModeOnExit { get; set; } = "off";

        public string ActiveDeviceName { get; set; }

        public Dictionary<string, object> KnownDevices { get; set; }

        public IEnumerable<string> RTMovieDevices { get; set; }

        #region Serialization

        public void Save(string filename)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filename));
            using (var fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                using (var sw = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented=true }))
                {
                    JsonSerializer.Serialize(sw, this);
                }
            }
        }

        public static AppSettings Read(string filename)
        {
            using (var sr = new StreamReader(filename))
            {
                var str = sr.ReadToEnd();
                return JsonSerializer.Deserialize<AppSettings>(str);
            }
        }

        #endregion

        // Returns a dictionary object to store and persist metadata about a device.
        // If the device is unknown, a new device node is added to KnownDevices.
        // If the device has no previously stored metadata, a node is added.
        // note that Settings.KnownDevices is Dictionary<string, object> where the value
        // can be EITHER a Dictionary<string, string> OR a JsonElement, as it's first read.
        // Invoking this function replaces the JsonElement with a Dictionary.
        public Dictionary<string, string> GetDeviceMetadata(string uniqueName)
        {
            var metadata = KnownDevices.GetValueOrDefault(uniqueName);

            if(metadata is Dictionary<string, string>)
            {
                return (Dictionary<string, string>)metadata;
            }

            if (metadata is JsonElement)
            {
                var convertedMetadata = JsonSerializer.Deserialize<Dictionary<string, string>>((JsonElement)metadata);
                KnownDevices[uniqueName] = convertedMetadata;
                return convertedMetadata;
            }
            
            // no stored user data for this device--create an empty object
            var emptyMetadata = new Dictionary<string, string>();
            KnownDevices[uniqueName] = emptyMetadata;
            return emptyMetadata;
        }


        public string GetDeviceMetadataEntry(string uniqueName, string key)
        {
            var metadata = GetDeviceMetadata(uniqueName);
            if (metadata.ContainsKey(key))
                return metadata[key];
            return null;
        }

    }
}
