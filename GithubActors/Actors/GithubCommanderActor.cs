using Akka.Actor;
using Akka.IO;
using Akka.Routing;
using System;
using System.Linq;

namespace GithubActors.Actors
{
    /// <summary>
    /// Top-level actor responsible for coordinating and launching repo-processing jobs
    /// </summary>
    public class GithubCommanderActor : ReceiveActor, IWithUnboundedStash
    {
        #region Message classes

        public IStash Stash { get; set; }
        private int pendingJobReplies;

        public class CanAcceptJob
        {
            public CanAcceptJob(RepoKey repo)
            {
                Repo = repo;
            }

            public RepoKey Repo { get; private set; }
        }

        public class AbleToAcceptJob
        {
            public AbleToAcceptJob(RepoKey repo)
            {
                Repo = repo;
            }

            public RepoKey Repo { get; private set; }
        }

        public class UnableToAcceptJob
        {
            public UnableToAcceptJob(RepoKey repo)
            {
                Repo = repo;
            }

            public RepoKey Repo { get; private set; }
        }

        #endregion Message classes

        private IActorRef _coordinator;
        private IActorRef _canAcceptJobSender;

        public GithubCommanderActor()
        {
            Ready();
        }

        private void Ready()
        {
            Receive<CanAcceptJob>(job =>
            {
                _coordinator.Tell(job);
                BecomeAsking();
            });
        }

        private void BecomeAsking()
        {
            _canAcceptJobSender = Sender;
            pendingJobReplies = 3;
            Become(Asking);
        }

        private void Asking()
        {
            Receive<CanAcceptJob>(job => Stash.Stash());
            Receive<UnableToAcceptJob>(job =>
            {
                pendingJobReplies--;
                if (pendingJobReplies != 0) return;
                _canAcceptJobSender.Tell(job);
                BecomeReady();
            });
            Receive<AbleToAcceptJob>(job =>
            {
                _canAcceptJobSender.Tell(job);
                Sender.Tell(new GithubCoordinatorActor.BeginJob(job.Repo));
                Context.ActorSelection(ActorPaths.MainFormActor.Path).Tell(new MainFormActor.LaunchRepoResultsWindow(job.Repo, Sender));
            });
        }

        private void BecomeReady()
        {
            Become(Ready);
            Stash.UnstashAll();
        }

        protected override void PreStart()
        {
            var c1 = Context.ActorOf(Props.Create(() => new GithubCoordinatorActor()), ActorPaths.GithubCoordinatorActor.Name + "1");
            var c2 = Context.ActorOf(Props.Create(() => new GithubCoordinatorActor()), ActorPaths.GithubCoordinatorActor.Name + "2");
            var c3 = Context.ActorOf(Props.Create(() => new GithubCoordinatorActor()), ActorPaths.GithubCoordinatorActor.Name + "3");
            // create a broadcast router who will ask all of them
            // if they're available for work
            _coordinator =
                Context.ActorOf(Props.Empty.WithRouter(
                    new BroadcastGroup(ActorPaths.GithubCoordinatorActor.Path + "1",
                    ActorPaths.GithubCoordinatorActor.Path + "2",
                    ActorPaths.GithubCoordinatorActor.Path + "3")));
            base.PreStart();
        }

        protected override void PreRestart(Exception reason, object message)
        {
            //kill off the old coordinator so we can recreate it from scratch
            _coordinator.Tell(PoisonPill.Instance);
            base.PreRestart(reason, message);
        }
    }
}