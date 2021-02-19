using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppTime
{
    static class Utils
    {
        public static T CheckTimeout<T>(Func<T> act, Func<Thread, T> whenTimeout, int timeoutMs, bool abortThread = true)
        { 


            var isTimeout = true;
            

            Exception ex = null;
            var t = new Thread(() =>
            {
                try
                {
                    act();
                    isTimeout = false;
                }
                catch (Exception e)
                {
                    ex = e;
                }
            });
            t.Start();
            t.Join(timeoutMs);
            if (isTimeout)
            {
                if (abortThread)
                {
                    t.Abort();
                }
                return whenTimeout(t);
            }

            if (ex != null)
            {
                throw ex;
            }
            return default(T);

        }

        public static Exception Try(Action act)
        {
            try
            {
                act();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }
}
