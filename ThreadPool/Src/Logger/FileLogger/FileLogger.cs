using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadPool.Src.Logger.FileLogger
{
    public class FileLogger : ILogger
    {
        private readonly string _filePath;

        public FileLogger(string fileName)
        {
            _filePath = fileName;
        }

        public void writeMessage(string message)
        {
            lock (this)
            {
                using (StreamWriter streamWriter = File.AppendText(_filePath))
                {
                    streamWriter.WriteLine(message);
                    streamWriter.Close();
                }
            }
        }

        public void writeException(Exception ex, string message)
        {
            lock (this)
            {
                using (StreamWriter streamWriter = File.AppendText(_filePath))
                {
                    streamWriter.WriteLine(message);
                    streamWriter.WriteLine("Exception: " + ex.Message);
                    streamWriter.WriteLine(ex.StackTrace);
                    streamWriter.WriteLine();
                    streamWriter.Close();
                }
            }
        }

        public bool CheckPath()
        {
            bool result;
            try
            {
                var stream = File.Create(_filePath);
                result = stream.CanWrite;
                stream.Dispose();
            }
            catch (Exception)
            {
                result = false;
            }

            return result;
        }
    }
}
