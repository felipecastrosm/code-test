using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RingbaLibs;
using RingbaLibs.Models;

namespace code_test
{
    /// <summary>
    /// TODO: Fill in the implementation of this service
    /// </summary>
    public class ImplementMeService : IDisposable
    {
        #region private instance members
        private readonly IKVRepository _repository;
        private readonly ILogService _logservice;
        private readonly IMessageProcessService _processService;
        private readonly IMessageQueService _queService;
        private readonly TimeSpan _maxStopWait;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly CentralizedLock _centralizedLock;
        #endregion

        #region private static members
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        #endregion

        #region private constants
        private const string TryCountKeyPrefix = "RingbaUOW:TryCount:";
        private const string LockKeyPrefix = "RingbaUOW:Lock:";
        #endregion

        #region public instance members
        public virtual bool IsStopped { get; private set; }
        #endregion

        public ImplementMeService(IKVRepository repository,
            ILogService logService,
            IMessageProcessService messageProcessService,
            IMessageQueService messageQueService) : this(repository, logService, messageProcessService,
            messageQueService, null)
        {
        }

        public ImplementMeService(IKVRepository repository,
            ILogService logService,
            IMessageProcessService messageProcessService,
            IMessageQueService messageQueService,
            CentralizedLock centralizedLock = null, CancellationTokenSource cancellationTokenSource = null,
            TimeSpan? maxStopWait = null)
        {
            _repository = repository;
            _logservice = logService;
            _queService = messageQueService;
            _processService = messageProcessService;

            _cancellationTokenSource = cancellationTokenSource ?? new CancellationTokenSource();
            _maxStopWait = maxStopWait ?? TimeSpan.FromSeconds(5);

            _centralizedLock = centralizedLock ?? new CentralizedLock(repository, -1, _logservice);
        }

        public void DoWork()
        {
            DoWorkAsync().Wait();
        }

        public virtual async Task DoWorkAsync(bool runOnce = false)
        {
            await LogInfoAsync($"Starting ImplementMeService.DoWorkAsync @ {Environment.MachineName}", "Startup");

            do
            {
                var batch = await FetchMessagesAsync();

                if (batch == null)
                {
                    continue;
                }

                var updateBatchRequests = new ConcurrentBag<UpdateBatchRequest>();
                var successfulUowIds = new ConcurrentBag<string>();

                await batch.Messages.ForEachAsync(async message =>
                {
                    await ProcessMessageAsync(message, updateBatchRequests, successfulUowIds);
                }, _cancellationTokenSource.Token);

                await LogInfoAsync(
                    $"ImplementMeService.DoWorkAsync successfully processed {successfulUowIds.Count} out of {updateBatchRequests.Count} eligible messages @ {Environment.MachineName}",
                    "ProcessMessageBatch",
                    new
                    {
                        ElegibleMessages = updateBatchRequests.Count,
                        SuccessfullyProcessedMessages = successfulUowIds.Count
                    });

                if (await UpdateMessagesAsync(updateBatchRequests))
                {
                    await ScheduleCleanupAsync(successfulUowIds);
                }
            } while (!runOnce && !_cancellationTokenSource.IsCancellationRequested);

            IsStopped = true;
        }

        public virtual async Task<MessageBatchResult<RingbaUOW>> FetchMessagesAsync()
        {
            await LogInfoAsync(
                $"ImplementMeService.DoWorkAsync is trying to fetch a new message batch @ {Environment.MachineName}",
                "PreFetchMessageBatch");

            var batch = await _queService.GetMessagesFromQueAsync<RingbaUOW>(10, 30, 30);

            if (!batch.IsSuccessfull)
            {
                await LogErrorAsync(batch.ErrorCode, batch.ErrorMessage, "FetchMessageBatch");

                return null;
            }

            await LogInfoAsync(
                $"ImplementMeService.DoWorkAsync fetched {batch.NumberOfMessages} @ {Environment.MachineName}",
                "FetchMessageBatch");

            return batch;
        }

        public virtual async Task<bool> UpdateMessagesAsync(ConcurrentBag<UpdateBatchRequest> updateBatchRequests)
        {
            var updateResult = await _queService.UpdateMessagesAsync(updateBatchRequests);

            if (!updateResult.IsSuccessfull)
            {
                await LogErrorAsync(updateResult.ErrorCode, updateResult.ErrorMessage, "UpdateMessageBatch");
                return false;
            }

            await LogInfoAsync(
                $"ImplementMeService.DoWorkAsync successfully updated {updateBatchRequests.Count} messages @ {Environment.MachineName}",
                "UpdateMessageBatch",
                new
                {
                    UpdatedMessages = updateBatchRequests.Count,
                });

            return true;
        }

