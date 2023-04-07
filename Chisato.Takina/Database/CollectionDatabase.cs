using Chisato.Takina.OsuElement.Components.Beatmaps;
using Chisato.Takina.OsuElement.Serialization;

namespace Chisato.Takina.Database;

/// <summary>
/// Serializes and deserializes the collection.db file.
/// </summary>
public class CollectionDatabase : ISerializable
{
    public int OsuVersion;
    public readonly List<Collection> Collections = new();

    public static CollectionDatabase Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static CollectionDatabase Read(Stream stream)
    {
        var db = new CollectionDatabase();
        using var r = new SerializationReader(stream);
        db.ReadFromStream(r);
        return db;
    }

    public void ReadFromStream(SerializationReader r)
    {
        OsuVersion = r.ReadInt32();
        int amount = r.ReadInt32();

        for (int i = 0; i < amount; i++) {
            var c = new Collection();
            c.ReadFromStream(r);
            Collections.Add(c);
        }
    }

    public void WriteToStream(SerializationWriter w)
    {
        w.Write(OsuVersion);
        w.Write(Collections.Count);

        foreach (var collection in Collections)
        {
            collection.WriteToStream(w);
        }
    }
}