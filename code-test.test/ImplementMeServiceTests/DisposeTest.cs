using System.Linq;
using code_test;
using NSubstitute;
using RingbaLibs;
using Xunit;

namespace Tests
{
    public class ImplementMeServiceTest_Dispose
    {
        private static ImplementMeService SetupService()
        {
            var service = Substitute.ForPartsOf<ImplementMeService>(Substitute.For<IKVRepository>(),
                Substitute.For<ILogService>(),
                Substitute.For<IMessageProcessService>(),
                Substitute.For<IMessageQueService>());

            service.When(x => x.Stop()).DoNotCallBase();

            return service;
        }

        [Fact]
        public void ShouldCallStop()
        {
            //Arrange
            var service = SetupService();

            //Act
            service.Dispose();

            //Assert
            Assert.Equal(1, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "Stop"));
        }
    }
}