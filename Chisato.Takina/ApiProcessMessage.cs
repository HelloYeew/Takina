namespace Chisato.Takina;

/// <summary>
/// Message structure for let the API process know via RabbitMQ.
/// </summary>
public class ApiProcessMessage
{
    public int UserId { get; set; }
    public string CollectionName { get; set; }
    public int BeatmapSetId { get; set; }
    public int BeatmapId { get; set; }
    public string BeatmapChecksum { get; set; }
}