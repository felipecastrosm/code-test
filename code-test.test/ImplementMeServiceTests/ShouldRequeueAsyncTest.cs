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
    public class ImplementMeServiceTest_ShouldRequeueAsync
    {
        private readonly IKVRepository _repository;

        public ImplementMeServiceTest_ShouldRequeueAsync()
        {
            var repository = Substitute.For<IKVRepository>();

            repository.DeleteAsync(Arg.Any<string>())
                .Returns(Task.FromResult(new ActionResult {IsSuccessfull = true}));
            repository.CreateAsync(Arg.Any<CreateKVRequest<MessageWrapper<int>>>())
                .Returns(Task.FromResult(new ActionResult {IsSuccessfull = true}));
            repository.GetAsync<MessageWrapper<int>>(Arg.Any<string>())
                .Returns(Task.FromResult(new Result<MessageWrapper<int>>
                    {IsSuccessfull = true, Item = new MessageWrapper<int>()}));
            repository.UpdateAsync(Arg.Any<string>(), Arg.Any<MessageWrapper<int>>())
                .Returns(Task.FromResult(new ActionResult {IsSuccessfull = true}));

            _repository = repository;
        }

        private ImplementMeService SetupService()
        {
            var service = Substitute.ForPartsOf<ImplementMeService>(_repository,
                Substitute.For<ILogService>(),
                Substitute.For<IMessageProcessService>(),
                Substitute.For<IMessageQueService>());

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
        public async Task ShouldReturnFalse_IfExpired()
        {
            //Arrange
            var service = SetupService();

            //Act
            var result = await service.ShouldRequeueAsync("123", 0, 1, -1);

            //Assert
            Assert.Equal(false, result);
        }

        [Fact]
        public async Task ShouldGetTryCount()
        {
            //Arrange
            var service = SetupService();

            //Act
            await service.ShouldRequeueAsync("123", 0, -1, 1);

            //Assert
            Assert.Equal(1, _repository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "GetAsync"));
        }

        [Fact]
        public async Task ShouldLogError_IfGetTryCountIsUnsuccessful()
        {
            //Arrange
            var service = SetupService();
            _repository.GetAsync<MessageWrapper<int>>(Arg.Any<string>())
                .Returns(Task.FromResult(new Result<MessageWrapper<int>> {IsSuccessfull = false}));

            //Act
            await service.ShouldRequeueAsync("123", 0, -1, 1);

            //Assert
            Assert.Equal(1, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "LogErrorAsync"));
        }

        [Fact]
        public async Task ShouldReturnTrue_IfGetTryCountIsUnsuccessful()
        {
            //Arrange
            var service = SetupService();
            _repository.GetAsync<MessageWrapper<int>>(Arg.Any<string>())
                .Returns(Task.FromResult(new Result<MessageWrapper<int>> {IsSuccessfull = false}));

            //Act
            var result = await service.ShouldRequeueAsync("123", 0, -1, 1);

            //Assert
            Assert.Equal(true, result);
        }

        [Fact]
        public async Task ShouldCreateTryCountEntry_IfFirstTry()
        {
            //Arrange
            var service = SetupService();
            _repository.GetAsync<MessageWrapper<int>>(Arg.Any<string>())
                .Returns(Task.FromResult(new Result<MessageWrapper<int>> {IsSuccessfull = true}));

            //Act
            await service.ShouldRequeueAsync("123", 0, -1, 1);

            //Assert
            Assert.Equal(1, _repository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "CreateAsync"));
            Assert.Equal(1,
                (_repository.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "CreateAsync").GetArguments()[0] as
                    CreateKVRequest<MessageWrapper<int>>)?.Item?.Body);
        }

        [Fact]
        public async Task ShouldLogError_IfCreateTryCountEntryIsUnsuccessful()
        {
            //Arrange
            var service = SetupService();
            _repository.GetAsync<MessageWrapper<int>>(Arg.Any<string>())
                .Returns(Task.FromResult(new Result<MessageWrapper<int>> {IsSuccessfull = true}));
            _repository.CreateAsync(Arg.Any<CreateKVRequest<MessageWrapper<int>>>())
                .Returns(Task.FromResult(new ActionResult {IsSuccessfull = false}));

            //Act
            await service.ShouldRequeueAsync("123", 0, -1, 1);

            //Assert
            Assert.Equal(1, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "LogErrorAsync"));
        }

        [Fact]
        public async Task ShouldUpdateExistingTryCountEntry()
        {
            //Arrange
            var service = SetupService();
            _repository.GetAsync<MessageWrapper<int>>(Arg.Any<string>())
                .Returns(Task.FromResult(new Result<MessageWrapper<int>>
                    {IsSuccessfull = true, Item = new MessageWrapper<int> {Body = 1}}));

            //Act
            await service.ShouldRequeueAsync("123", 0, -1, 1);

            //Assert
            Assert.Equal(1, _repository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "UpdateAsync"));
            Assert.Equal(2,
                (_repository.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "UpdateAsync").GetArguments()[1] as
                    MessageWrapper<int>)?.Body);
        }

        [Fact]
        public async Task ShouldLogError_IfUpdateTryCountEntryIsUnsuccessful()
        {
            //Arrange
            var service = SetupService();
            _repository.GetAsync<MessageWrapper<int>>(Arg.Any<string>())
                .Returns(Task.FromResult(new Result<MessageWrapper<int>>
                    {IsSuccessfull = true, Item = new MessageWrapper<int> {Body = 1}}));
            _repository.UpdateAsync(Arg.Any<string>(), Arg.Any<MessageWrapper<int>>())
                .Returns(Task.FromResult(new ActionResult {IsSuccessfull = false}));

            //Act
            await service.ShouldRequeueAsync("123", 0, -1, 1);

            //Assert
            Assert.Equal(1, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "LogErrorAsync"));
        }

        [Fact]
        public async Task ShouldReturnTrue_IfTryCountIsLessThanMaxTries()
        {
            //Arrange
            var service = SetupService();
            _repository.GetAsync<MessageWrapper<int>>(Arg.Any<string>())
                .Returns(Task.FromResult(new Result<MessageWrapper<int>>
                    {IsSuccessfull = true, Item = new MessageWrapper<int> {Body = 1}}));

            //Act
            var result = await service.ShouldRequeueAsync("123", 0, -1, 3);

            //Assert
            Assert.Equal(true, result);
        }

        [Fact]
        public async Task ShouldReturnFalse_IfTryCountIsEqualToMaxTries()
        {
            //Arrange
            var service = SetupService();
            _repository.GetAsync<MessageWrapper<int>>(Arg.Any<string>())
                .Returns(Task.FromResult(new Result<MessageWrapper<int>>
                    { IsSuccessfull = true, Item = new MessageWrapper<int> { Body = 1 } }));

            //Act
            var result = await service.ShouldRequeueAsync("123", 0, -1, 2);

            //Assert
            Assert.Equal(false, result);
        }

        [Fact]
        public async Task ShouldReturnFalse_IfTryCountIsGreaterThanMaxTries()
        {
            //Arrange
            var service = SetupService();
            _repository.GetAsync<MessageWrapper<int>>(Arg.Any<string>())
                .Returns(Task.FromResult(new Result<MessageWrapper<int>>
                    { IsSuccessfull = true, Item = new MessageWrapper<int> { Body = 1 } }));

            //Act
            var result = await service.ShouldRequeueAsync("123", 0, -1, 1);

            //Assert
            Assert.Equal(false, result);
        }

        [Fact]
        public async Task ShouldReturnTrue_IfMaxAgeAndMaxTriesAreUnlimited()
        {
            //Arrange
            var service = SetupService();

            //Act
            var result = await service.ShouldRequeueAsync("123", 0, -1, -1);

            //Assert
            Assert.Equal(true, result);
        }
    }
}