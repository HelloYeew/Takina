using Chisato.Takina.Database;

string osuDatabasePath, collectionDatabasePath, presenceDatabasePath, scoreDatabasePath;

if (OperatingSystem.IsWindows())
{
    osuDatabasePath = @"C:\Users\gamep\AppData\Local\osu!\osu!.db";
    collectionDatabasePath = @"C:\Users\gamep\AppData\Local\osu!\collection.db";
    presenceDatabasePath = @"C:\Users\gamep\AppData\Local\osu!\presence.db";
    scoreDatabasePath = @"C:\Users\gamep\AppData\Local\osu!\scores.db";
}
else if (OperatingSystem.IsMacOS())
{
    osuDatabasePath = @"/Users/helloyeew/Downloads/osu!.db";
    collectionDatabasePath = @"/Users/helloyeew/Downloads/collection.db";
    presenceDatabasePath = @"/Users/helloyeew/Downloads/presence.db";
    scoreDatabasePath = @"/Users/helloyeew/Downloads/scores.db";
}
else
{
    throw new PlatformNotSupportedException("This platform is currently not supported.");
}

var osuDatabase = OsuDatabase.Read(osuDatabasePath);
var collectionDatabase = CollectionDatabase.Read(collectionDatabasePath);
var presenceDatabase = PresenceDatabase.Read(presenceDatabasePath);
var scoreDatabase = ScoreDatabase.Read(scoreDatabasePath);

Console.WriteLine($"OsuDatabase: {osuDatabasePath}");
Console.WriteLine($"CollectionDatabase: {collectionDatabasePath}");
Console.WriteLine($"PresenceDatabase: {presenceDatabasePath}");
Console.WriteLine($"ScoreDatabase: {scoreDatabasePath}");
    
Console.WriteLine(osuDatabase.AccountName);

for (int i = 0; i < osuDatabase.Beatmaps.Count; i++)
{
    var beatmap = osuDatabase.Beatmaps[i];
    Console.WriteLine($"{i + 1}. ({beatmap.BeatmapSetId}) {beatmap.BeatmapId} {beatmap.Artist} - {beatmap.Title} [{beatmap.Version}]");
}