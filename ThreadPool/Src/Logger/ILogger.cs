using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadPool.Src.Logger
{
    interface ILogger
    {
        void writeMessage(string message);
        void writeException(Exception e, string message);
    }
}
