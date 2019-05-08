using System;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Moq;
using Xunit;

namespace Helpers
{
    public class ExceptionProxyTests
    {
        [Fact, IsLayer0]
        public async Task NoResultMethodExecution()
        {
            var mock = new Mock<ITestInterface>();
            mock.Setup(x => x.NoResultMethod()).Returns(Task.CompletedTask);
            var proxy = new ProxyGenerator().CreateExceptionTransformationInterfaceProxy(mock.Object);

            await proxy.NoResultMethod();
        }

        [Fact, IsLayer0]
        public async Task WithResultMethodExecution()
        {
            var mock = new Mock<ITestInterface>();
            mock.Setup(x => x.WithResultMethod()).ReturnsAsync(4);
            var proxy = new ProxyGenerator().CreateExceptionTransformationInterfaceProxy(mock.Object);

            var result = await proxy.WithResultMethod();

            result.Should().Be(4);
        }

        [Fact, IsLayer0]
        public void NoResultMethodException()
        {
            var mock = new Mock<ITestInterface>();
            mock.Setup(x => x.NoResultMethod()).Throws(new InvalidOperationException("test"));
            var proxy = new ProxyGenerator().CreateExceptionTransformationInterfaceProxy(mock.Object);

            Func<Task> func = () => proxy.NoResultMethod();

            var ex = func.Should().Throw<AggregateException>()
                .Which;
            ex.InnerException.Should().BeOfType<InvalidOperationException>();
            ex.InnerException.Message.Should().Be("test");
        }

        [Theory, IsLayer0]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SuccessfullCalls(bool delay)
        {
            var obj = new TestDelayedAsync(true, delay);
            var proxy = new ProxyGenerator().CreateExceptionTransformationInterfaceProxy<ITestInterface>(obj);

            await proxy.NoResultMethod();
            var result = await proxy.WithResultMethod();

            result.Should().Be(4);
        }

        [Theory, IsLayer0]
        [InlineData(false)]
        [InlineData(true)]
        public void FailedCalls(bool delay)
        {
            var obj = new TestDelayedAsync(false, delay);
            var proxy = new ProxyGenerator().CreateExceptionTransformationInterfaceProxy<ITestInterface>(obj);

            Task noResult() => proxy.NoResultMethod();
            Task withResult() => proxy.WithResultMethod();

            ValidateException(noResult);
            ValidateException(withResult);

            void ValidateException(Func<Task> func)
            {
                var ex = func.Should().Throw<AggregateException>()
                    .Which;
                ex.InnerException.Should().BeOfType<InvalidOperationException>();
                ex.InnerException.Message.Should().Be("test");
            }
        }

        public interface ITestInterface
        {
            Task NoResultMethod();

            Task<int> WithResultMethod();
        }

        public class TestDelayedAsync : ITestInterface
        {
            private readonly bool _succeed;
            private readonly bool _delay;

            public TestDelayedAsync(bool succeed, bool delay)
            {
                _succeed = succeed;
                _delay = delay;
            }

            public async Task NoResultMethod()
            {
                if (_delay)
                    await Task.Delay(1);
                if (!_succeed)
                    throw new InvalidOperationException("test");
            }

            public async Task<int> WithResultMethod()
            {
                if (_delay)
                    await Task.Delay(2);
                return _succeed ? 4 : throw new InvalidOperationException("test");
            }
        }
    }
}