        public virtual Task ScheduleCleanupAsync(ConcurrentBag<string> successfulUowIds)
        {
            return Task.Factory.StartNew(async () =>
            {
                await LogInfoAsync(
                    $"ImplementMeService.DoWorkAsync scheduled the cleanup of {successfulUowIds.Count} tryCount entries @ {Environment.MachineName}",
                    "ScheduleCleanup",
                    new
                    {
                        ScheduledEntriesCleanup = successfulUowIds.Count,
                    });

                //Only clear after message is guaranteed not to show back up
                await Task.Delay(TimeSpan.FromSeconds(2), _cancellationTokenSource.Token);

                var clearResults = (await ClearTryCountAsync(successfulUowIds, _cancellationTokenSource.Token)).ToList();

                var successfulCleanupsCount = clearResults.Count(r => r.IsSuccessfull);

                await LogInfoAsync(
                    $"ImplementMeService.DoWorkAsync successfully cleaned up {successfulCleanupsCount} tryCount entries @ {Environment.MachineName}",
                    "ExecuteCleanup",
                    new
                    {
                        SuccessfulCleanups = successfulCleanupsCount,
                        UnsuccessfulCleanups = clearResults.Count - successfulCleanupsCount,
                    });

                await clearResults.Where(r => !r.IsSuccessfull).ForEachAsync(
                    async result => { await LogErrorAsync(result.ErrorCode, result.ErrorMessage, "ExecuteCleanup"); },
                    _cancellationTokenSource.Token);
            });
        }

        public virtual async Task<IEnumerable<ActionResult>> ClearTryCountAsync(IEnumerable<string> uowIds, CancellationToken cancellationToken)
        {
            var deleteResults = new ConcurrentBag<ActionResult>();

            await uowIds.ForEachAsync(async uowId =>
            {
                var tryCountKey = $"{TryCountKeyPrefix}{uowId}";

                deleteResults.Add(await _repository.DeleteAsync(tryCountKey));
            }, cancellationToken);

            return deleteResults;
        }

        public virtual async Task ProcessMessageAsync(MessageWrapper<RingbaUOW> message,
            ConcurrentBag<UpdateBatchRequest> updateBatchRequests, ConcurrentBag<string> successfulUowIds)
        {
            await LogInfoAsync(
                $"ImplementMeService.ProcessMessageAsync starting for UOWId {message.Body.UOWId} @ {Environment.MachineName}",
                "PreProcessMessage",
                new
                {
                    UOWId = message.Body.UOWId,
                });

            using (var lockItem = await _centralizedLock.TryAcquireAsync($"{LockKeyPrefix}{message.Body.UOWId}"))
            {
                if (lockItem.IsLocked)
                {
                    await LogInfoAsync(
                        $"ImplementMeService.ProcessMessageAsync successfully acquired lock for UOWId {message.Body.UOWId} @ {Environment.MachineName}",
                        "AcquireLock",
                        new
                        {
                            UOWId = message.Body.UOWId,
                        });

                    ActionResult result = null;
                    Exception exception = null;

                    try
                    {
                        result = await _processService.ProccessMessageAsync(message.Body);

                        if (!result.IsSuccessfull)
                        {
                            await LogErrorAsync(result.ErrorCode, result.ErrorMessage, "ProcessMessage");
                        }
                        else
                        {
                            await LogInfoAsync(
                                $"ImplementMeService.ProcessMessageAsync successfully processed UOWId {message.Body.UOWId} @ {Environment.MachineName}",
                                "ProcessMessage",
                                new
                                {
                                    UOWId = message.Body.UOWId,
                                });
                        }

                    }
                    catch (Exception e)
                    {
                        await _logservice.LogAsync(Guid.NewGuid().ToString(), e.Message, LOG_LEVEL.EXCEPTION, new
                        {
                            UOWId = message.Body.UOWId,
                            MachineName = Environment.MachineName,
                            ServiceName = "ImplementMeService",
                            ActionName = "ProcessMessage",
                            ExceptionMessage = e.Message,
                        });

                        exception = e;
                    }
                    finally
                    {
                        bool messageCompleted;

                        if (result != null && !result.IsSuccessfull || exception != null)
                        {
                            await _logservice.LogAsync(Guid.NewGuid().ToString(),
                                $"ImplementMeService.ProcessMessageAsync did not process UOWId {message.Body.UOWId} successfully, evaluating requeue @ {Environment.MachineName}",
                                LOG_LEVEL.WARNING,
                                new
                                {
                                    UOWId = message.Body.UOWId,
                                    MachineName = Environment.MachineName,
                                    ServiceName = "ImplementMeService",
                                    ActionName = "ProcessMessage",
                                });
                            messageCompleted = !await ShouldRequeueAsync(message.Body.UOWId, message.Body.CreationEPOCH,
                                message.Body.MaxAgeInSeconds, message.Body.MaxNumberOfRetries);
                        }
                        else
                        {
                            messageCompleted = true;
                        }

                        if (messageCompleted)
                        {
                            successfulUowIds.Add(message.Body.UOWId);
                        }

                        await LogInfoAsync(
                            $"ImplementMeService.ProcessMessageAsync queued UOWId {message.Body.UOWId} message update @ {Environment.MachineName}",
                            "EnqueueMessageUpdate",
                            new
                            {
                                UOWId = message.Body.UOWId,
                            });

                        updateBatchRequests.Add(new UpdateBatchRequest
                        {
                            Id = message.Id,
                            MessageCompleted = messageCompleted,
                        });
                    }
                }
            }
        }


