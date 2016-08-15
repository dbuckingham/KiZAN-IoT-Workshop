using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;

namespace BigDataLab.WorkerRole
{
    public class AlertEventProcessor : IEventProcessor
    {
        Stopwatch checkpointStopWatch;
        PartitionContext partitionContext;

        public Task OpenAsync(PartitionContext context)
        {
            Trace.TraceInformation(string.Format("Initializing EventProcessor: Partition: '{0}', Offset: '{1}'", context.Lease.PartitionId, context.Lease.Offset));
            this.partitionContext = context;
            this.checkpointStopWatch = new Stopwatch();
            this.checkpointStopWatch.Start();
            return Task.FromResult<object>(null);
        }

        public async Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            Trace.TraceInformation(string.Format("EventProcessor Shuting Down.  Partition '{0}', Reason: '{1}'.", this.partitionContext.Lease.PartitionId, reason.ToString()));
            if (reason == CloseReason.Shutdown)
            {
                await context.CheckpointAsync();
            }
        }

        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {
            Trace.TraceInformation("\n");
            Trace.TraceInformation("........ProcessEventsAsync........");
            foreach (EventData eventData in messages)
            {
                try
                {
                    string jsonString = Encoding.UTF8.GetString(eventData.GetBytes());

                    Trace.TraceInformation(string.Format("Message received at '{0}'. Partition: '{1}'",
                        eventData.EnqueuedTimeUtc.ToLocalTime(), this.partitionContext.Lease.PartitionId));

                    Trace.TraceInformation(string.Format("-->Raw Data: '{0}'", jsonString));

                    AlertEvent newAlertEvent = this.DeserializeEventData(jsonString);

                    Trace.TraceInformation(string.Format("-->Serialized Data: '{0}', '{1}', '{2}'",
                        newAlertEvent.deviceId, newAlertEvent.alert, newAlertEvent.description));

                    // Issuing alarm to device.
                    var deviceAlarm = new
                    {
                        deviceId = newAlertEvent.deviceId,
                        alert = newAlertEvent.alert,
                        description = newAlertEvent.description
                    };
                    var messageString = JsonConvert.SerializeObject(deviceAlarm);

                    Trace.TraceInformation("Issuing alarm to device: '{0}', from sensor: '{1}'", newAlertEvent.deviceId, newAlertEvent.description);
                    Trace.TraceInformation("New Device Alarm: '{0}'", messageString);
                    await WorkerRole.iotHubServiceClient.SendAsync(newAlertEvent.deviceId, new Microsoft.Azure.Devices.Message(Encoding.UTF8.GetBytes(messageString)));
                }
                catch (Exception ex)
                {
                    Trace.TraceInformation("Error in ProssEventsAsync -- {0}\n", ex.Message);
                }
            }

            await context.CheckpointAsync();
        }

        private AlertEvent DeserializeEventData(string eventDataString)
        {
            return JsonConvert.DeserializeObject<AlertEvent>(eventDataString);
        }
    }
}