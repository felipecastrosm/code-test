using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using code_test;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RingbaLibs;
using RingbaLibs.Models;
using Xunit;

namespace Tests
{
    public class ImplementMeServiceTest_ProcessMessageAsync
    {
        private readonly IKVRepository _repository;
        private readonly CentralizedLock _centralizedLock;
        private readonly IMessageProcessService _processService;
        private readonly ILogService _logService;

        public ImplementMeServiceTest_ProcessMessageAsync()
        {
            var repository = Substitute.For<IKVRepository>();

            repository.DeleteAsync(Arg.Any<string>())
                .Returns(Task.FromResult(new ActionResult {IsSuccessfull = true}));

            _repository = repository;

            var logService = Substitute.For<ILogService>();

            _logService = logService;

            var centralizedLock = Substitute.For<CentralizedLock>(_repository, 30, _logService);

            centralizedLock.TryAcquireAsync(Arg.Any<string>())
                .Returns(Task.FromResult(new CentralizedLockItem(() => { }, true)));

            _centralizedLock = centralizedLock;

            var processService = Substitute.For<IMessageProcessService>();

            processService.ProccessMessageAsync(Arg.Any<RingbaUOW>())
                .Returns(Task.FromResult(new ActionResult {IsSuccessfull = true}));

            _processService = processService;
        }

        private ImplementMeService SetupService()
        {
            var service = Substitute.ForPartsOf<ImplementMeService>(_repository,
                _logService,
                _processService,
                Substitute.For<IMessageQueService>(),
                _centralizedLock, null, null);

            service.When(x => x.LogInfoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object[]>()))
                .DoNotCallBase();
            service.LogInfoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object[]>()).Returns(Task.CompletedTask);

