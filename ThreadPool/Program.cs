using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadPool
{
    class Program
    {
        static void Main(string[] args)
        {
            ThreadPool threadPool;

            // creating pool
            threadPool = new ThreadPool("log.txt", 5, 7);

            //adding tasks
            for (int i = 0; i < 10; i++)
                threadPool.AddUserTask(execute);


            Console.ReadKey();


        }

        private static void execute()
        {
            Console.WriteLine("Emulate activity");
        }
    }
}
