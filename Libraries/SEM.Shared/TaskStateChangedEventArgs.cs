
namespace SEM
{
    public enum StateType
    {
        Request = 0,
        Start = 1,
        DSP = 2,
        Clickthrough =3,
        Success = 4,
        Complete = 5,
        Error = 6,
        Failure = 7,

        X5Sec = 8,
        HomepageTrigger = 9,
    }
    public static class StateTypeExtensions
    {
        public static string FullName(this StateType type) => type switch
        {
            StateType.Request => "request",
            StateType.Start => "start",
            StateType.DSP => "dsp",
            StateType.Clickthrough => "click",
            StateType.Success => "success",
            StateType.Complete => "complete",
            StateType.Error => "error",
            StateType.Failure => "failure",
            StateType.X5Sec => "x5sec",
            StateType.HomepageTrigger => "homepage_trigger",
            _ => "unknown"
        };
    }
    public class TaskStateChangedEventArgs : EventArgs
    {
        public StateType Type { get; set; }
        public int Id { get; }
        public int Count { get; }
        public string? Data { get; set; }
        public TaskStateChangedEventArgs(StateType type,int id, int count,string? data = null)
        {
            Type = type;
            Id = id;
            Count = count;
            Data = data;
        }
    }
}
