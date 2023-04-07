using System.Text;
using Chisato.Takina;
using Chisato.Takina.Database;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

// TODO: Seperate some RabbitMQ value to .env file

var factory = new ConnectionFactory()
{
    HostName = "localhost",
    UserName = "user",
    Password = "password",
    Port = 5672
};
using var connection = factory.CreateConnection();
using var databaseProcessChannel = connection.CreateModel();

databaseProcessChannel.QueueDeclare(
    queue: "database-process-default",
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: null
);

Console.WriteLine("⏱️ Waiting for messages.");

var consumer = new EventingBasicConsumer(databaseProcessChannel);

List<string> messages = new List<string>();

consumer.Received += async (model, ea) =>
{
    try
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine("✉️ Received {0}", message);
        
        // Payload example (JSON):
        // {"user_id": 1, "file_type": "osu", "file_name": "osu_COJSuxolYcGMDdHz.db",
        // "file_url": "https://chisato-dev.sgp1.digitaloceanspaces.com/osu/1/osu_COJSuxolYcGMDdHz.db", "default_collection_id": 1
        // "default_collection_name": "All beatmaps"}
        var payload = JsonConvert.DeserializeObject<Dictionary<string, string>>(message);
        var userId = payload["user_id"];
        var fileType = payload["file_type"];
        var fileName = payload["file_name"];
        var fileUrl = payload["file_url"];
        var defaultCollectionId = payload["default_collection_id"];
        var defaultCollectionName = payload["default_collection_name"];
        
        // Download file (don't use WebClient)
        HttpClient client = new HttpClient();
        var response = await client.GetAsync(fileUrl);
        using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await response.Content.CopyToAsync(fileStream);
        }
        // get file path
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
        Console.WriteLine($"File path: {filePath}");
        switch (fileType)
        {
            case "osu":
                OsuDatabase osuDatabase = OsuDatabase.Read(filePath);
                for (int i = 0; i < osuDatabase.Beatmaps.Count; i++)
                {
                    var beatmap = osuDatabase.Beatmaps[i];
                    // Console.WriteLine($"{i + 1}. ({beatmap.BeatmapSetId}) {beatmap.BeatmapId} {beatmap.Artist} - {beatmap.Title} [{beatmap.Version}]");
                    ApiProcessMessage apiProcessMessage = new ApiProcessMessage
                    {
                        UserId = int.Parse(userId),
                        CollectionName = defaultCollectionName,
                        BeatmapSetId = beatmap.BeatmapSetId,
                        BeatmapId = beatmap.BeatmapId,
                        BeatmapChecksum = beatmap.BeatmapChecksum
                    };
                    messages.Add(JsonConvert.SerializeObject(apiProcessMessage));
                    // Console.WriteLine($"➕ Added {JsonConvert.SerializeObject(apiProcessMessage)} to queue");
                }
                break;
            
            case "collection":
                CollectionDatabase collectionDatabase = CollectionDatabase.Read(filePath);
                for (int i = 0; i < collectionDatabase.Collections.Count; i++)
                {
                    var collection = collectionDatabase.Collections[i];
                    // Console.WriteLine($"{i + 1}. {collection.Name}");
                    for (int j = 0; j < collection.BeatmapHashes.Count; j++)
                    {
                        ApiProcessMessage apiProcessMessage = new ApiProcessMessage
                        {
                            UserId = int.Parse(userId),
                            CollectionName = collection.Name,
                            BeatmapSetId = 0,
                            BeatmapId = 0,
                            BeatmapChecksum = collection.BeatmapHashes[j]
                        };
                        messages.Add(JsonConvert.SerializeObject(apiProcessMessage));
                        // Console.WriteLine($"➕ Added {JsonConvert.SerializeObject(apiProcessMessage)} to queue");
                    }
                }
                break;
            
            default:
                Console.WriteLine("❌ Unknown file type");
                break;
        }
        
        Thread thread = new Thread(() => PublishMessage(messages));
        thread.Start();
        
        // Delete file
        File.Delete(filePath);
    }
    catch (Exception e)
    {
        Console.WriteLine("❌ Error during processing the message: " + e.Message);
    }
};

// Routinely check for messages every 5 seconds
while (true)
{
    databaseProcessChannel.BasicConsume(
        queue: "database-process-default",
        autoAck: true,
        consumer: consumer
    );
    Thread.Sleep(5000);
    Console.WriteLine("⏱️ Waiting for messages.");
}

void PublishMessage(List<string> messagesList)
{
    var newFactory = new ConnectionFactory()
    {
        HostName = "localhost",
        UserName = "user",
        Password = "password",
        Port = 5672
    };
    
    var newConnection = newFactory.CreateConnection();
    using var apiProcessChannel = newConnection.CreateModel();
    
    apiProcessChannel.ExchangeDeclare(
        exchange: "api-process",
        type: ExchangeType.Direct,
        durable: true,
        autoDelete: false,
        arguments: null
    );

    apiProcessChannel.QueueDeclare(
        queue: "api-process-default",
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: null
    );
    
    apiProcessChannel.QueueBind(
        exchange: "api-process",
        queue: "api-process-default",
        routingKey: "api-process-default"
    );
    
    // Track unrouteable messages
    apiProcessChannel.CallbackException += (sender, ea) =>
    {
        Console.WriteLine("❌ Unrouteable message: " + ea.Exception.Message);
    };
    
    // Send messages to API process queue
    for (int i = 0; i < messagesList.Count; i++)
    {
        var apiProcessMessageBytes = messagesList[i];
        try
        {
            apiProcessChannel.BasicPublish(
                exchange: "api-process",
                routingKey: "api-process-default",
                basicProperties: null,
                body: Encoding.UTF8.GetBytes(apiProcessMessageBytes)
            );
        }
        catch (Exception e)
        {
            Console.WriteLine("❌ Error during sending the message: " + e.Message);
        }
        Console.WriteLine($"🚀 Sent {apiProcessMessageBytes}");
    }
}