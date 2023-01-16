using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ambilight.Helpers
{
    // CODE BY UROS VIDOJEVIC // http://urosv.blogspot.com.br/ //
    public class LightSleeper
    {
        /// <summary> 
        /// Sleeps for the specified amount of time. 
        /// It can wake up earlier if Cancel() is called during the sleep. 
        /// Also, it returns immediately if Cancel() has already been called. 
        /// </summary> 
        /// <param name="millisecondsTimeout">Sleep interval in milliseconds.</param> 
        /// <returns>True if sleep wasn't canceled, false otherwise.</returns> 
        public bool Sleep(int millisecondsTimeout)
        {
            return !m_manualResetEvent.WaitOne(millisecondsTimeout);
        }

        /// <summary> 
        /// Cancels the current sleep operation (if there is one in progress), 
        /// and causes all future sleep operations to return immediately when called. 
        /// </summary> 
        public void Cancel()
        {
            // Only one thread calling Cancel() can actually set the event. 
            if (Interlocked.Exchange(ref m_canceled, Canceled) == NotCanceled)
            {
                m_manualResetEvent.Set();
            }
        }

        /// <summary> 
        /// Returns true if light sleeper has been canceled. 
        /// </summary> 
        public bool HasBeenCanceled
        {
            get
            {
                return (m_canceled == Canceled);
            }
        }

        private const int NotCanceled = 0;
        private const int Canceled = 1;

        private int m_canceled = NotCanceled;
        private ManualResetEvent m_manualResetEvent = new ManualResetEvent(false);
    }
}
