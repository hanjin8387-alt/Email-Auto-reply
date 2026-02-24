using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using FluentAssertions;
using MailTriageAssistant.ViewModels;
using Xunit;

namespace MailTriageAssistant.Tests.ViewModels;

public sealed class AsyncRelayCommandTests
{
    [Fact]
    public async Task Execute_WhenTaskSucceeds_DoesNotCallOnException()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var executeCompleted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var onExceptionCalled = false;

            var command = new AsyncRelayCommand(
                execute: () =>
                {
                    executeCompleted.TrySetResult(null);
                    return Task.CompletedTask;
                },
                onException: _ => onExceptionCalled = true);

            command.Execute(null);

            await executeCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
            await WaitForConditionAsync(() => command.CanExecute(null)).ConfigureAwait(true);

            onExceptionCalled.Should().BeFalse();
        });
    }

    [Fact]
    public async Task Execute_WhenTaskThrows_CallsOnException()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var expected = new InvalidOperationException("test");
            var exceptionObserved = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

            var command = new AsyncRelayCommand(
                execute: () => Task.FromException(expected),
                onException: ex => exceptionObserved.TrySetResult(ex));

            command.Execute(null);

            var actual = await exceptionObserved.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
            await WaitForConditionAsync(() => command.CanExecute(null)).ConfigureAwait(true);

            actual.Should().BeSameAs(expected);
        });
    }

    [Fact]
    public async Task Execute_WhenTaskThrows_AndNoCallback_DoesNotCrash()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var expected = new InvalidOperationException("no callback");
            var unhandledObserved = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dispatcher = Dispatcher.CurrentDispatcher;

            void Handler(object? sender, DispatcherUnhandledExceptionEventArgs args)
            {
                unhandledObserved.TrySetResult(args.Exception);
                args.Handled = true;
            }

            dispatcher.UnhandledException += Handler;
            try
            {
                var command = new AsyncRelayCommand(() => Task.FromException(expected));

                command.Execute(null);

                var actual = await unhandledObserved.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
                await WaitForConditionAsync(() => command.CanExecute(null)).ConfigureAwait(true);

                actual.Should().BeSameAs(expected);
            }
            finally
            {
                dispatcher.UnhandledException -= Handler;
            }
        });
    }

    [Fact]
    public async Task CanExecute_ReturnsFalse_WhileRunning()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var started = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            var command = new AsyncRelayCommand(async () =>
            {
                started.TrySetResult(null);
                await release.Task.ConfigureAwait(true);
            });

            command.CanExecute(null).Should().BeTrue();
            command.Execute(null);

            await started.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
            command.CanExecute(null).Should().BeFalse();

            release.TrySetResult(null);
            await WaitForConditionAsync(() => command.CanExecute(null)).ConfigureAwait(true);
        });
    }

    [Fact]
    public async Task CanExecute_ReturnsTrue_AfterCompletion()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var exceptionHandled = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            var command = new AsyncRelayCommand(
                execute: () => Task.FromException(new InvalidOperationException("after completion")),
                onException: _ => exceptionHandled.TrySetResult(null));

            command.Execute(null);

            await exceptionHandled.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
            await WaitForConditionAsync(() => command.CanExecute(null)).ConfigureAwait(true);

            command.CanExecute(null).Should().BeTrue();
        });
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Condition was not met within the expected time.");
            }

            await Task.Delay(10).ConfigureAwait(true);
        }
    }

    private static Task RunOnStaThreadAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

                action().ContinueWith(t =>
                {
                    if (t.IsFaulted && t.Exception is not null)
                    {
                        tcs.TrySetException(t.Exception.InnerExceptions);
                    }
                    else if (t.IsCanceled)
                    {
                        tcs.TrySetCanceled();
                    }
                    else
                    {
                        tcs.TrySetResult(null);
                    }

                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }, TaskScheduler.Default);

                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return tcs.Task;
    }
}
