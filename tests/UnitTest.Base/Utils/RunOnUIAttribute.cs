using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;
using System;
using System.Threading;
using UnitTest.Base;

namespace UnitTest.Base.Utils
{
    [System.AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class RunOnUIAttribute : Attribute, IWrapTestMethod
    {
        public TestCommand Wrap(TestCommand command) => new RunOnUICommand(command);

        class RunOnUICommand : DelegatingTestCommand
        {
            public RunOnUICommand(TestCommand innerCommand)
                : base(innerCommand)
            {
            }

            public override TestResult Execute(TestExecutionContext context)
            {
                // Marshal the test body onto the headless session's UI thread.
                var resultTask = UnitTestBase.Session.Dispatch(
                    () => RunTest(context), CancellationToken.None);
                resultTask.Wait();

                if (resultTask.Result is Exception ex)
                    throw ex;

                return (TestResult)resultTask.Result;
            }

            private object RunTest(TestExecutionContext context)
            {
                try
                {
                    return innerCommand.Execute(context);
                }
                catch (Exception e)
                {
                    return e;
                }
            }
        }
    }
}
