namespace Chisato.Takina.OsuElement.Components.Events
{
    /// <summary>
    /// Video event in beatmap.
    /// </summary>
    public class VideoEvent : EventBase
    {
        internal VideoEvent()
        {
        }

        public int Offset { get; internal set; }
        public string Path { get; internal set; }
    }
}