using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using code_test;
using NSubstitute;
using RingbaLibs;
using RingbaLibs.Models;
using Xunit;

namespace Tests
{
    public class ImplementMeServiceTest_Stop
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly TimeSpan _maxStopWait;

        public ImplementMeServiceTest_Stop()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _maxStopWait = TimeSpan.FromSeconds(1);
        }

        private ImplementMeService SetupService()
        {
            var service = Substitute.ForPartsOf<ImplementMeService>(Substitute.For<IKVRepository>(),
                Substitute.For<ILogService>(),
                Substitute.For<IMessageProcessService>(),
                Substitute.For<IMessageQueService>(), null, _cancellationTokenSource, _maxStopWait);

            service.When(x => x.LogInfoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object[]>()))
                .DoNotCallBase();
            service.LogInfoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object[]>()).Returns(Task.CompletedTask);

            return service;
        }

        [Fact]
        public void ShouldCancelCancellationToken()
        {
            //Arrange
            var service = SetupService();

            //Act
            service.Stop();

            //Assert
            Assert.True(_cancellationTokenSource.IsCancellationRequested);
        }

        [Fact]
        public void ShouldNotWaitMoreThanMaxWait()
        {
            //Arrange
            var service = SetupService();

            var stopwatch = Stopwatch.StartNew();
            //Act
            service.Stop();

            //Assert
            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed < _maxStopWait.Add(TimeSpan.FromMilliseconds(200)));
        }

        [Fact]
        public void ShouldEnd_WhenServiceIsStopped()
        {
            //Arrange
            var service = SetupService();
            service.IsStopped.Returns(false);
            Task.Factory.StartNew(() =>
            {
                Task.Delay(300).Wait();
                service.IsStopped.Returns(true);
            });

            var stopwatch = Stopwatch.StartNew();
            //Act
            service.Stop();

            //Assert
            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(500));
        }
    }
}