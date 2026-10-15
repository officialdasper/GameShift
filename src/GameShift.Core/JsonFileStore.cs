using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameShift.Core;

public sealed class JsonFileStore<T> : IStore<T> where T : class
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    readonly string _path;

    public JsonFileStore(string path) => _path = path;

    public T? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(_path), Options);
        }
        catch (JsonException)
        {
            File.Move(_path, _path + ".broken", overwrite: true);
            return null;
        }
    }

    public void Save(T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, _path, overwrite: true);
    }

    public void Delete()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
