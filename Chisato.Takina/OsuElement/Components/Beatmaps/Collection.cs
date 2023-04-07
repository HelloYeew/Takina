using Chisato.Takina.OsuElement.Serialization;

namespace Chisato.Takina.OsuElement.Components.Beatmaps;

/// <summary>
/// Collection of beatmaps in collection.db.
/// </summary>
public class Collection : ISerializable
{
    public string Name;
    public List<string> BeatmapHashes;
    
    public void ReadFromStream(SerializationReader r)
    {
        Name = r.ReadString();
        
        BeatmapHashes = new List<string>();
        int amount = r.ReadInt32();
        for (int j = 0; j < amount; j++)
            BeatmapHashes.Add(r.ReadString());
    }
    
    public void WriteToStream(SerializationWriter w)
    {
        w.Write(Name);
        w.Write(BeatmapHashes.Count);
        
        foreach (string beatmapHash in BeatmapHashes)
        {
            w.Write(beatmapHash);
        }
    }
}