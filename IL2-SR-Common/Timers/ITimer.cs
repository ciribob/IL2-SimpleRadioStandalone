using System;

namespace Cabhishek.Timers
{
    /// <summary>
    /// Source: https://github.com/cabhishek/Time
    /// </summary>
    public interface ITimer
    {
        void Start();
        void Stop();
        void UpdateTimeInterval(TimeSpan interval);
    }
}