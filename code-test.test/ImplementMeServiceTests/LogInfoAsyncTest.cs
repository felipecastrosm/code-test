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
    public class ImplementMeServiceTest_LogInfoAsync
    {
        private readonly ILogService _logService;

        public ImplementMeServiceTest_LogInfoAsync()
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

            return service;
        }

        [Fact]
        public async Task ShouldUseLogService()
        {
            //Arrange
            var service = SetupService();

            //Act
            await service.LogInfoAsync("SampleMessage", "SampleAction");

            //Assert
            Assert.Equal(1, _logService.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "LogAsync"));
        }

        [Fact]
        public async Task ShouldLogWithInfoLogLevel()
        {
            //Arrange
            var service = SetupService();

            //Act
            await service.LogInfoAsync("SampleMessage", "SampleAction");

            //Assert
            Assert.Equal(LOG_LEVEL.INFO,
                (LOG_LEVEL)_logService.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "LogAsync")
                    .GetArguments()[2]);
        }

        [Fact]
        public async Task ShouldLogGivenMessage()
        {
            //Arrange
            var service = SetupService();

            //Act
            await service.LogInfoAsync("SampleMessage", "SampleAction");

            //Assert
            Assert.Equal("SampleMessage",
                (string) _logService.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "LogAsync")
                    .GetArguments()[1]);
        }

        [Fact]
        public async Task ShouldLogGivenAction()
        {
            //Arrange
            var service = SetupService();

            //Act
            await service.LogInfoAsync("SampleMessage", "SampleAction");

            //Assert
            var args = ((object[])_logService.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "LogAsync")
                .GetArguments()[3])?[0];
            Assert.Equal("SampleAction", args?.GetType().GetProperty("ActionName")?.GetValue(args, null));
        }

        [Fact]
        public async Task ShouldLogGivenArgs()
        {
            //Arrange
            var service = SetupService();

            //Act
            await service.LogInfoAsync("SampleMessage", "SampleAction", new {SampleArgKey = "SampleArgValue"});

            //Assert
            var args = ((object[]) ((object[]) _logService.ReceivedCalls()
                .Single(c => c.GetMethodInfo().Name == "LogAsync").GetArguments()[3])?[1])?[0];
            Assert.Equal("SampleArgValue", args?.GetType().GetProperty("SampleArgKey")?.GetValue(args, null));
        }
    }
}