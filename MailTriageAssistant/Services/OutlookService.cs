using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace MailTriageAssistant.Services;

public sealed class OutlookService : IOutlookService, IDisposable
{
    private static readonly TimeSpan ComTimeout = TimeSpan.FromSeconds(30);
    private const int MaxFetchCount = 50;
    private const int MaxBodyLength = 1500;
    private const int RestrictDays = 7;

    private readonly ILogger<OutlookService> _logger;
    private readonly Thread _comThread;
    private readonly Dispatcher _comDispatcher;
    private readonly SemaphoreSlim _comLock = new(initialCount: 1, maxCount: 1);
    private bool _disposed;
    private bool _newOutlookChecked;

    private Outlook.Application? _app;
    private Outlook.NameSpace? _session;

    public OutlookService()
        : this(NullLogger<OutlookService>.Instance)
    {
    }

    public OutlookService(ILogger<OutlookService> logger)
    {
        _logger = logger ?? NullLogger<OutlookService>.Instance;
        var tcs = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);

        _comThread = new Thread(() =>
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                tcs.SetResult(dispatcher);
                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "Outlook COM (STA)",
        };
        _comThread.SetApartmentState(ApartmentState.STA);
        _comThread.Start();

        _comDispatcher = tcs.Task.GetAwaiter().GetResult();
    }

    public Task<List<RawEmailHeader>> FetchInboxHeaders(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return InvokeAsync(FetchInboxHeadersInternal, ct);
    }

    public Task<string> GetBody(string entryId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return InvokeAsync(() => GetBodyInternal(entryId), ct);
    }

    public Task OpenItem(string entryId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return InvokeAsync(() => OpenItemInternal(entryId), ct);
    }

    public Task CreateDraft(string to, string subject, string body, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return InvokeAsync(() => CreateDraftInternal(to, subject, body), ct);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (!_comDispatcher.HasShutdownStarted && !_comDispatcher.HasShutdownFinished)
            {
                _comDispatcher.Invoke(() =>
                {
                    try
                    {
                        ResetConnection();
                    }
                    catch
                    {
                        // Ignore disposal errors.
                    }
                });

                _comDispatcher.InvokeShutdown();
            }
        }
        catch
        {
            // Ignore shutdown failures.
        }

        try
        {
            _comThread.Join(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Ignore thread join failures.
        }
    }

    private async Task<T> InvokeAsync<T>(Func<T> func, CancellationToken ct)
    {
        await _comLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var op = _comDispatcher.InvokeAsync(func);
            var task = op.Task;
            var timeoutOrCancel = Task.Delay(ComTimeout, ct);
            var completed = await Task.WhenAny(task, timeoutOrCancel).ConfigureAwait(false);
            if (!ReferenceEquals(completed, task))
            {
                try
                {
                    op.Abort();
                }
                catch
                {
                    // Ignore abort failures.
                }

                ct.ThrowIfCancellationRequested();
                throw new TimeoutException("Outlook COM 작업이 30초 내에 완료되지 않았습니다.");
            }

            return await task.ConfigureAwait(false);
        }
        finally
        {
            _comLock.Release();
        }
    }

    private async Task InvokeAsync(Action action, CancellationToken ct)
    {
        await _comLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var op = _comDispatcher.InvokeAsync(action);
            var task = op.Task;
            var timeoutOrCancel = Task.Delay(ComTimeout, ct);
            var completed = await Task.WhenAny(task, timeoutOrCancel).ConfigureAwait(false);
            if (!ReferenceEquals(completed, task))
            {
                try
                {
                    op.Abort();
                }
                catch
                {
                    // Ignore abort failures.
                }

                ct.ThrowIfCancellationRequested();
                throw new TimeoutException("Outlook COM 작업이 30초 내에 완료되지 않았습니다.");
            }

            await task.ConfigureAwait(false);
        }
        finally
        {
            _comLock.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(OutlookService));
        }
    }

    private void EnsureClassicOutlookOrThrow()
    {
        if (!_newOutlookChecked)
        {
            _newOutlookChecked = true;
            if (Process.GetProcessesByName("olk").Any())
            {
                _logger.LogWarning("New Outlook detected (olk.exe). Blocking COM interop.");
                throw new NotSupportedException(
                    "Classic Outlook이 필요합니다. New Outlook(olk.exe)은 COM Interop을 지원하지 않습니다.");
            }
        }

        if (_app is not null && _session is not null)
        {
            return;
        }

        try
        {
            _app = GetActiveOutlookApplication();
            _session = _app.Session;
        }
        catch (COMException ex)
        {
            _logger.LogWarning("Outlook connect failed: {ExceptionType} (HResult={HResult}).", ex.GetType().Name, ex.HResult);
            _app = null;
            _session = null;
            throw new InvalidOperationException(
                "Outlook이 실행 중이지 않습니다. Classic Outlook을 먼저 실행해 주세요.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Outlook connect failed: {ExceptionType}.", ex.GetType().Name);
            _app = null;
            _session = null;
            throw new InvalidOperationException(
                "Outlook 연결에 실패했습니다. Classic Outlook을 확인해 주세요.");
        }
    }

    private static Outlook.Application GetActiveOutlookApplication()
    {
        var hr = CLSIDFromProgID("Outlook.Application", out var clsid);
        if (hr != 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        hr = GetActiveObject(ref clsid, IntPtr.Zero, out var punk);
        if (hr != 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        try
        {
            return (Outlook.Application)Marshal.GetObjectForIUnknown(punk);
        }
        finally
        {
            Marshal.Release(punk);
        }
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(string lpszProgID, out Guid pclsid);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(ref Guid rclsid, IntPtr pvReserved, out IntPtr ppunk);

    private List<RawEmailHeader> FetchInboxHeadersInternal()
    {
        EnsureClassicOutlookOrThrow();

        Outlook.MAPIFolder? inbox = null;
        Outlook.Items? items = null;
        Outlook.Items? filteredItems = null;
        var filteredItemsIsSeparate = false;
        object? raw = null;

        var sw = Stopwatch.StartNew();

        try
        {
            inbox = _session!.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderInbox);
            items = inbox.Items;

            filteredItems = TryRestrictRecentItems(items, days: RestrictDays, out filteredItemsIsSeparate);

            var result = new List<RawEmailHeader>(capacity: MaxFetchCount);
            raw = filteredItems.GetFirst();

            while (raw is not null && result.Count < MaxFetchCount)
            {
                var current = raw;
                raw = null;

                try
                {
                    if (current is Outlook.MailItem mail)
                    {
                        Outlook.Attachments? attachments = null;
                        bool hasAttachments;
                        try
                        {
                            attachments = mail.Attachments;
                            hasAttachments = attachments is not null && attachments.Count > 0;
                        }
                        finally
                        {
                            SafeReleaseComObject(attachments);
                        }

                        result.Add(new RawEmailHeader
                        {
                            EntryId = mail.EntryID ?? string.Empty,
                            SenderName = mail.SenderName ?? string.Empty,
                            SenderEmail = mail.SenderEmailAddress ?? string.Empty,
                            Subject = mail.Subject ?? string.Empty,
                            ReceivedTime = mail.ReceivedTime,
                            HasAttachments = hasAttachments,
                        });
                    }
                }
                catch
                {
                    // Partial failure: skip a problematic item and continue enumerating the rest.
                }
                finally
                {
                    SafeReleaseComObject(current);
                }

                raw = filteredItems.GetNext();
            }

            result.Sort((a, b) => b.ReceivedTime.CompareTo(a.ReceivedTime));
            sw.Stop();
            _logger.LogInformation("Fetched inbox headers: {Count} in {ElapsedMs}ms.", result.Count, sw.ElapsedMilliseconds);
            return result;
        }
        catch (COMException ex)
        {
            _logger.LogWarning("FetchInboxHeaders failed: {ExceptionType} (HResult={HResult}).", ex.GetType().Name, ex.HResult);
            ResetConnection();
            throw new InvalidOperationException(
                "Outlook과 통신할 수 없습니다. Classic Outlook이 실행 중인지 확인해 주세요.");
        }
        catch (NotSupportedException)
        {
            _logger.LogWarning("FetchInboxHeaders blocked: Outlook not supported.");
            throw;
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning("FetchInboxHeaders failed: Outlook not available.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("FetchInboxHeaders failed: {ExceptionType}.", ex.GetType().Name);
            ResetConnection();
            throw new InvalidOperationException("메일 헤더를 불러오는 중 오류가 발생했습니다.");
        }
        finally
        {
#if DEBUG
            sw.Stop();
            MailTriageAssistant.Helpers.PerfEventSource.Log.Measure("FetchInboxHeadersInternal", sw.ElapsedMilliseconds);
#endif
            SafeReleaseComObject(raw);
            if (filteredItemsIsSeparate)
            {
                SafeReleaseComObject(filteredItems);
            }
            SafeReleaseComObject(items);
            SafeReleaseComObject(inbox);
        }
    }

    private static Outlook.Items TryRestrictRecentItems(Outlook.Items items, int days, out bool isSeparateComObject)
    {
        isSeparateComObject = false;

        var since = DateTime.Now.AddDays(-Math.Abs(days));
        var filter = BuildReceivedTimeFilter(since);

        try
        {
            var restricted = items.Restrict(filter);
            isSeparateComObject = true;
            return restricted;
        }
        catch (COMException)
        {
            // If Restrict fails (e.g. locale/date parsing), fall back to the full collection.
            return items;
        }
    }

    private static string BuildReceivedTimeFilter(DateTime since)
    {
        // Outlook Restrict() date parsing is locale-sensitive; "en-US" is broadly supported.
        var formatted = since.ToString("g", CultureInfo.GetCultureInfo("en-US"));
        return $"[ReceivedTime] >= '{formatted}'";
    }

    private string GetBodyInternal(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return string.Empty;
        }

        EnsureClassicOutlookOrThrow();

        object? raw = null;
        var sw = Stopwatch.StartNew();
        try
        {
            raw = _session!.GetItemFromID(entryId);
            if (raw is not Outlook.MailItem mail)
            {
                return string.Empty;
            }

            var body = mail.Body ?? string.Empty;
            if (body.Length > MaxBodyLength)
            {
                body = body[..MaxBodyLength];
            }

            sw.Stop();
            _logger.LogDebug("Body fetched: {EntryId} ({Length}c) in {ElapsedMs}ms.", entryId, body.Length, sw.ElapsedMilliseconds);
            return body;
        }
        catch (COMException ex)
        {
            _logger.LogWarning("GetBody failed: {ExceptionType} (HResult={HResult}).", ex.GetType().Name, ex.HResult);
            ResetConnection();
            throw new InvalidOperationException(
                "Outlook 본문을 가져오지 못했습니다. Classic Outlook 상태를 확인해 주세요.");
        }
        catch (NotSupportedException)
        {
            _logger.LogWarning("GetBody blocked: Outlook not supported.");
            throw;
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning("GetBody failed: Outlook not available.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("GetBody failed: {ExceptionType}.", ex.GetType().Name);
            ResetConnection();
            throw new InvalidOperationException("Outlook 본문을 가져오는 중 오류가 발생했습니다.");
        }
        finally
        {
#if DEBUG
            sw.Stop();
            MailTriageAssistant.Helpers.PerfEventSource.Log.Measure("GetBodyInternal", sw.ElapsedMilliseconds);
#endif
            SafeReleaseComObject(raw);
        }
    }

    private void OpenItemInternal(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return;
        }

        EnsureClassicOutlookOrThrow();

        object? raw = null;
        try
        {
            raw = _session!.GetItemFromID(entryId);
            if (raw is Outlook.MailItem mail)
            {
                mail.Display(false);
            }
        }
        catch (COMException)
        {
            ResetConnection();
            throw new InvalidOperationException(
                "Outlook 항목을 열 수 없습니다. Classic Outlook 상태를 확인해 주세요.");
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            ResetConnection();
            throw new InvalidOperationException("Outlook 항목을 여는 중 오류가 발생했습니다.");
        }
        finally
        {
            SafeReleaseComObject(raw);
        }
    }

    private void CreateDraftInternal(string to, string subject, string body)
    {
        EnsureClassicOutlookOrThrow();

        Outlook.MailItem? draft = null;
        try
        {
            draft = (Outlook.MailItem)_app!.CreateItem(Outlook.OlItemType.olMailItem);
            draft.To = to ?? string.Empty;
            draft.Subject = subject ?? string.Empty;
            draft.Body = body ?? string.Empty;
            draft.Display(false);
        }
        catch (COMException)
        {
            ResetConnection();
            throw new InvalidOperationException(
                "Outlook 초안을 만들 수 없습니다. Classic Outlook 상태를 확인해 주세요.");
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            ResetConnection();
            throw new InvalidOperationException("Outlook 초안 생성 중 오류가 발생했습니다.");
        }
        finally
        {
            SafeReleaseComObject(draft);
        }
    }

    private void ResetConnection()
    {
        SafeReleaseComObject(_session);
        SafeReleaseComObject(_app);
        _session = null;
        _app = null;
        _newOutlookChecked = false;
    }

    private static void SafeReleaseComObject(object? obj)
    {
        if (obj is null)
        {
            return;
        }

        try
        {
            if (Marshal.IsComObject(obj))
            {
                Marshal.FinalReleaseComObject(obj);
            }
        }
        catch
        {
            // Ignore COM release issues.
        }
    }
}
