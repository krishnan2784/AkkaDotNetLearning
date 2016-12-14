namespace WinTail
{
    public class StopTail
    {
        public StopTail(string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get; private set; }
    }
}