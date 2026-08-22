using System;
using System.Threading.Tasks;

namespace EDM.Services
{
    /// <summary>
    /// Utility class for running background tasks with automatic exception logging.
    /// Ensures all background operations are properly audited and don't crash silently.
    /// </summary>
    public static class BackgroundTaskManager
    {
        /// <summary>
        /// Runs an async task in the background with automatic exception logging.
        /// </summary>
        /// <param name="taskName">Name of the task for logging purposes</param>
        /// <param name="taskFunc">The async task to run</param>
        /// <returns>A task that completes when the operation is finished or logged as failed</returns>
        public static async Task RunBackgroundTaskAsync(string taskName, Func<Task> taskFunc)
        {
            if (string.IsNullOrWhiteSpace(taskName))
                taskName = "UnnamedTask";

            try
            {
                LoggingService.Log($"[BackgroundTask] Starting: {taskName}");
                await taskFunc().ConfigureAwait(false);
                LoggingService.Log($"[BackgroundTask] Completed: {taskName}");
            }
            catch (OperationCanceledException)
            {
                LoggingService.LogWarning($"[BackgroundTask] Cancelled: {taskName}");
            }
            catch (Exception ex)
            {
                LoggingService.LogBackgroundTaskFailure(taskName, ex);
            }
        }

        /// <summary>
        /// Runs an async task with a return value in the background with automatic exception logging.
        /// </summary>
        /// <typeparam name="T">The return type of the task</typeparam>
        /// <param name="taskName">Name of the task for logging purposes</param>
        /// <param name="taskFunc">The async task to run</param>
        /// <param name="defaultValue">Value to return if task fails</param>
        /// <returns>The result of the task, or defaultValue if an exception occurs</returns>
        public static async Task<T> RunBackgroundTaskAsync<T>(string taskName, Func<Task<T>> taskFunc, T defaultValue = default!)
        {
            if (string.IsNullOrWhiteSpace(taskName))
                taskName = "UnnamedTask";

            try
            {
                LoggingService.Log($"[BackgroundTask] Starting: {taskName}");
                var result = await taskFunc().ConfigureAwait(false);
                LoggingService.Log($"[BackgroundTask] Completed: {taskName}");
                return result;
            }
            catch (OperationCanceledException)
            {
                LoggingService.LogWarning($"[BackgroundTask] Cancelled: {taskName}");
                return defaultValue;
            }
            catch (Exception ex)
            {
                LoggingService.LogBackgroundTaskFailure(taskName, ex);
                return defaultValue;
            }
        }

        /// <summary>
        /// Runs a synchronous task in the background with automatic exception logging.
        /// </summary>
        public static void RunBackgroundTask(string taskName, Action taskFunc)
        {
            if (string.IsNullOrWhiteSpace(taskName))
                taskName = "UnnamedTask";

            Task.Run(() =>
            {
                try
                {
                    LoggingService.Log($"[BackgroundTask] Starting: {taskName}");
                    taskFunc();
                    LoggingService.Log($"[BackgroundTask] Completed: {taskName}");
                }
                catch (Exception ex)
                {
                    LoggingService.LogBackgroundTaskFailure(taskName, ex);
                }
            });
        }

        /// <summary>
        /// Runs a synchronous task with a return value in the background with automatic exception logging.
        /// </summary>
        public static Task<T> RunBackgroundTask<T>(string taskName, Func<T> taskFunc, T defaultValue = default!)
        {
            if (string.IsNullOrWhiteSpace(taskName))
                taskName = "UnnamedTask";

            return Task.Run(() =>
            {
                try
                {
                    LoggingService.Log($"[BackgroundTask] Starting: {taskName}");
                    var result = taskFunc();
                    LoggingService.Log($"[BackgroundTask] Completed: {taskName}");
                    return result;
                }
                catch (Exception ex)
                {
                    LoggingService.LogBackgroundTaskFailure(taskName, ex);
                    return defaultValue;
                }
            });
        }

        /// <summary>
        /// Runs a fire-and-forget async operation without awaiting.
        /// Logs any exceptions that occur.
        /// </summary>
        public static void FireAndForget(string operationName, Func<Task> taskFunc)
        {
            if (string.IsNullOrWhiteSpace(operationName))
                operationName = "UnnamedOperation";

            _ = Task.Run(async () =>
            {
                try
                {
                    LoggingService.Log($"[FireAndForget] Starting: {operationName}");
                    await taskFunc().ConfigureAwait(false);
                    LoggingService.Log($"[FireAndForget] Completed: {operationName}");
                }
                catch (Exception ex)
                {
                    LoggingService.LogException($"FireAndForget: {operationName}", ex);
                }
            });
        }

        /// <summary>
        /// Schedules a delayed task to run after the specified delay.
        /// Logs progress and any exceptions.
        /// </summary>
        public static async Task ScheduleTaskAsync(string taskName, TimeSpan delay, Func<Task> taskFunc)
        {
            if (string.IsNullOrWhiteSpace(taskName))
                taskName = "UnnamedTask";

            try
            {
                LoggingService.Log($"[ScheduledTask] Scheduled: {taskName} (delay: {delay.TotalSeconds}s)");
                await Task.Delay(delay).ConfigureAwait(false);
                await RunBackgroundTaskAsync(taskName, taskFunc).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"ScheduledTask: {taskName}", ex);
            }
        }
    }
}
