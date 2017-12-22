namespace ThreadPool
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Priority_Queue;
    using Src.Logger;
    using Src.Logger.FileLogger;

    public class ThreadPool : IDisposable
    {
        private readonly int _cleanupInterval;
        private readonly FastPriorityQueue<ProrityContainer> _userTaskQueue;
        private readonly List<Task> _taskList = new List<Task>();
        private readonly ILogger _logger;
        private bool _disposed = false;

        #region Lock Objects

        private readonly object _cleanupLock = new object();
        private readonly object _stopCreatingTasksLock = new object();

        #endregion

        private DateTime _lastTaskCleanup;
        private bool _stopCreatingTasks = false;

        #region Constructors and Destructors

        public ThreadPool(string logFilePath, int cleanupInterval = 200) : this(logFilePath, 1, 10, cleanupInterval)
        {

        }

        public ThreadPool(string logFilePath, int minThreads, int maxThreads, int cleanupInterval = 200)
        {
            if (logFilePath == null) throw new ArgumentNullException(nameof(logFilePath));
            _logger = new FileLogger(logFilePath);
            if (!(_logger as FileLogger).CheckPath())
            {
                throw new ArgumentException("Bad path.");
            }

            if (minThreads < 0)
            {
                throw new ArgumentException("Need more min threads.");
            }

            if (maxThreads < minThreads || maxThreads == 0)
            {
                throw new ArgumentException("Need more max threads.");
            }

            MinThreads = minThreads;
            MaxThreads = maxThreads;
            _userTaskQueue = new FastPriorityQueue<ProrityContainer>(maxThreads);
            _lastTaskCleanup = DateTime.Now;
            _cleanupInterval = cleanupInterval;

            lock (_taskList)
            {
                AddTasks(MinThreads);
            }

            _logger.writeMessage($"Created {MinThreads} threads.");
        }

        ~ThreadPool()
        {
            if (!_disposed)
            {
                KillAllTasks();
            }
        }

        #endregion

        public void Dispose()
        {
            _disposed = true;
            KillAllTasks();
        }

        #region  Properties

        public int MinThreads { get; }
        
        public int MaxThreads { get; }

        #endregion
        public void AddUserTask(Action action, float priority = 2)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Object has already been disposed.");
            }

            if (action == null) throw new ArgumentNullException(nameof(action) + "cannot be null.");

            lock (_userTaskQueue)
            {
                if (_userTaskQueue.Count != MaxThreads)
                {
                    _userTaskQueue.Enqueue(new ProrityContainer(action), priority);
                    _logger.writeMessage("Added new user task.");
                }
                else
                {
                    var e = new InvalidOperationException("Tried to add too many tasks.");
                    _logger.writeException(e, "Error adding task");
                }
            }
        }

        private void AddTask()
        {
            var task = new Task(TaskMethod, TaskCreationOptions.LongRunning);
            _taskList.Add(task);
            task.Start();
        }

        private void AddTasks(int tasksToAdd = 1)
        {
            for (int i = 0; i < tasksToAdd; i++)
            {
                AddTask();
            }
        }

        private void KillAllTasks()
        {
            lock (_stopCreatingTasksLock)
            {
                _stopCreatingTasks = true;
            }

            bool queueIsEmpty = false;
            while (!queueIsEmpty)
            {
                lock (_userTaskQueue)
                {
                    if (_userTaskQueue.Count == 0)
                    {
                        queueIsEmpty = true;
                    }
                }

                if (!queueIsEmpty)
                {
                    Thread.Yield();
                }
            }

            int tasksToRemove;

            lock (_taskList)
            {
                tasksToRemove = _taskList.Count;
            }

            while (tasksToRemove > 0)
            {
                KillTask();
                tasksToRemove--;
            }
        }

        private void KillTask()
        {
            bool succeeded = false;

            while (!succeeded)
            {
                lock (_userTaskQueue)
                {
                    if (_userTaskQueue.Count < MaxThreads)
                    {
                        _userTaskQueue.Enqueue(new ProrityContainer(null), 0);
                        succeeded = true;
                    }
                }

                Thread.Yield();
            }
        }
        

        private void CheckTaskList()
        {
            lock (_taskList)
            {
                //Removing all completed tasks and adding enough tasks so that there are at least MinThreads of them
                _taskList.RemoveAll(task => (task.IsCompleted) || (task.IsCanceled) || (task.IsFaulted));
                AddTasks(MinThreads - _taskList.Count);

                bool stopCreatingTasks;

                lock (_stopCreatingTasksLock)
                {
                    stopCreatingTasks = _stopCreatingTasks;
                }

                if (!stopCreatingTasks)
                {
                    lock (_userTaskQueue)
                    {
                        var tasksToAdd = _userTaskQueue.Count - _taskList.Count;
                        if (tasksToAdd > 0)
                        {
                            AddTasks(tasksToAdd);
                        }
                    }
                }
            }
        }

        private void TaskMethod()
        {
            Action action = null;
            var finishExecution = false;

            do
            {
                lock (_cleanupLock)
                {
                    if ((DateTime.Now - _lastTaskCleanup) > TimeSpan.FromMilliseconds(_cleanupInterval))
                    {
                        CheckTaskList();
                        _lastTaskCleanup = DateTime.Now;
                    }
                }

                lock (_userTaskQueue)
                {
                    if (_userTaskQueue.Count != 0)
                    {
                        action = _userTaskQueue.Dequeue().Action;
                        if (action == null)
                        {
                            finishExecution = true;
                        }
                    }
                }

                if (action != null)
                {
                    try
                    {
                        action();
                        _logger.writeMessage("User task completed.");
                    }
                    catch (Exception e)
                    {
                        _logger.writeException(e, "An exception occurred during task execution.");
                        finishExecution = true;
                    }
                }

                action = null;
            } while (!finishExecution);
        }

        private class ProrityContainer : FastPriorityQueueNode
        {
            public readonly Action Action;

            public ProrityContainer(Action action)
            {
                Action = action;
            }
        }
    }
}
