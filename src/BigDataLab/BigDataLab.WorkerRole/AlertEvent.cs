using System.Runtime.Serialization;

namespace BigDataLab.WorkerRole
{
    [DataContract]
    public class AlertEvent
    {
        [DataMember]
        public string deviceId { get; set; }

        [DataMember]
        public int alert { get; set; }

        [DataMember]
        public string description { get; set; }
    }
}