        public virtual async Task<bool> ShouldRequeueAsync(string uowId, long creationEpoch, int maxAgeInSeconds,
            int maxNumberOfRetries)
        {
            var tryCountKey = $"{TryCountKeyPrefix}{uowId}";
            var createdAt = Epoch.AddSeconds(creationEpoch);

            if (maxAgeInSeconds != -1 && (DateTime.UtcNow - createdAt).TotalSeconds > maxAgeInSeconds)
            {
                await LogInfoAsync($"ImplementMeService detected an expired message with UOWId {uowId}",
                    "ShouldRequeue",
                    new
                    {
                        UOWId = uowId,
                    });

                return false;
            }

            if (maxNumberOfRetries != -1)
            {
                var getResult = await _repository.GetAsync<MessageWrapper<int>>(tryCountKey);

                if (getResult != null && !getResult.IsSuccessfull)
                {
                    await LogErrorAsync(getResult.ErrorCode, getResult.ErrorMessage, "GetMessageTryCount");

                    return true;
                }

                var uowTryCountEntry = getResult?.Item;

                if (uowTryCountEntry == null)
                {
                    await LogInfoAsync(
                        $"ImplementMeService did not find tryCount entry for UOWId {uowId}, assuming first try",
                        "ShouldRequeue",
                        new
                        {
                            UOWId = uowId,
                            TryCount = 1,
                        });

                    uowTryCountEntry = new MessageWrapper<int>
                    {
                        Body = 1,
                    };

                    var result = await _repository.CreateAsync(new CreateKVRequest<MessageWrapper<int>>
                    {
                        Item = uowTryCountEntry,
                        Key = tryCountKey,
                        ExpireInSeconds = -1,
                    });

                    if (!result.IsSuccessfull)
                    {
                        await LogErrorAsync(result.ErrorCode, result.ErrorMessage, "CreateMessageTryCountEntry");
                    }
                }
                else
                {
                    uowTryCountEntry.Body++;

                    await LogInfoAsync(
                        $"ImplementMeService detected try number {uowTryCountEntry.Body} for UOWId {uowId}",
                        "ShouldRequeue",
                        new
                        {
                            UOWId = uowId,
                            TryCount = uowTryCountEntry.Body,
                        });

                    var result = await _repository.UpdateAsync(tryCountKey, uowTryCountEntry);

                    if (!result.IsSuccessfull)
                    {
                        await LogErrorAsync(result.ErrorCode, result.ErrorMessage, "UpdateMessageTryCountEntry");
                    }
                    else
                    {
                        await LogInfoAsync(
                            $"ImplementMeService successfully updated message tryCount for UOWId {uowId}",
                            "ShouldRequeue",
                            new
                            {
                                UOWId = uowId,
                            });
                    }
                }

                var shouldRequeue = uowTryCountEntry.Body < maxNumberOfRetries;

                await LogInfoAsync(
                    $"ImplementMeService detected message tryCount {uowTryCountEntry.Body} for UOWId {uowId} with MaxNumberOfRetries {maxNumberOfRetries}",
                    "ShouldRequeue",
                    new
                    {
                        UOWId = uowId,
                        ShouldRequeue = shouldRequeue,
                    });

                return shouldRequeue;
            }

            return true;
        }

        public virtual Task LogErrorAsync(int errorCode, string errorMessage, string action)
        {
            return _logservice.LogAsync(Guid.NewGuid().ToString(),
                $"ImplementMeService encountered the following error on {action} step: [{errorCode}] {errorMessage}",
                LOG_LEVEL.EXCEPTION,
                new
                {
                    MachineName = Environment.MachineName,
                    ServiceName = "ImplementMeService",
                    ActionName = action,
                    ErrorCode = errorCode,
                    ErrorMessage = errorMessage,
                });
        }

        public virtual Task LogInfoAsync(string message, string action, params object[] args)
        {
            return _logservice.LogAsync(Guid.NewGuid().ToString(),
                message,
                LOG_LEVEL.INFO,
                new
                {
                    MachineName = Environment.MachineName,
                    ServiceName = "ImplementMeService",
                    ActionName = action,
                }, args);
        }

        public virtual void Stop()
        {
            LogInfoAsync($"ImplementMeService was requested do stop, cancelling token", "Stop").Wait();

            _cancellationTokenSource.Cancel();

            var cancellationRequestedTime = DateTime.UtcNow;

            while (!IsStopped && DateTime.UtcNow - cancellationRequestedTime < _maxStopWait)
            {
                LogInfoAsync($"ImplementMeService is waiting for processing to stop", "Stop",
                    new
                    {
                        MaxStopWaitInSeconds = _maxStopWait.TotalSeconds,
                    }).Wait();

                Task.Delay(TimeSpan.FromMilliseconds(100)).Wait();
            }

            if (IsStopped)
            {
                LogInfoAsync($"ImplementMeService stopped successfully", "Stop").Wait();
            }
            else
            {
                LogInfoAsync($"ImplementMeService did not stop gracefully on time, aborting wait", "Stop").Wait();
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
