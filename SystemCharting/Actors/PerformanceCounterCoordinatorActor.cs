using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;

namespace ChartApp.Actors
{
    public class PerformanceCounterCoordinatorActor : ReceiveActor
    {
        private readonly Dictionary<CounterType, IActorRef> _counterActors;

        #region Messages types

        public class Watch
        {
            public CounterType Counter { get; private set; }

            public Watch(CounterType counter)
            {
                Counter = counter;
            }
        }

        /// <summary>
        /// Unsubscribe the <see cref="ChartingActor"/> to
        /// updates for <see cref="Counter"/>
        /// </summary>
        public class Unwatch
        {
            public Unwatch(CounterType counter)
            {
                Counter = counter;
            }

            public CounterType Counter { get; private set; }
        }

        #endregion Messages types

        /// <summary>
        /// Methods for generating new instances of all <see cref="PerformanceCounter"/>s
        /// we want to monitor
        /// </summary>
        private static readonly Dictionary<CounterType, Func<PerformanceCounter>>
            CounterGenerators = new Dictionary<CounterType, Func<PerformanceCounter>>()
        {
            { CounterType.Cpu, () => new PerformanceCounter("Processor", "% Processor Time", "_Total", true) },
            { CounterType.Memory, () => new PerformanceCounter("Memory", "% Committed Bytes In Use", true) },
            { CounterType.Disk, () => new PerformanceCounter("LogicalDisk", "% Disk Time", "_Total", true) }
        };

        /// <summary>
        /// Methods for creating new <see cref="Series"/> with distinct colours and names
        /// corresponding to each <see cref="PerformanceCounter"/>
        /// </summary>
        private static readonly Dictionary<CounterType, Func<Series>> CounterSeries = new Dictionary<CounterType, Func<Series>>()
        {
            {CounterType.Cpu, () => new Series(CounterType.Cpu.ToString()) {ChartType = SeriesChartType.SplineArea, Color = Color.DarkGreen} },
            {CounterType.Memory, () => new Series(CounterType.Memory.ToString()) { ChartType = SeriesChartType.FastLine, Color = Color.MediumBlue}},
            {CounterType.Memory, ()=> new Series(CounterType.Disk.ToString()) {ChartType = SeriesChartType.SplineArea, Color = Color.DarkRed} }
        };

        private IActorRef _chartingActor;

        public PerformanceCounterCoordinatorActor(IActorRef chartingActor) : this(chartingActor, new Dictionary<CounterType, IActorRef>())
        {
            _chartingActor = chartingActor;
        }

        public PerformanceCounterCoordinatorActor(IActorRef chartingActor, Dictionary<CounterType, IActorRef> counterActorsActorRefs)
        {
            _chartingActor = chartingActor;
            _counterActors = counterActorsActorRefs;

            Receive<Watch>(watch =>
                {
                    if (!_counterActors.ContainsKey(watch.Counter))
                    {
                        // create a child actor to monitor this counter if
                        // one doesn't exist already
                        var counterActor = Context.ActorOf(Props.Create(() =>
                            new PerformanceCounterActor(watch.Counter.ToString(),
                                    CounterGenerators[watch.Counter])));
                        _counterActors[watch.Counter] = counterActor;
                    }
                    _chartingActor.Tell(new ChartingActor.AddSeries(CounterSeries[watch.Counter]()));
                    _counterActors[watch.Counter].Tell(new SubscribeCounter(watch.Counter, _chartingActor));
                }
            );

            Receive<Unwatch>(unwatch =>
            {
                if (!_counterActors.ContainsKey(unwatch.Counter))
                {
                    // create a child actor to monitor this counter if
                    // one doesn't exist already
                    return;
                }

                // unsubscribe the ChartingActor from receiving any more updates
                _counterActors[unwatch.Counter].Tell(new UnsubscribeCounter(unwatch.Counter, _chartingActor));
                // remove this series from the ChartingActor
                _chartingActor.Tell(new ChartingActor.RemoveSeries(unwatch.Counter.ToString()));
            }
           );
        }
    }
}