namespace Second.Utils
{
    public interface ISimpleFileLogger
    {
        void Log(string message, bool withoutNewline = false, bool overwite = false);
    }
}