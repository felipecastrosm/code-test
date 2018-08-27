using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using code_test;
using NSubstitute;
using RingbaLibs;
using RingbaLibs.Models;
using Xunit;

namespace Tests
{
    public class ImplementMeServiceTest_UpdateMessagesAsync
    {
        private readonly IMessageQueService _queService;

        public ImplementMeServiceTest_UpdateMessagesAsync()
        {
            var queService = Substitute.For<IMessageQueService>();

            queService.UpdateMessagesAsync(Arg.Any<IEnumerable<UpdateBatchRequest>>())
                .Returns(Task.FromResult(new ActionResult{IsSuccessfull = true}));

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
        public async Task ShouldUpdateMessages()
        {
            //Arrange
            var service = SetupService();
            var updateBatchRequest = new UpdateBatchRequest();

            //Act
            await service.UpdateMessagesAsync(new ConcurrentBag<UpdateBatchRequest> {updateBatchRequest});

            //Assert
            Assert.Equal(1, _queService.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "UpdateMessagesAsync"));
            Assert.Equal(updateBatchRequest, (_queService.ReceivedCalls()
                .Single(c => c.GetMethodInfo().Name == "UpdateMessagesAsync").GetArguments()
                .Single() as IEnumerable<UpdateBatchRequest>)?.Single());
        }

        [Fact]
        public async Task ShouldLogError_IfUpdateIsUnsuccessful()
        {
            //Arrange
            var service = SetupService();

            _queService.UpdateMessagesAsync(Arg.Any<IEnumerable<UpdateBatchRequest>>())
                .Returns(Task.FromResult(new ActionResult
                {
                    IsSuccessfull = false,
                    ErrorCode = 1,
                    ErrorMessage = "SampleMessage"
                }));

            //Act
            await service.UpdateMessagesAsync(new ConcurrentBag<UpdateBatchRequest> {new UpdateBatchRequest()});

            //Assert
            Assert.Equal(1, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "LogErrorAsync"));
            Assert.Equal(1,
                (int) service.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "LogErrorAsync").GetArguments()[0]);
            Assert.Equal("SampleMessage",
                (string) service.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "LogErrorAsync")
                    .GetArguments()[1]);
        }

        [Fact]
        public async Task ShouldReturnFalse_IfUpdateIsUnsuccessful()
        {
            //Arrange
            var service = SetupService();

            _queService.UpdateMessagesAsync(Arg.Any<IEnumerable<UpdateBatchRequest>>())
                .Returns(Task.FromResult(new ActionResult
                {
                    IsSuccessfull = false,
                    ErrorCode = 1,
                    ErrorMessage = "SampleMessage"
                }));

            //Act
            var result = await service.UpdateMessagesAsync(new ConcurrentBag<UpdateBatchRequest> { new UpdateBatchRequest() });

            //Assert
            Assert.Equal(false, result);
        }

        [Fact]
        public async Task ShouldReturnTrue_IfUpdateIsSuccessful()
        {
            //Arrange
            var service = SetupService();

            //Act
            var result = await service.UpdateMessagesAsync(new ConcurrentBag<UpdateBatchRequest> { new UpdateBatchRequest() });

            //Assert
            Assert.Equal(true, result);
        }
    }
}