using System.Linq;
using System.Threading.Tasks;
using code_test;
using NSubstitute;
using RingbaLibs;
using Xunit;

namespace Tests
{
    public class ImplementMeServiceTest_DoWork
    {
        [Fact]
        public void ShouldCallDoWorkAsync()
        {
            //Arrange
            var service = Substitute.For<ImplementMeService>(Substitute.For<IKVRepository>(),
                Substitute.For<ILogService>(),
                Substitute.For<IMessageProcessService>(),
                Substitute.For<IMessageQueService>());
            service.DoWorkAsync().Returns(Task.CompletedTask);

            //Act
            service.DoWork();

            //Assert
            Assert.Equal(1, service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "DoWorkAsync"));
        }
    }
}
