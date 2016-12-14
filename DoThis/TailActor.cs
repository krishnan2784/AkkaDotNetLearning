using Akka.Actor;
using System.IO;
using System.Text;

namespace WinTail
{
    public class TailActor : UntypedActor
    {
        private readonly IActorRef _reportActor;
        private readonly string _filePath;
        private readonly FileObserver _observer;
        private readonly FileStream _fileStream;
        private readonly StreamReader _fileStreamReader;

        public class FileError
        {
            public string FileName { get; set; }
            public string Reason { get; set; }

            public FileError(string fileNameOnly, string reason)
            {
                FileName = fileNameOnly;
                Reason = reason;
            }
        }

        public class InitialRead
        {
            public string FilePath { get; set; }
            public string Text { get; set; }

            public InitialRead(string filePath, string text)
            {
                FilePath = filePath;
                Text = text;
            }
        }

        public class FileWrite
        {
            public string Name { get; set; }

            public FileWrite(string name)
            {
                Name = name;
            }
        }

        public TailActor(IActorRef reportActor, string filePath)
        {
            _reportActor = reportActor;
            _filePath = filePath;

            _observer = new FileObserver(Self, Path.GetFullPath(_filePath));
            _observer.Start();

            _fileStream = new FileStream(Path.GetFullPath(_filePath), FileMode.Open, FileAccess.Read, share: FileShare.ReadWrite);

            _fileStreamReader = new StreamReader(_fileStream, Encoding.UTF8);
            var text = _fileStreamReader.ReadToEnd();
            Self.Tell(new InitialRead(_filePath, text));
        }

        protected override void OnReceive(object message)
        {
            if (message is FileWrite)
            {
                var text = _fileStreamReader.ReadToEnd();
                if (!string.IsNullOrEmpty(text))
                {
                    _reportActor.Tell(text);
                }
            }
            else if (message is FileError)
            {
                var fe = message as FileError;
                _reportActor.Tell(string.Format("Tail error: {0}", fe.Reason));
            }
            else if (message is InitialRead)
            {
                var ir = message as InitialRead;
                _reportActor.Tell(ir.Text);
            }
        }
    }
}