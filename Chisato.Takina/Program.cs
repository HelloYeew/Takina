using System.Text;
using Chisato.Takina;
using Chisato.Takina.Database;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Sentry;
using Serilog;
using Serilog.Sinks.Loki;

// Check that the appsettings.json file exists
if (!File.Exists("appsettings.json"))
{
    Console.WriteLine("❌ appsettings.json file not found, please put it in the " + Directory.GetCurrentDirectory() + " folder");
    Environment.Exit(1);
    return;
}

// load appsettings.json from root of source code, not from bin/Debug/net7.0
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

#region Sentry

if (Boolean.Parse(configuration["Sentry:Enabled"] ?? string.Empty))
{
    using (SentrySdk.Init(o =>
           {
               o.Dsn = configuration["Sentry:DSN"];
               // When configuring for the first time, to see what the SDK is doing:
               o.Debug = false;
               // Set traces_sample_rate to 1.0 to capture 100% of transactions for performance monitoring.
               // We recommend adjusting this value in production.
               o.TracesSampleRate = 1.0;
               // Enable Global Mode if running in a client app
               o.IsGlobalModeEnabled = true;
           }))
    {
        // App code goes here. Dispose the SDK before exiting to flush events.
        Console.WriteLine("✅ Sentry initialized");
    }
}

#endregion

#region Logger

if (Boolean.Parse(configuration["Logging:Loki:Enabled"] ?? string.Empty))
{
    var credentials = new NoAuthCredentials(configuration["Logging:Loki:Url"]);

    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.LokiHttp(credentials)
        .CreateLogger();
}
else
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .CreateLogger();
}

#endregion

Log.Information("✅ appsettings.json loaded");

var factory = new ConnectionFactory()
{
    HostName = configuration["RabbitMQ:HostName"],
    UserName = configuration["RabbitMQ:UserName"],
    Password = configuration["RabbitMQ:Password"],
    Port = int.Parse(configuration["RabbitMQ:Port"] ?? string.Empty)
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

Log.Information("⏱️ Waiting for messages");

var consumer = new EventingBasicConsumer(databaseProcessChannel);

List<string> messages = new List<string>();

consumer.Received += async (model, ea) =>
{
    try
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Log.Information($"✉️ Received {message}");
        
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
                Log.Error("❌ Unknown file type");
                break;
        }
        
        Thread thread = new Thread(() => PublishMessage(messages));
        thread.Start();
        
        // Delete file
        File.Delete(filePath);
    }
    catch (Exception e)
    {
        Log.Error("❌ Error during processing the message: " + e.Message);
        // Capture exception with sending e.Message to Sentry
        SentrySdk.CaptureException(e);
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
    Thread.Sleep(2000);
}

void PublishMessage(List<string> messagesList)
{
    var newFactory = new ConnectionFactory()
    {
        HostName = configuration["RabbitMQ:HostName"],
        UserName = configuration["RabbitMQ:UserName"],
        Password = configuration["RabbitMQ:Password"],
        Port = int.Parse(configuration["RabbitMQ:Port"] ?? string.Empty)
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
    
    apiProcessChannel.BasicQos(0, 1, false);
    
    // Track unrouteable messages
    apiProcessChannel.CallbackException += (sender, ea) =>
    {
        Log.Error("❌ Unrouteable message: " + ea.Exception.Message);
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
            Log.Error("❌ Error during sending the message: " + e.Message);
            SentrySdk.CaptureException(e);
        }
        Log.Information($"🚀 Sent {apiProcessMessageBytes}");
    }
}