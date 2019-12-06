using Newtonsoft.Json;

namespace Bullfrog.Actors.ResourceScalers
{
    public abstract class ResourceScalerWithState<T> : ResourceScaler
        where T : class
    {
        public T State { get; set; }

        public override string SerializedState
        {
            get => JsonConvert.SerializeObject(State);
            set => State = value == null ? (default) : JsonConvert.DeserializeObject<T>(value);
        }
    }
}