            service.When(x => x.LogErrorAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>()))
                .DoNotCallBase();
            service.LogErrorAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);

            service.When(x => x.ClearTryCountAsync(Arg.Any<ConcurrentBag<string>>(), Arg.Any<CancellationToken>()))
                .DoNotCallBase();
            service.ClearTryCountAsync(Arg.Any<ConcurrentBag<string>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult((IEnumerable<ActionResult>) new List<ActionResult>()));

            return service;
        }

        [Fact]
        public async Task ShouldTryAcquireLock()
        {
            //Arrange
            var service = SetupService();

            //Act
            await service.ProcessMessageAsync(new MessageWrapper<RingbaUOW> {Body = new RingbaUOW()},
                new ConcurrentBag<UpdateBatchRequest>(),
                new ConcurrentBag<string>());

            //Assert
            Assert.Equal(1, _centralizedLock.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "TryAcquireAsync"));
        }

        [Fact]
        public async Task ShouldNotProcess_IfItemIsNotLocked()
        {
            //Arrange
            var service = SetupService();

            _centralizedLock.TryAcquireAsync(Arg.Any<string>())
                .Returns(Task.FromResult(new CentralizedLockItem(() => { }, false)));

            //Act
            await service.ProcessMessageAsync(new MessageWrapper<RingbaUOW> {Body = new RingbaUOW()},
                new ConcurrentBag<UpdateBatchRequest>(),
                new ConcurrentBag<string>());

            //Assert
            Assert.Equal(0, _processService.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "ProccessMessageAsync"));
        }

        [Fact]
        public async Task ShouldCallProcessMessageWithMessageBody()
        {
            //Arrange
            var service = SetupService();
            var ringbaUow = new RingbaUOW();

            //Act
            await service.ProcessMessageAsync(new MessageWrapper<RingbaUOW> {Body = ringbaUow},
                new ConcurrentBag<UpdateBatchRequest>(),
                new ConcurrentBag<string>());

            //Assert
            Assert.Equal(1, _processService.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "ProccessMessageAsync"));
            Assert.Same(ringbaUow,
                _processService.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "ProccessMessageAsync")
                    .GetArguments()[0] as RingbaUOW);
        }

        [Fact]
        public async Task ShouldLogError_IfProcessMessageIsUnsuccessful()
        {
            //Arrange
            var service = SetupService();
            _processService.ProccessMessageAsync(Arg.Any<RingbaUOW>())
                .Returns(Task.FromResult(new ActionResult {IsSuccessfull = false}));

            //Act
            await service.ProcessMessageAsync(new MessageWrapper<RingbaUOW> {Body = new RingbaUOW()},
                new ConcurrentBag<UpdateBatchRequest>(),
                new ConcurrentBag<string>());

            //Assert
            Assert.Equal(1, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "LogErrorAsync"));
        }

        [Fact]
        public async Task ShouldLogException_IfExceptionIsThrown()
        {
            //Arrange
            var service = SetupService();
            _processService.ProccessMessageAsync(Arg.Any<RingbaUOW>())
                .Throws(new Exception("SampleException"));

            //Act
            await service.ProcessMessageAsync(new MessageWrapper<RingbaUOW> { Body = new RingbaUOW() },
                new ConcurrentBag<UpdateBatchRequest>(),
                new ConcurrentBag<string>());

            //Assert
            Assert.Equal(1,
                _logService.ReceivedCalls().Count(c =>
                    c.GetMethodInfo().Name == "LogAsync" && (LOG_LEVEL) c.GetArguments()[2] == LOG_LEVEL.EXCEPTION));
        }

        [Fact]
        public async Task ShouldLogWarning_IfMessageProcessingIsUnsuccessful()
        {
            //Arrange
            var service = SetupService();
            _processService.ProccessMessageAsync(Arg.Any<RingbaUOW>())
                .Returns(Task.FromResult(new ActionResult { IsSuccessfull = false }));

            //Act
            await service.ProcessMessageAsync(new MessageWrapper<RingbaUOW> { Body = new RingbaUOW() },
                new ConcurrentBag<UpdateBatchRequest>(),
                new ConcurrentBag<string>());

            //Assert
            Assert.Equal(1,
                _logService.ReceivedCalls().Count(c =>
                    c.GetMethodInfo().Name == "LogAsync" && (LOG_LEVEL)c.GetArguments()[2] == LOG_LEVEL.WARNING));
        }

        [Fact]
        public async Task ShouldEvaluateRequeue_IfMessageProcessingIsUnsuccessful()
        {
            //Arrange
            var service = SetupService();
            _processService.ProccessMessageAsync(Arg.Any<RingbaUOW>())
                .Returns(Task.FromResult(new ActionResult { IsSuccessfull = false }));

            //Act
            await service.ProcessMessageAsync(new MessageWrapper<RingbaUOW> { Body = new RingbaUOW() },
                new ConcurrentBag<UpdateBatchRequest>(),
                new ConcurrentBag<string>());

            //Assert
            Assert.Equal(1, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "ShouldRequeueAsync"));
        }

        [Fact]
        public async Task ShouldAddUowIdToSuccessfulUowIds_IfMessageProcessingIsSuccessful()
        {
            //Arrange
            var service = SetupService();
            var successfulUowIds = new ConcurrentBag<string>();

            //Act
            await service.ProcessMessageAsync(new MessageWrapper<RingbaUOW> {Body = new RingbaUOW()},
                new ConcurrentBag<UpdateBatchRequest>(), successfulUowIds);

            //Assert
            Assert.Equal(1, successfulUowIds.Count);
        }

        [Fact]
        public async Task ShouldAddUpdateRequest()
        {
            //Arrange
            var service = SetupService();
            var updateBatchRequest = new ConcurrentBag<UpdateBatchRequest>();

            //Act
            await service.ProcessMessageAsync(new MessageWrapper<RingbaUOW> { Body = new RingbaUOW() },
                updateBatchRequest, new ConcurrentBag<string>());

            //Assert
            Assert.Equal(1, updateBatchRequest.Count);
        }

        [Fact]
        public async Task ShouldUseShouldRequeueResultOnUpdateRequest()
        {
            //Arrange
            var service = SetupService();
            _processService.ProccessMessageAsync(Arg.Any<RingbaUOW>())
                .Returns(Task.FromResult(new ActionResult { IsSuccessfull = false }));
            service.ShouldRequeueAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>())
                .Returns(Task.FromResult(true));
            var updateBatchRequest = new ConcurrentBag<UpdateBatchRequest>();

            //Act
            await service.ProcessMessageAsync(new MessageWrapper<RingbaUOW> { Body = new RingbaUOW() },
                updateBatchRequest,
                new ConcurrentBag<string>());

            //Assert
            Assert.Equal(false, updateBatchRequest.Single().MessageCompleted);
        }
    }
}