namespace Bullfrog.Actor.Interfaces.Models
{
    public enum ScaleEventState
    {
        NotFound,
        Waiting,
        AlreadyStarted, // Executing?
        Completed,
    }
}
