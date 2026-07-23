using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Isaac.FileStorage.Helpers;

public static class Bson
{
    public static byte[] Generate<T>(T? obj)
    {
        if (obj is null) return [];

        using var ms = new MemoryStream();
        using var writer = new BsonDataWriter(ms);
        var serializer = new JsonSerializer();

        serializer.Serialize(writer, obj);

        return ms.ToArray();
    }
}