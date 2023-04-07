namespace Chisato.Takina.OsuElement.Components.Events
{
    /// <summary>
    /// Break event in .osu file.
    /// </summary>
    public class BreakEvent : EventBase
    {
        internal BreakEvent()
        {
        }

        public double StartTime { get; internal set; }
        public double EndTime { get; internal set; }

        public double BreakTime => EndTime - StartTime;
    }
}