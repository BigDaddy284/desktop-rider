using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using DesktopRider.AI.Ollama;
using DesktopRider.Core.Configuration;

namespace DesktopRider.AI.Tests;

/// <summary>Settings held in memory, with a switch to make saving fail.</summary>
internal sealed class FakeSettingsService : ISettingsService
{
    public event EventHandler? Changed;

    public AppSettings Current { get; } = new();

    public SettingsLoadStatus LoadStatus => SettingsLoadStatus.Defaults;

    public bool FailSaves { get; set; }

    public int SaveCount { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdateAsync(Action<AppSettings> change, CancellationToken cancellationToken = default)
    {
        if (FailSaves)
        {
            return Task.FromException(new IOException("The disk is full."));
        }

        change(Current);
        SaveCount++;
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}

/// <summary>Stands in for the network: every request is answered by the supplied function.</summary>
internal sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public List<(HttpMethod Method, Uri Uri, string? Body)> Requests { get; } = [];

    public static StubHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") }));

    public static StubHandler Lines(params string[] lines) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Concat(lines.Select(line => line + "\n")), Encoding.UTF8, "application/x-ndjson"),
        }));

    public static StubHandler Throws(Exception exception) => new((_, _) => Task.FromException<HttpResponseMessage>(exception));

    /// <summary>Accepts the request and then never answers.</summary>
    public static StubHandler Silent() => new(async (_, token) =>
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
        throw new InvalidOperationException("unreachable");
    });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri!, body));
        return await respond(request, cancellationToken);
    }
}

internal static class Ollama
{
    public const string Address = "http://localhost:11434";

    public static OllamaProvider Provider(
        HttpMessageHandler handler,
        string address = Address,
        int responseTimeoutMs = 5000,
        int discoveryTimeoutMs = 5000) =>
        new(
            new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan },
            () => new OllamaConnection(new Uri(address), TimeSpan.FromMilliseconds(responseTimeoutMs), TimeSpan.FromMilliseconds(discoveryTimeoutMs)));

    public static string Piece(string content) =>
        $$"""{"model":"m","message":{"role":"assistant","content":{{System.Text.Json.JsonSerializer.Serialize(content)}}},"done":false}""";

    public const string Done = """{"model":"m","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","total_duration":1}""";

    public static ChatRequest Request(string model = "test-model", double? temperature = null) =>
        new(model, [new ChatMessage(ChatRole.User, "Hello")], temperature);

    public static async Task<List<string>> CollectAsync(this IAsyncEnumerable<string> pieces)
    {
        var all = new List<string>();
        await foreach (var piece in pieces)
        {
            all.Add(piece);
        }

        return all;
    }
}

/// <summary>A provider whose answers are scripted by the test.</summary>
internal sealed class FakeProvider(string name = AiSettings.OllamaProvider) : IAiProvider
{
    public string Name { get; } = name;

    public List<AiModelInfo> Models { get; } = [];

    public AiProviderException? Problem { get; set; }

    public List<string> Reply { get; } = [];

    /// <summary>When set, the reply pauses after its first piece until this completes or the request is cancelled.</summary>
    public TaskCompletionSource? Gate { get; set; }

    public TaskCompletionSource FirstPieceSent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<ChatRequest> Requests { get; } = [];

    public int ListCalls { get; private set; }

    public Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        ListCalls++;
        cancellationToken.ThrowIfCancellationRequested();
        return Problem is null ? Task.FromResult<IReadOnlyList<AiModelInfo>>(Models) : Task.FromException<IReadOnlyList<AiModelInfo>>(Problem);
    }

    public async IAsyncEnumerable<string> StreamChatAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (Problem is not null)
        {
            throw Problem;
        }

        var first = true;
        foreach (var piece in Reply)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return piece;
            if (first && Gate is not null)
            {
                FirstPieceSent.TrySetResult();
                await Gate.Task.WaitAsync(cancellationToken);
            }

            first = false;
        }
    }
}

/// <summary>A conversation store held in memory, with a switch to make saving fail.</summary>
internal sealed class FakeConversationStore : IConversationStore
{
    public List<SavedLine> Saved { get; set; } = [];

    public bool FailSaves { get; set; }

    public int SaveCount { get; private set; }

    public int DeleteCount { get; private set; }

    public int LoadCount { get; private set; }

    public Task<IReadOnlyList<SavedLine>> LoadAsync(CancellationToken cancellationToken = default)
    {
        LoadCount++;
        return Task.FromResult<IReadOnlyList<SavedLine>>([.. Saved]);
    }

    public Task SaveAsync(IReadOnlyList<SavedLine> lines, CancellationToken cancellationToken = default)
    {
        if (FailSaves)
        {
            return Task.FromException(new IOException("The disk is full."));
        }

        SaveCount++;
        Saved = [.. lines];
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        DeleteCount++;
        Saved = [];
        return Task.CompletedTask;
    }
}

/// <summary>A clock the test moves by hand. Timers made from it go off as the time passes them.</summary>
internal sealed class ManualClock(DateTimeOffset now) : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];

    public ManualClock()
        : this(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero))
    {
    }

    public DateTimeOffset Now { get; private set; } = now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <summary>How many timers are waiting to go off.</summary>
    public int Waiting => _timers.Count(timer => timer.Due is not null);

    public override DateTimeOffset GetUtcNow() => Now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        _timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Moves the time on, setting off each timer that falls due on the way, in order.</summary>
    public void Advance(TimeSpan by)
    {
        var target = Now + by;
        while (_timers.Where(timer => timer.Due <= target).OrderBy(timer => timer.Due).FirstOrDefault() is { } next)
        {
            Now = next.Due!.Value > Now ? next.Due.Value : Now;
            next.Fire();
        }

        Now = target;
    }

    private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Due = dueTime == Timeout.InfiniteTimeSpan ? null : clock.Now + dueTime;
            return true;
        }

        public void Fire()
        {
            Due = null;
            callback(state);
        }

        public void Dispose()
        {
            Due = null;
            clock._timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>A memory store held in memory, with a switch to make saving fail.</summary>
internal sealed class FakeMemoryStore : IMemoryStore
{
    public List<MemoryNote> Saved { get; set; } = [];

    public bool FailSaves { get; set; }

    public int DeleteCount { get; private set; }

    public Task<IReadOnlyList<MemoryNote>> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MemoryNote>>([.. Saved]);

    public Task SaveAsync(IReadOnlyList<MemoryNote> notes, CancellationToken cancellationToken = default)
    {
        if (FailSaves)
        {
            return Task.FromException(new IOException("The disk is full."));
        }

        Saved = [.. notes];
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (FailSaves)
        {
            return Task.FromException(new IOException("The disk is full."));
        }

        DeleteCount++;
        Saved = [];
        return Task.CompletedTask;
    }
}
