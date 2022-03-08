using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Isaac.FileStorage
{
    public class FileStorageEngine
    {
        public string DirectoryPath { get; }

        const string J2KFileExtension = ".j2k";
        const string TempFileExtension = ".legacy";
        const string ZipName = "legacyFiles.zip";

        public FileStorageEngine(string dirPath)
        {
            bool zipLegacyFiles = true;

            if (dirPath == null) throw new ArgumentNullException(nameof(dirPath));

            var di = new DirectoryInfo(dirPath);

            if (!di.Exists) di.Create();

            DirectoryPath = di.FullName;

            // backwards compatibility addon
            jsonToBsonConverter();

            if (zipLegacyFiles) archiveLegacyFiles();
        }

        /// <summary>
        /// Inserts an entry and records it to a file.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <param name="key">The file key (will be used as file name).</param>
        /// <param name="obj">The instantiated class containing data.</param>
        public void Insert<T>(string key, T obj)
        {
            if (string.IsNullOrEmpty(key)) throw new EmptyKeyException();

            var bson = bsonGenerator(obj);
            File.WriteAllBytes(getFileName(key), bson);
        }

        /// <summary>
        /// Retrieves an entry from file.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <param name="key">The file key (the file name).</param>
        /// <returns>Returns a T object with deserialized data.</returns>
        public T Get<T>(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new EmptyKeyException();

            try
            {
                using FileStream fs = File.OpenRead(getFileName(key));
                using var reader = new BsonDataReader(fs);
                return new JsonSerializer().Deserialize<T>(reader);
            }
            catch
            {
                throw new
                    InvalidOperationException($"Cannot get data content from file of key '{key}'. " +
                    "This happened because either the file is unreadable or the generic type mismatches. " +
                    $"The current destination type is '{typeof(T)}' but I'm unable to determine the actual type. " +
                    "Try verifying the type you are trying to recover data to and try again.");
            }
        }

        /// <summary>
        /// Gets all keys matching file type on a given directory.
        /// </summary>
        /// <returns>An array containing all keys found.</returns>
        public IEnumerable<string> GetAllKeys()
        {
            return Directory.GetFiles(DirectoryPath, $"*{J2KFileExtension}")
                            .Select(item => new FileInfo(item).Name[..^J2KFileExtension.Length]);
        }

        /// <summary>
        /// Removes an entry by key.
        /// </summary>
        /// <param name="key">The key to delete.</param>
        public void Delete(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new EmptyKeyException();

            var fileName = Path.Combine(DirectoryPath, $"{key}.j2k");

            if (!File.Exists(fileName)) throw new KeyNotFoundException();

            File.Delete(fileName);
        }

        private string getFileName(string key)
        {
            return Path.Combine(DirectoryPath, $"{key}{J2KFileExtension}");
        }
        private byte[] bsonGenerator<T>(T obj)
        {
            // I think it's okay to write null file on disk.
            if (obj is null) return new byte[0];

            using var ms = new MemoryStream();
            using var writer = new BsonDataWriter(ms);
            var serializer = new JsonSerializer();
            serializer.Serialize(writer, obj);
            return ms.ToArray();
        }
        private void jsonToBsonConverter()
        {
            // I'm not sure I can delete stuff. I think I can;
            // will leave this here so I remember to test it all
            bool deleteOriginalFile = true;

            // jk (json) to j2k (bson) converter, for backwards compatibility
            foreach (var f in Directory.GetFiles(DirectoryPath))
            {
                try
                {
                    FileInfo jk = new FileInfo(f);
                    if (jk.Extension != ".jk") continue;

                    var jkFile = $"{Path.Combine(DirectoryPath, jk.Name)}";
                    var tmpFile = $"{Path.Combine(DirectoryPath, jk.Name)}{TempFileExtension}";
                    var j2kFile = $"{Path.Combine(DirectoryPath, jk.Name[..^3])}{J2KFileExtension}";

                    // Used to create a copy but now I just zip stuff up.
                    // File.Copy(jkFile, tmpFile, true);

                    var jkFileContents = File.ReadAllText(jkFile);
                    object jkObj = JsonConvert.DeserializeObject(jkFileContents);

                    var bson = bsonGenerator(jkObj);

                    File.WriteAllBytes(j2kFile, bson);
                    if (deleteOriginalFile)
                    {
                        File.Delete(jkFile);
                    }
                }
                // If it doesn't work, well, just roll with it. 
                // If I can't deserialise, I'll just not deserialise.
                // I cannot disallow people from using in case of corrupted files.
                catch { return; }
            }
        }
        private void archiveLegacyFiles()
        {
            // Instead of just spamming files into database, after JSON to BSON conversion,
            // I thought it'd be nice to zip old files.
            // Let's face it, people won't be using them anymore.
            // It's just safekeeping.
            // So, storing it in a zipped file, well, that seems reasonable.

            var zipName = Path.Combine(DirectoryPath, ZipName);
            var tmpDir = $"{Path.Combine(DirectoryPath, Guid.NewGuid().ToString())}";

            var allFiles = new DirectoryInfo(DirectoryPath).GetFiles();

            var jkFiles = allFiles.Where(o => o.Extension == ".jk")
                                  .ToArray();

            if (jkFiles.Length == 0) return;

            try { Directory.CreateDirectory(tmpDir); }
            catch { if (Directory.Exists(tmpDir)) return; }

            foreach (var f in jkFiles)
            {
                try { File.Move(Path.Combine(DirectoryPath, f.Name), Path.Combine(tmpDir, f.Name)); }
                catch { return; }
            }

            try
            {
                if (File.Exists(zipName)) File.Delete(zipName);
                ZipFile.CreateFromDirectory(tmpDir, zipName);
                Directory.Delete(tmpDir, true);
            }
            catch { return; }
        }
    }
}