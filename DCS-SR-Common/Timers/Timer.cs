using System;
using System.Runtime.InteropServices;

namespace Cabhishek.Timers
{
    /// <summary>
    /// Source: https://github.com/cabhishek
    /// This timer class uses unmanaged DLL for better accuracy at short frequencies.
    /// This class is not thread safe.
    /// http://stackoverflow.com/questions/416522/c-sharp-why-are-timer-frequencies-extremely-off
    /// </summary>
    public class Timer : IDisposable, ITimer
    {
        private const string WINMM = "winmm.dll";
        private readonly MMTimerProc callbackFunction;
        protected readonly Action clientCallback;
        protected TimeSpan interval;
        private uint timerId;

        public Timer(Action clientCallback, TimeSpan interval)
        {
            callbackFunction = CallbackFunction;
            this.clientCallback = clientCallback;
            this.interval = interval;
        }

        public virtual void Dispose()
        {
            Stop();
        }

        public virtual void Start()
        {
            StartUnmanagedTimer();

            if (timerId == 0)
            {
                throw new Exception("TimeSet Event Error");
            }
        }

        public void Stop()
        {
            if (timerId == 0) return;
            StopUnmanagedTimer();
        }

        public void UpdateTimeInterval(TimeSpan interval)
        {
            Stop();
            this.interval = interval;
            Start();
        }

        private void StartUnmanagedTimer()
        {
            timerId = timeSetEvent((uint) interval.TotalMilliseconds, 0, callbackFunction, 0, 1);
        }

        private void StopUnmanagedTimer()
        {
            timeKillEvent(timerId);
            timerId = 0;
        }

        private void CallbackFunction(uint timerid, uint msg, IntPtr user, uint dw1, uint dw2)
        {
            clientCallback();
        }

        [DllImport(WINMM)]
        private static extern uint timeSetEvent(
            uint uDelay,
            uint uResolution,
            [MarshalAs(UnmanagedType.FunctionPtr)] MMTimerProc lpTimeProc,
            uint dwUser,
            int fuEvent
        );

        [DllImport(WINMM)]
        private static extern uint timeKillEvent(uint uTimerID);

        #region Nested type: MMTimerProc

        private delegate void MMTimerProc(uint timerid, uint msg, IntPtr user, uint dw1, uint dw2);

        #endregion
    }
}