using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Isaac.FileStorage.Lib.CustomExceptions;
using Isaac.FileStorage.Lib.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Isaac.FileStorage.Lib;

public class FileStorageEngine
{
    public string DirectoryPath { get; }

    public FileStorageEngine(string dirPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(dirPath);

        var di = new DirectoryInfo(dirPath);
        if (!di.Exists) di.Create();
        DirectoryPath = di.FullName;
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

        var fileName = GetFileName(key);

        try
        {
            File.WriteAllBytes(fileName, Bson.Generate(obj));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot insert data content for key '{key}'. " +
                $"This happened because the object of type '{typeof(T)}' could not be serialised or the file could not be written. " +
                "Try verifying the object you are trying to store and try again.", ex);
        }
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

        var fileName = GetFileName(key);

        try
        {
            using var fs = File.OpenRead(fileName);
            using var reader = new BsonDataReader(fs);

            return new JsonSerializer().Deserialize<T>(reader);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot get data content from file of key '{key}'. " +
                "This happened because either the file is unreadable or the generic type mismatches. " +
                $"The current destination type is '{typeof(T)}' but I'm unable to determine the actual type. " +
                "Try verifying the type you are trying to recover data to and try again.", ex);
        }
    }

    /// <summary>
    /// Gets all keys matching file type on a given directory.
    /// </summary>
    /// <returns>An array containing all keys found.</returns>
    public IEnumerable<string> GetAllKeys()
    {
        return Directory.GetFiles(DirectoryPath, $"*{Constants.J2KFileExtension}")
            .Select(item => new FileInfo(item).Name[..^Constants.J2KFileExtension.Length]);
    }

    /// <summary>
    /// Removes an entry by key.
    /// </summary>
    /// <param name="key">The key to delete.</param>
    public void Delete(string key)
    {
        if (string.IsNullOrEmpty(key)) throw new EmptyKeyException();

        var fileName = GetFileName(key);

        if (!File.Exists(fileName)) throw new StorageKeyNotFoundException();

        try
        {
            File.Delete(fileName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot delete data content for key '{key}'. " +
                "This happened because the file could not be deleted, e.g. it may be locked or access may be denied. " +
                "Try verifying the file is not in use and try again.", ex);
        }
    }

    private string GetFileName(string key)
    {
        var fileName = Path.GetFullPath(Path.Combine(DirectoryPath, $"{key}{Constants.J2KFileExtension}"));
        var relativePath = Path.GetRelativePath(DirectoryPath, fileName);

        var escapesDirectory = relativePath == ".." ||
                                relativePath.StartsWith($"..{Path.DirectorySeparatorChar}") ||
                                Path.IsPathRooted(relativePath);

        if (escapesDirectory)
            throw new InvalidKeyException();

        return fileName;
    }
}
