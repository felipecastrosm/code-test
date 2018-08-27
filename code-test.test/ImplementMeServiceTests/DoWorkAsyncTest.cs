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
    public class ImplementMeServiceTest_DoWorkAsync
    {
        private readonly CancellationTokenSource _cancellationTokenSource;

        public ImplementMeServiceTest_DoWorkAsync()
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }

        private ImplementMeService SetupService()
        {
            var service = Substitute.ForPartsOf<ImplementMeService>(Substitute.For<IKVRepository>(),
                Substitute.For<ILogService>(),
                Substitute.For<IMessageProcessService>(),
                Substitute.For<IMessageQueService>(),
                null, _cancellationTokenSource, null);

            service.When(x => x.LogInfoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object[]>()))
                .DoNotCallBase();
            service.LogInfoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object[]>()).Returns(Task.CompletedTask);

            service.When(x => x.FetchMessagesAsync()).DoNotCallBase();
            service.FetchMessagesAsync().Returns(Task.FromResult(new MessageBatchResult<RingbaUOW>
            {
                IsSuccessfull = true,
                NumberOfMessages = 0,
                Messages = new List<MessageWrapper<RingbaUOW>>(),
            }));

            service.When(x => x.ProcessMessageAsync(Arg.Any<MessageWrapper<RingbaUOW>>(),
                Arg.Any<ConcurrentBag<UpdateBatchRequest>>(), Arg.Any<ConcurrentBag<string>>())).DoNotCallBase();
            service.ProcessMessageAsync(Arg.Any<MessageWrapper<RingbaUOW>>(),
                    Arg.Any<ConcurrentBag<UpdateBatchRequest>>(), Arg.Any<ConcurrentBag<string>>())
                .Returns(Task.CompletedTask);

            service.When(x => x.UpdateMessagesAsync(Arg.Any<ConcurrentBag<UpdateBatchRequest>>()))
                .DoNotCallBase();
            service.UpdateMessagesAsync(Arg.Any<ConcurrentBag<UpdateBatchRequest>>()).Returns(Task.FromResult(true));

            service.When(x => x.ScheduleCleanupAsync(Arg.Any<ConcurrentBag<string>>())).DoNotCallBase();
            service.ScheduleCleanupAsync(Arg.Any<ConcurrentBag<string>>()).Returns(Task.CompletedTask);

            return service;
        }

        [Fact]
        public async Task ShouldFetchMessages()
        {
            //Arrange
            var service = SetupService();

            //Act
            await service.DoWorkAsync(true);

            //Assert
            Assert.Equal(1, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "FetchMessagesAsync"));
        }

        [Fact]
        public async Task ShouldProcessEveryMessage()
        {
            //Arrange
            var service = SetupService();
            service.FetchMessagesAsync().Returns(Task.FromResult(new MessageBatchResult<RingbaUOW>
            {
                IsSuccessfull = true,
                NumberOfMessages = 5,
                Messages = new List<MessageWrapper<RingbaUOW>>
                {
                    new MessageWrapper<RingbaUOW>(),
                    new MessageWrapper<RingbaUOW>(),
                    new MessageWrapper<RingbaUOW>(),
                    new MessageWrapper<RingbaUOW>(),
                    new MessageWrapper<RingbaUOW>(),
                }
            }));

            //Act
            await service.DoWorkAsync(true);

            //Assert
            Assert.Equal(5, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "ProcessMessageAsync"));
        }

        [Fact]
        public async Task ShouldUpdateEligibleMessages()
        {
            //Arrange
            var service = SetupService();
            service.FetchMessagesAsync().Returns(Task.FromResult(new MessageBatchResult<RingbaUOW>
            {
                IsSuccessfull = true,
                NumberOfMessages = 5,
                Messages = new List<MessageWrapper<RingbaUOW>>
                {
                    new MessageWrapper<RingbaUOW>(),
                    new MessageWrapper<RingbaUOW>(),
                    new MessageWrapper<RingbaUOW>(),
                    new MessageWrapper<RingbaUOW>(),
                    new MessageWrapper<RingbaUOW>(),
                }
            }));

            service.ProcessMessageAsync(Arg.Any<MessageWrapper<RingbaUOW>>(),
                    Arg.Any<ConcurrentBag<UpdateBatchRequest>>(), Arg.Any<ConcurrentBag<string>>())
                .Returns(Task.CompletedTask).AndDoes(callInfo =>
                {
                    (callInfo.Args().Single(a => a.GetType() == typeof(ConcurrentBag<UpdateBatchRequest>)) as
                            ConcurrentBag<UpdateBatchRequest>)?
                        .Add(new UpdateBatchRequest {Id = "", MessageCompleted = true});
                });

            //Act
            await service.DoWorkAsync(true);

            //Assert
            Assert.Equal(1, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "UpdateMessagesAsync"));
            Assert.Equal(5,
                (service.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "UpdateMessagesAsync").GetArguments()
                    .Single() as ConcurrentBag<UpdateBatchRequest>)?.Count);
        }

        [Fact]
        public async Task ShouldScheduleCleanup_IfUpdateSuccessful()
        {
            //Arrange
            var service = SetupService();
            service.FetchMessagesAsync().Returns(Task.FromResult(new MessageBatchResult<RingbaUOW>
            {
                IsSuccessfull = true,
                NumberOfMessages = 1,
                Messages = new List<MessageWrapper<RingbaUOW>>
                {
                    new MessageWrapper<RingbaUOW>(),
                }
            }));

            service.ProcessMessageAsync(Arg.Any<MessageWrapper<RingbaUOW>>(),
                    Arg.Any<ConcurrentBag<UpdateBatchRequest>>(), Arg.Any<ConcurrentBag<string>>())
                .Returns(Task.CompletedTask).AndDoes(callInfo =>
                {
                    (callInfo.Args().Single(a => a.GetType() == typeof(ConcurrentBag<string>)) as
                            ConcurrentBag<string>)?
                        .Add("123");
                });

            //Act
            await service.DoWorkAsync(true);

            //Assert
            Assert.Equal(1, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "ScheduleCleanupAsync"));
            Assert.Equal(1,
                (service.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "ScheduleCleanupAsync").GetArguments()
                    .Single() as ConcurrentBag<string>)?.Count);
            Assert.Equal("123",
                (service.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "ScheduleCleanupAsync").GetArguments()
                    .Single() as ConcurrentBag<string>)?.Single());
        }

        [Fact]
        public async Task ShouldNotScheduleCleanup_IfUpdateUnsuccessful()
        {
            //Arrange
            var service = SetupService();
            service.FetchMessagesAsync().Returns(Task.FromResult(new MessageBatchResult<RingbaUOW>
            {
                IsSuccessfull = true,
                NumberOfMessages = 1,
                Messages = new List<MessageWrapper<RingbaUOW>>
                {
                    new MessageWrapper<RingbaUOW>(),
                }
            }));

            service.ProcessMessageAsync(Arg.Any<MessageWrapper<RingbaUOW>>(),
                    Arg.Any<ConcurrentBag<UpdateBatchRequest>>(), Arg.Any<ConcurrentBag<string>>())
                .Returns(Task.CompletedTask).AndDoes(callInfo =>
                {
                    (callInfo.Args().Single(a => a.GetType() == typeof(ConcurrentBag<string>)) as
                            ConcurrentBag<string>)?
                        .Add("123");
                });

            service.UpdateMessagesAsync(Arg.Any<ConcurrentBag<UpdateBatchRequest>>()).Returns(Task.FromResult(false));

            //Act
            await service.DoWorkAsync(true);

            //Assert
            Assert.Equal(0, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "ScheduleCleanupAsync"));
        }

        [Fact]
        public async Task ShouldContinue_IfBatchIsNull()
        {
            //Arrange
            var service = SetupService();
            service.FetchMessagesAsync().Returns(Task.FromResult((MessageBatchResult<RingbaUOW>)null));

            //Act
            await service.DoWorkAsync(true);

            //Assert
            Assert.Equal(0, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "ProcessMessageAsync"));
            Assert.Equal(0, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "UpdateMessagesAsync"));
            Assert.Equal(0, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "ScheduleCleanupAsync"));
        }

        [Fact]
        public void ShouldStop_IfCancellationTokenCancelled()
        {
            //Arrange
            var service = SetupService();
            service.FetchMessagesAsync().Returns(Task.FromResult((MessageBatchResult<RingbaUOW>) null));

            var stopped = false;
            Task.Factory.StartNew(() =>
            {
                service.DoWorkAsync().Wait();
                stopped = true;
            });

            //Act
            _cancellationTokenSource.Cancel();

            //Assert
            Task.Delay(100).Wait();
            Assert.True(stopped);
        }


        [Fact]
        public void ShouldSetIsStoppedToTrue_WhenCancellationTokenCancelled()
        {
            //Arrange
            var service = SetupService();
            service.FetchMessagesAsync().Returns(Task.FromResult((MessageBatchResult<RingbaUOW>)null));

            Task.Factory.StartNew(() =>
            {
                service.DoWorkAsync().Wait();
            });

            //Act
            _cancellationTokenSource.Cancel();

            //Assert
            Task.Delay(100).Wait();
            Assert.True(service.IsStopped);
        }
    }
}
