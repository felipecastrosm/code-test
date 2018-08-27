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
    public class ImplementMeServiceTest_LogErrorAsync
    {
        private readonly ILogService _logService;

        public ImplementMeServiceTest_LogErrorAsync()
        {
            var logService = Substitute.For<ILogService>();

            logService.LogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<LOG_LEVEL>(), Arg.Any<object[]>())
                .Returns(Task.CompletedTask);

            _logService = logService;
        }

        private ImplementMeService SetupService()
        {
            var service = Substitute.ForPartsOf<ImplementMeService>(Substitute.For<IKVRepository>(),
                _logService,
                Substitute.For<IMessageProcessService>(),
                Substitute.For<IMessageQueService>());

            service.When(x => x.LogInfoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object[]>()))
                .DoNotCallBase();
            service.LogInfoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object[]>()).Returns(Task.CompletedTask);

            service.When(x => x.ClearTryCountAsync(Arg.Any<ConcurrentBag<string>>(), Arg.Any<CancellationToken>()))
                .DoNotCallBase();
            service.ClearTryCountAsync(Arg.Any<ConcurrentBag<string>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult((IEnumerable<ActionResult>) new List<ActionResult>()));

            return service;
        }

        [Fact]
        public async Task ShouldUseLogService()
        {
            //Arrange
            var service = SetupService();

            //Act
            await service.LogErrorAsync(1, "SampleMessage", "SampleAction");

            //Assert
            Assert.Equal(1, _logService.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "LogAsync"));
        }

        [Fact]
        public async Task ShouldLogWithExceptionLogLevel()
        {
            //Arrange
            var service = SetupService();

            //Act
            await service.LogErrorAsync(1, "SampleMessage", "SampleAction");

            //Assert
            Assert.Equal(LOG_LEVEL.EXCEPTION,
                (LOG_LEVEL) _logService.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "LogAsync")
                    .GetArguments()[2]);
        }

        [Fact]
        public async Task ShouldLogGivenErrorCode()
        {
            //Arrange
            var service = SetupService();

            //Act
            await service.LogErrorAsync(1, "SampleMessage", "SampleAction");

            //Assert
            var args = ((object[]) _logService.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "LogAsync")
                .GetArguments()[3])?[0];
            Assert.Equal(1, args?.GetType().GetProperty("ErrorCode")?.GetValue(args, null));
        }

        [Fact]
        public async Task ShouldLogGivenErrorMessage()
        {
            //Arrange
            var service = SetupService();

            //Act
            await service.LogErrorAsync(1, "SampleMessage", "SampleAction");

            //Assert
            var args = ((object[])_logService.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "LogAsync")
                .GetArguments()[3])?[0];
            Assert.Equal("SampleMessage", args?.GetType().GetProperty("ErrorMessage")?.GetValue(args, null));
        }

        [Fact]
        public async Task ShouldLogGivenAction()
        {
            //Arrange
            var service = SetupService();

            //Act
            await service.LogErrorAsync(1, "SampleMessage", "SampleAction");

            //Assert
            var args = ((object[])_logService.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "LogAsync")
                .GetArguments()[3])?[0];
            Assert.Equal("SampleAction", args?.GetType().GetProperty("ActionName")?.GetValue(args, null));
        }
    }
}