namespace Chisato.Takina.OsuElement.Components.Events
{
    /// <summary>
    /// Background event
    /// </summary>
    public class BackgroundEvent : EventBase
    {
        internal BackgroundEvent()
        {
        }

        public string Path { get; internal set; }
    }
}