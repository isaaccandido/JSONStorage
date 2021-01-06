using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Isaac.FileStorage
{
    public class Core
    {
        public string DirectoryPath { get; }

        public Core(string Path)
        {
            if(Path == null) throw new ArgumentNullException(nameof(Path));

            var di = new DirectoryInfo(Path);

            if (!di.Exists) di.Create();

            DirectoryPath = di.FullName;
        }
        public void Insert<T>(string key, T obj) 
        {
            var serialized = JsonConvert.SerializeObject(obj);
            var filename = getFileName(key);

            File.WriteAllText(filename, serialized);
        }
        public T Get<T>(string key)
        {
            var file = getFileName(key);
            var json = File.ReadAllText(file);
            return JsonConvert.DeserializeObject<T>(json);
        }
        public IEnumerable<string> GetAllKeys()
        {
            return Directory.GetFiles(DirectoryPath, "*.jk")
                            .Select(o => new FileInfo(o).Name[..^3]);
        }
        private string getFileName(string key)
        {
            return Path.Combine(DirectoryPath, $"{key}.jk");
        }
    }
}
