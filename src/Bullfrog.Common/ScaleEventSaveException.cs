using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Bullfrog.Common
{
    [Serializable]
    [ExcludeFromCodeCoverage]
    public class ScaleEventSaveException : BullfrogException
    {
        public ScaleEventSaveException(string message, ScaleEventSaveFailureReason reason)
            : base(message)
        {
            Reason = reason;
        }

        protected ScaleEventSaveException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Reason = (ScaleEventSaveFailureReason)info.GetValue(nameof(Reason), typeof(ScaleEventSaveFailureReason));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(Reason), Reason);
            base.GetObjectData(info, context);
        }

        public ScaleEventSaveFailureReason Reason { get; }
    }
}
