using System;
using System.Threading;
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
        #endregion

        public ImplementMeService(
            IKVRepository repository,
            ILogService logService,
            IMessageProcessService messageProcessService,
            IMessageQueService messageQueService)
        {
            _repository = repository;
            _logservice = logService;
            _queService = messageQueService;
            _processService = messageProcessService;

        }

        public void DoWork()
        {
            // Pseudo-code to illustrate a naive implementation. Provide an optimal implementation.

            while (true)
            {
                var batch = _queService.GetMessagesFromQueAsync<RingbaUOW>(10, 30, 30).Result;

                foreach (var message in batch.Messages)
                {
                    var result = _processService.ProccessMessageAsync(message.Body).Result;

                    _queService.UpdateMessagesAsync(new UpdateBatchRequest[]
                    {
                        new UpdateBatchRequest
                        {
                            Id=message.Id,
                            MessageCompleted = result.IsSuccessfull
                        }
                    });

                }
            }
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
