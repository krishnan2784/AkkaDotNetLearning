using Akka.Actor;
using System;

namespace WinTail
{
    public class TailCoordinatorActor : UntypedActor
    {
        /// <summary>
        /// Start tailing the file at user-specified path.
        /// </summary>
        public class StartTail
        {
            public StartTail(string filePath, IActorRef reporteActor)
            {
                FilePath = filePath;
                ReporterActor = reporteActor;
            }

            public IActorRef ReporterActor { get; private set; }

            public string FilePath { get; private set; }
        }

        public class StopTail
        {
            public StopTail(string filePath)
            {
                FilePath = filePath;
            }

            public string FilePath { get; private set; }
        }

        protected override void OnReceive(object message)
        {
            if (!(message is StartTail)) return;
            var msg = message as StartTail;
            Context.ActorOf(Props.Create(() => new TailActor(msg.ReporterActor, msg.FilePath)));
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            return new OneForOneStrategy(10, TimeSpan.FromSeconds(30), x =>
            {
                if (x is ArithmeticException) return Directive.Resume;
                if (x is NotSupportedException) return Directive.Stop;
                return Directive.Restart;
            });
        }
    }
}