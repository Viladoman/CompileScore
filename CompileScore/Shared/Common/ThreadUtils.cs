using Microsoft.VisualStudio.Shell;
using System;

namespace CompileScore.Common
{
    static public class ThreadUtils
    {
        public static void Run(Func<System.Threading.Tasks.Task> asyncMethod)
        {
#if VS17 || VS16
            ThreadHelper.JoinableTaskFactory.Run(asyncMethod);
#else
            asyncMethod.Invoke();
#endif
        }

        public static void Fork(Func<System.Threading.Tasks.Task> asyncMethod) 
        {
#if VS17 || VS16
            _ = System.Threading.Tasks.Task.Run(asyncMethod);
#else
            //TODO ~ ramonv ~ The standalone app runs sync ( figure out how to make the await to main thread )
            asyncMethod.Invoke();
#endif
        }
    }
}
