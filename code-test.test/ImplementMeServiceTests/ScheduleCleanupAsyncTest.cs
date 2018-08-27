using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using code_test;
using NSubstitute;
using RingbaLibs;
using RingbaLibs.Models;
using Xunit;

namespace Tests
{
    public class ImplementMeServiceTest_ScheduleCleanupAsync
    {
        private readonly IMessageQueService _queService;

        public ImplementMeServiceTest_ScheduleCleanupAsync()
        {
            var queService = Substitute.For<IMessageQueService>();

            queService.UpdateMessagesAsync(Arg.Any<IEnumerable<UpdateBatchRequest>>())
                .Returns(Task.FromResult(new ActionResult {IsSuccessfull = true}));

            _queService = queService;
        }

        private ImplementMeService SetupService()
        {
            var service = Substitute.ForPartsOf<ImplementMeService>(Substitute.For<IKVRepository>(),
                Substitute.For<ILogService>(),
                Substitute.For<IMessageProcessService>(),
                _queService);

            service.When(x => x.LogInfoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object[]>()))
                .DoNotCallBase();
            service.LogInfoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object[]>()).Returns(Task.CompletedTask);

            service.When(x => x.LogErrorAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>()))
                .DoNotCallBase();
            service.LogErrorAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);

            service.When(x => x.ClearTryCountAsync(Arg.Any<ConcurrentBag<string>>(), Arg.Any<CancellationToken>())).DoNotCallBase();
            service.ClearTryCountAsync(Arg.Any<ConcurrentBag<string>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult((IEnumerable<ActionResult>) new List<ActionResult>()));

            return service;
        }

        [Fact]
        public async Task ShouldNotCallClearTryCountAsync_BeforeTwoSeconds()
        {
            //Arrange
            var service = SetupService();

            //Act
            await service.ScheduleCleanupAsync(new ConcurrentBag<string>());

            //Assert
            Assert.Equal(0, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "ClearTryCountAsync"));
        }

        [Fact]
        public async Task ShouldCallClearTryCountAsync_AfterTwoSeconds()
        {
            //Arrange
            var service = SetupService();

            //Act
            await service.ScheduleCleanupAsync(new ConcurrentBag<string>());

            //Assert
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.Equal(1, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "ClearTryCountAsync"));
        }

        [Fact]
        public async Task ShouldClearTryCountForAllReceivedIds()
        {
            //Arrange
            var service = SetupService();
            var ids = new ConcurrentBag<string> {"1", "2", "3"};

            //Act
            await service.ScheduleCleanupAsync(ids);

            //Assert
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.Equal(1, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "ClearTryCountAsync"));
            Assert.Equal(true,
                (service.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "ClearTryCountAsync").GetArguments()[0]
                    as IEnumerable<string>)?.SequenceEqual(ids));
        }

        [Fact]
        public async Task ShouldLogErrorForAllUnsuccessfulResults()
        {
            //Arrange
            var service = SetupService();
            service.ClearTryCountAsync(Arg.Any<ConcurrentBag<string>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult((IEnumerable<ActionResult>) new List<ActionResult>
                {
                    new ActionResult {IsSuccessfull = false},
                    new ActionResult {IsSuccessfull = false},
                    new ActionResult {IsSuccessfull = false},
                }));

            //Act
            await service.ScheduleCleanupAsync(new ConcurrentBag<string>());

            //Assert
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.Equal(3, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "LogErrorAsync"));
        }
    }
}