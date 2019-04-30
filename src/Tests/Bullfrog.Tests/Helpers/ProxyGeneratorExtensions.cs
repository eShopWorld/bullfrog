using System;
using System.Threading.Tasks;
using Castle.DynamicProxy;

namespace Helpers
{
    public static class ProxyGeneratorExtensions
    {
        public static TInterface CreateExceptionTransformationInterfaceProxy<TInterface>(this ProxyGenerator generator, TInterface target)
        {
            return (TInterface)generator.CreateInterfaceProxyWithTarget(typeof(TInterface), target, new CallInterceptor());
        }

        public class CallInterceptor : IInterceptor
        {
            public void Intercept(IInvocation invocation)
            {
                var returnType = invocation.Method.ReturnType;

                if (!typeof(Task).IsAssignableFrom(returnType))
                    throw new InvalidOperationException($"The method {invocation.Method.Name} has invalid return type.");

                var taskReturnType = returnType.IsGenericType
                    ? returnType.GetGenericArguments()[0]
                    : typeof(object);
                var tcsType = typeof(TaskCompletionSource<>).MakeGenericType(taskReturnType);
                var tcs = Activator.CreateInstance(tcsType);

                try
                {
                    invocation.Proceed();
                }
                catch (Exception ex)
                {
                    tcsType.GetMethod("SetException", new[] { typeof(Exception) })
                        .Invoke(tcs, new object[] { new AggregateException(ex) });
                    invocation.ReturnValue = tcsType.GetProperty("Task").GetValue(tcs);
                    return;
                }

                var originalTask = (Task)invocation.ReturnValue;
                invocation.ReturnValue = tcsType.GetProperty("Task").GetValue(tcs);
                originalTask.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        tcsType.GetMethod("SetException", new[] { typeof(Exception) })
                            .Invoke(tcs, new object[] { t.Exception });
                    }
                    else
                    {
                        var returnValue = returnType.GetProperty("Result")?.GetValue(t);
                        tcsType.GetMethod("SetResult", new[] { taskReturnType })
                            .Invoke(tcs, new object[] { returnValue });
                    }
                });
            }
        }
    }
}
