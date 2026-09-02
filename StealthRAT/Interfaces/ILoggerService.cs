namespace StealthRAT.Interfaces
{
    public interface ILoggerService
    {
        void LogInfo(string message);
        void LogError(string message, Exception? exception = null);
        void LogWarning(string message);
    }
}
