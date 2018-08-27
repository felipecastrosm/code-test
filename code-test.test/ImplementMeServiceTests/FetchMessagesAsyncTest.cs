using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using code_test;
using NSubstitute;
using RingbaLibs;
using RingbaLibs.Models;
using Xunit;

namespace Tests
{
    public class ImplementMeServiceTest_FetchMessagesAsync
    {
        private readonly IMessageQueService _queService;

        public ImplementMeServiceTest_FetchMessagesAsync()
        {
            var queService = Substitute.For<IMessageQueService>();

            queService.GetMessagesFromQueAsync<RingbaUOW>(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>())
                .Returns(Task.FromResult(new MessageBatchResult<RingbaUOW>
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
                    },
                }));

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

            return service;
        }

        [Fact]
        public async Task ShouldGetMessagesFromQue()
        {
            //Arrange
            var service = SetupService();

            //Act
            await service.FetchMessagesAsync();

            //Assert
            Assert.Equal(1, _queService.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "GetMessagesFromQueAsync"));
        }

        [Fact]
        public async Task ShouldReturnNull_IfGetMessagesIsUnsuccessful()
        {
            //Arrange
            var service = SetupService();
            _queService.GetMessagesFromQueAsync<RingbaUOW>(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>())
                .Returns(Task.FromResult(new MessageBatchResult<RingbaUOW>
                {
                    IsSuccessfull = false,
                }));

            //Act
            var result = await service.FetchMessagesAsync();

            //Assert
            Assert.Equal(null, result);
        }

        [Fact]
        public async Task ShouldLogError_IfGetMessageIsUnsuccessful()
        {
            //Arrange
            var service = SetupService();
            _queService.GetMessagesFromQueAsync<RingbaUOW>(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>())
                .Returns(Task.FromResult(new MessageBatchResult<RingbaUOW>
                {
                    IsSuccessfull = false,
                }));

            //Act
            await service.FetchMessagesAsync();

            //Assert
            Assert.Equal(1, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "LogErrorAsync"));
        }

        [Fact]
        public async Task ShouldReturnBatch()
        {
            //Arrange
            var service = SetupService();

            //Act
            var result = await service.FetchMessagesAsync();

            //Assert
            Assert.Equal(5, result.NumberOfMessages);
            Assert.Equal(5, result.Messages.Count());
            Assert.Equal(true, result.IsSuccessfull);
        }
    }
}
