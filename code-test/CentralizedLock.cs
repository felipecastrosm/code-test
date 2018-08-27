using System;
using System.Threading.Tasks;
using RingbaLibs;

namespace code_test
{
    public class CentralizedLock
    {
        private readonly IKVRepository _repository;

        private readonly int _timeoutInSeconds;

        private readonly ILogService _logService;

        public CentralizedLock(IKVRepository repository, int timeoutInSeconds, ILogService logService)
        {
            _repository = repository;
            _timeoutInSeconds = timeoutInSeconds;
            _logService = logService;
        }

        public virtual async Task<CentralizedLockItem> TryAcquireAsync(string key)
        {
            var result = await _repository.CreateIfNotExistAsync(new CreateKVRequest<string>
            {
                Item = Environment.MachineName,
                Key = key,
                ExpireInSeconds = _timeoutInSeconds,
            });

            if (!result.IsSuccessfull)
            {
                await _logService.LogAsync(Guid.NewGuid().ToString(),
                    $"An error occurred while trying to create lock entry: [{result.ErrorCode}] {result.ErrorMessage}",
                    LOG_LEVEL.EXCEPTION, new
                    {
                        MachineName = Environment.MachineName,
                        ServiceName = "CentralizedLock",
                        ActionName = "CreateLockEntry",
                        ErrorCode = result.ErrorCode,
                        ErrorMessage = result.ErrorMessage,
                    });

                return new CentralizedLockItem(() => { }, false);
            }

            return new CentralizedLockItem(() =>
            {
                if (result.Item)
                {
                    var deleteResult = _repository.DeleteAsync(key).Result;

                    if (!deleteResult.IsSuccessfull)
                    {
                        _logService.LogAsync(Guid.NewGuid().ToString(),
                            $"An error occurred while trying to delete lock entry: [{result.ErrorCode}] {result.ErrorMessage}",
                            LOG_LEVEL.EXCEPTION, new
                            {
                                MachineName = Environment.MachineName,
                                ServiceName = "CentralizedLock",
                                ActionName = "DeleteLockEntry",
                                ErrorCode = result.ErrorCode,
                                ErrorMessage = result.ErrorMessage,
                            }).Wait();
                    }
                }
            }, result.Item);
        }
    }

    public class CentralizedLockItem : IDisposable
    {
        private readonly Action _releaseAction;

        public bool IsLocked { get; }

        public CentralizedLockItem(Action releaseAction, bool isLocked)
        {
            _releaseAction = releaseAction;
            IsLocked = isLocked;
        }

        public void Dispose()
        {
            _releaseAction();
        }
    }
}
