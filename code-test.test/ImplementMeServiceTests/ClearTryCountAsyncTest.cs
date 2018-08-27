using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
    public class ImplementMeServiceTest_ClearTryCountAsync
    {
        private readonly IKVRepository _repository;

        public ImplementMeServiceTest_ClearTryCountAsync()
        {
            var repository = Substitute.For<IKVRepository>();

            repository.DeleteAsync(Arg.Any<string>())
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
        public async Task ShouldDeleteTryCountForEveryGivenId()
        {
            //Arrange
            var service = SetupService();
            var sampleIds = new[] {"1", "2", "3"};

            //Act
            await service.ClearTryCountAsync(sampleIds, CancellationToken.None);

            //Assert
            Assert.Equal(3, _repository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "DeleteAsync"));
            Assert.True(_repository.ReceivedCalls().Where(c => c.GetMethodInfo().Name == "DeleteAsync")
                .All(c => sampleIds.Any(i => ((string)c.GetArguments()[0]).EndsWith($":{i}"))));
            Assert.Equal(3,
                _repository.ReceivedCalls().Where(c => c.GetMethodInfo().Name == "DeleteAsync")
                    .Select(c => (string) c.GetArguments()[0]).Distinct().Count());
        }

        [Fact]
        public async Task ShouldReturnDeleteResults()
        {
            //Arrange
            var service = SetupService();
            var sampleIds = new[] { "1", "2", "3" };

            //Act
            var result = await service.ClearTryCountAsync(sampleIds, CancellationToken.None);

            //Assert
            Assert.Equal(3, result.Count());
        }
    }
}