using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace TwinklyWPF
{
    [Serializable]
    public class AppSettings
    {
        public WindowPlacement.WINDOWPLACEMENT MainWindowPlacement { get; set; }

        public bool AutoStart { get; set; } = true;

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
    }
}
