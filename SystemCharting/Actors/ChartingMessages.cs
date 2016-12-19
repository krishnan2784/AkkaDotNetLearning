using Akka.Actor;

namespace ChartApp.Actors
{
    #region Reporting

    public class GatherMetrics
    {
    }

    /// <summary>
    /// Signal used to indicate that it's time to sample all counters
    /// </summary>
    public class ChartingMessagesGaterMetrics
    {
    }

    public class Metric
    {
        public string Series { get; private set; }
        public float CounterValue { get; private set; }

        public Metric(string series, float counterValue)
        {
            Series = series;
            CounterValue = counterValue;
        }
    }

    #endregion Reporting

    #region Performance Counter Management

    public enum CounterType
    {
        Cpu,
        Memory,
        Disk
    }

    public class SubscribeCounter
    {
        public CounterType Counter { get; set; }
        public IActorRef Subscriber { get; set; }

        public SubscribeCounter(CounterType counter, IActorRef subscriber)
        {
            Counter = counter;
            Subscriber = subscriber;
        }
    }

    public class UnsubscribeCounter
    {
        public CounterType Counter { get; set; }
        public IActorRef Subscriber { get; set; }

        public UnsubscribeCounter(CounterType counter, IActorRef subscriber)
        {
            Counter = counter;
            Subscriber = subscriber;
        }
    }

    #endregion Performance Counter Management
}