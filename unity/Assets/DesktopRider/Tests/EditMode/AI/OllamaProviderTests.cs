#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;
using DesktopRider.AI.Ollama;
using NUnit.Framework;

namespace DesktopRider.AI.Tests
{
    public class OllamaProviderTests
    {
        private const string Tags = @"{""models"":[
  {""name"":""zeta:latest"",""model"":""zeta:latest"",""size"":4661224676,""details"":{""family"":""z"",""parameter_size"":""8.0B"",""quantization_level"":""Q4_K_M""}},
  {""name"":""Alpha:7b"",""size"":100},
  {""name"":""  "",""size"":1},
  {""size"":2}
]}";

        [Test]
        public async Task ListModels_ReturnsTheModelsOllamaReports_SortedByName()
        {
            var handler = StubHandler.Json(Tags);
            using var provider = Ollama.Provider(handler);

            var models = await provider.ListModelsAsync();

            Assert.Equal(new[] { "Alpha:7b", "zeta:latest" }, models.Select(model => model.Name));
            Assert.Equal(4661224676, models[1].SizeBytes);
            Assert.Equal("8.0B, Q4_K_M", models[1].Details);
            Assert.Null(models[0].Details);
            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("http://localhost:11434/api/tags", request.Uri.ToString());
        }
        [TestCase("http://192.168.1.20:8080", "http://192.168.1.20:8080/api/tags")]
        [TestCase("http://localhost:11434/", "http://localhost:11434/api/tags")]
        [TestCase("https://example.test/ollama", "https://example.test/ollama/api/tags")]
        public async Task ListModels_UsesTheConfiguredAddress(string address, string expected)
        {
            var handler = StubHandler.Json("{\"models\":[]}");
            using var provider = Ollama.Provider(handler, address);

            await provider.ListModelsAsync();

            Assert.Equal(expected, handler.Requests[0].Uri.ToString());
        }
        [TestCase("{\"models\":[]}")]
        [TestCase("{}")]
        public async Task ListModels_WithNoModelsInstalled_ReturnsAnEmptyList(string json)
        {
            using var provider = Ollama.Provider(StubHandler.Json(json));

            Assert.Empty(await provider.ListModelsAsync());
        }

        [Test]
        public async Task ListModels_WhenOllamaIsNotRunning_ExplainsInPlainWords()
        {
            using var provider = Ollama.Provider(StubHandler.Throws(new HttpRequestException("Connection refused")));

            var problem = await Assert.ThrowsAsync<AiProviderException>(() => provider.ListModelsAsync());

            Assert.Equal(AiErrorKind.NotRunning, problem.Kind);
            Assert.Contains("http://localhost:11434", problem.Message, StringComparison.Ordinal);
            Assert.Contains("Ollama is running", problem.Message, StringComparison.Ordinal);
            Assert.IsType<HttpRequestException>(problem.InnerException);
        }

        [Test]
        public async Task ListModels_WhenNothingIsListeningOnARealPort_ReportsNotRunning()
        {
            using var provider = new OllamaProvider(
                new HttpClient { Timeout = Timeout.InfiniteTimeSpan },
                () => new OllamaConnection(new Uri($"http://127.0.0.1:{FreePort()}"), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));

            var problem = await Assert.ThrowsAsync<AiProviderException>(() => provider.ListModelsAsync());

            Assert.Equal(AiErrorKind.NotRunning, problem.Kind);
        }

        [Test]
        public async Task ListModels_WhenOllamaNeverAnswers_TimesOut()
        {
            using var provider = Ollama.Provider(StubHandler.Silent(), discoveryTimeoutMs: 50);

            var problem = await Assert.ThrowsAsync<AiProviderException>(() => provider.ListModelsAsync());

            Assert.Equal(AiErrorKind.Timeout, problem.Kind);
        }

        [Test]
        public async Task ListModels_WhenCancelledByTheCaller_StopsWithoutReportingAProblem()
        {
            using var provider = Ollama.Provider(StubHandler.Silent());
            using var cancel = new CancellationTokenSource();

            var listing = provider.ListModelsAsync(cancel.Token);
            await cancel.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => listing);
        }

        [Test]
        public async Task ListModels_WhenTheAddressIsNotOllama_SaysSo()
        {
            using var html = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>router login</html>", Encoding.UTF8, "text/html"),
            }));
            using var provider = Ollama.Provider(html);
            using var brokenJson = Ollama.Provider(StubHandler.Json("{not json"));

            Assert.Equal(AiErrorKind.InvalidResponse, (await Assert.ThrowsAsync<AiProviderException>(() => provider.ListModelsAsync())).Kind);
            Assert.Equal(AiErrorKind.InvalidResponse, (await Assert.ThrowsAsync<AiProviderException>(() => brokenJson.ListModelsAsync())).Kind);
        }

        [Test]
        public async Task ListModels_WhenOllamaReportsAnError_PassesItsWordsOn()
        {
            using var provider = Ollama.Provider(StubHandler.Json("{\"error\":\"database is locked\"}", HttpStatusCode.InternalServerError));
            using var wordless = Ollama.Provider(StubHandler.Json("oops", HttpStatusCode.BadGateway));

            var problem = await Assert.ThrowsAsync<AiProviderException>(() => provider.ListModelsAsync());
            var bare = await Assert.ThrowsAsync<AiProviderException>(() => wordless.ListModelsAsync());

            Assert.Equal(AiErrorKind.ProviderError, problem.Kind);
            Assert.Contains("database is locked", problem.Message, StringComparison.Ordinal);
            Assert.Equal(AiErrorKind.ProviderError, bare.Kind);
            Assert.Contains("502", bare.Message, StringComparison.Ordinal);
        }

        [Test]
        public async Task Problems_NeverShowSignInDetailsFromTheAddress()
        {
            using var provider = Ollama.Provider(StubHandler.Throws(new HttpRequestException("refused")), "http://someone:secret@myserver:11434");

            var problem = await Assert.ThrowsAsync<AiProviderException>(() => provider.ListModelsAsync());

            Assert.Contains("http://myserver:11434", problem.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", problem.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("someone", problem.Message, StringComparison.Ordinal);
        }

        [Test]
        public async Task StreamChat_SendsTheConversationInOllamasFormat()
        {
            var handler = StubHandler.Lines(Ollama.Done);
            using var provider = Ollama.Provider(handler);
            var request = new ChatRequest(
                "chosen-model:latest",
                new ChatMessage[] { new(ChatRole.System, "Be friendly."), new(ChatRole.User, "Hi \"rider\""), new(ChatRole.Assistant, "Hello!"), new(ChatRole.User, "Wheelie?") },
                Temperature: 0.4);

            await provider.StreamChatAsync(request).CollectAsync();

            var sent = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, sent.Method);
            Assert.Equal("http://localhost:11434/api/chat", sent.Uri.ToString());
            var root = JObject.Parse(sent.Body!);
            Assert.Equal("chosen-model:latest", (string?)root["model"]);
            Assert.True((bool)root["stream"]!);
            Assert.Equal(0.4, (double)root["options"]!["temperature"]!);
            var messages = root["messages"]!.Select(m => ((string)m["role"]!, (string)m["content"]!)).ToList();
            Assert.Equal(new[] { ("system", "Be friendly."), ("user", "Hi \"rider\""), ("assistant", "Hello!"), ("user", "Wheelie?") }, messages);
        }

        [Test]
        public async Task StreamChat_WithNoTemperature_LeavesTheModelsOwnDefault()
        {
            var handler = StubHandler.Lines(Ollama.Done);
            using var provider = Ollama.Provider(handler);

            await provider.StreamChatAsync(Ollama.Request()).CollectAsync();

            var body = JObject.Parse(handler.Requests[0].Body!);
            Assert.False(body.ContainsKey("options"));
        }

        [Test]
        public async Task StreamChat_ReturnsThePiecesInOrder_AndStopsWhenOllamaSaysDone()
        {
            using var provider = Ollama.Provider(StubHandler.Lines(
                Ollama.Piece("Braaap! "),
                string.Empty,
                Ollama.Piece(string.Empty),
                "{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"thinking\":\"hmm\"},\"done\":false}",
                Ollama.Piece("Hello é ✓"),
                Ollama.Done,
                Ollama.Piece("never shown")));

            var pieces = await provider.StreamChatAsync(Ollama.Request()).CollectAsync();

            Assert.Equal(new[] { "Braaap! ", "Hello é ✓" }, pieces);
        }

        [Test]
        public async Task StreamChat_HandsOverEachPieceAsSoonAsItArrives()
        {
            var pipe = new Pipe();
            using var handler = PipeHandler(pipe);
            using var provider = Ollama.Provider(handler);

            await using var reply = provider.StreamChatAsync(Ollama.Request()).GetAsyncEnumerator();
            await WriteLineAsync(pipe, Ollama.Piece("first"));

            // The reply is still unfinished, yet the first piece is already available.
            Assert.True(await reply.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal("first", reply.Current);

            await WriteLineAsync(pipe, Ollama.Piece("second"));
            Assert.True(await reply.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal("second", reply.Current);

            await WriteLineAsync(pipe, Ollama.Done);
            Assert.False(await reply.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        }

        [Test]
        public async Task StreamChat_WhenTheModelIsNotInstalled_SaysWhichModel()
        {
            using var provider = Ollama.Provider(StubHandler.Json("{\"error\":\"model \\\"gone\\\" not found, try pulling it first\"}", HttpStatusCode.NotFound));

            var problem = await Assert.ThrowsAsync<AiProviderException>(() => provider.StreamChatAsync(Ollama.Request("gone")).CollectAsync());

            Assert.Equal(AiErrorKind.ModelNotFound, problem.Kind);
            Assert.Contains("\"gone\"", problem.Message, StringComparison.Ordinal);
        }

        [Test]
        public async Task StreamChat_WhenOllamaReportsAnErrorPartWay_KeepsWhatArrivedThenReportsIt()
        {
            using var provider = Ollama.Provider(StubHandler.Lines(Ollama.Piece("So "), "{\"error\":\"out of memory\"}"));
            var received = new List<string>();

            var problem = await Assert.ThrowsAsync<AiProviderException>(async () =>
            {
                await foreach (var piece in provider.StreamChatAsync(Ollama.Request()))
                {
                    received.Add(piece);
                }
            });

            Assert.Equal(new[] { "So " }, received);
            Assert.Equal(AiErrorKind.ProviderError, problem.Kind);
            Assert.Contains("out of memory", problem.Message, StringComparison.Ordinal);
        }

        [Test]
        public async Task StreamChat_WhenOllamaRefusesTheRequest_ReportsItsError()
        {
            using var provider = Ollama.Provider(StubHandler.Json("{\"error\":\"invalid options\"}", HttpStatusCode.BadRequest));

            var problem = await Assert.ThrowsAsync<AiProviderException>(() => provider.StreamChatAsync(Ollama.Request()).CollectAsync());

            Assert.Equal(AiErrorKind.ProviderError, problem.Kind);
            Assert.Contains("invalid options", problem.Message, StringComparison.Ordinal);
        }

        [Test]
        public async Task StreamChat_WithALongErrorFromOllama_ShortensIt()
        {
            var longError = new string('x', 2000);
            using var provider = Ollama.Provider(StubHandler.Json("{\"error\":\"" + longError + "\"}", HttpStatusCode.InternalServerError));

            var problem = await Assert.ThrowsAsync<AiProviderException>(() => provider.StreamChatAsync(Ollama.Request()).CollectAsync());

            Assert.True(problem.Message.Length < 300);
        }

        [Test]
        public async Task StreamChat_WhenAPieceCannotBeUnderstood_ReportsAnInvalidResponse()
        {
            using var provider = Ollama.Provider(StubHandler.Lines(Ollama.Piece("ok"), "<html>"));

            var problem = await Assert.ThrowsAsync<AiProviderException>(() => provider.StreamChatAsync(Ollama.Request()).CollectAsync());

            Assert.Equal(AiErrorKind.InvalidResponse, problem.Kind);
        }

        [Test]
        public async Task StreamChat_WhenTheReplyEndsWithoutFinishing_ReportsALostConnection()
        {
            using var provider = Ollama.Provider(StubHandler.Lines(Ollama.Piece("half a rep")));

            var problem = await Assert.ThrowsAsync<AiProviderException>(() => provider.StreamChatAsync(Ollama.Request()).CollectAsync());

            Assert.Equal(AiErrorKind.ConnectionLost, problem.Kind);
        }

        [Test]
        public async Task StreamChat_WhenTheConnectionBreaksPartWay_ReportsALostConnection()
        {
            var pipe = new Pipe();
            using var handler = PipeHandler(pipe);
            using var provider = Ollama.Provider(handler);
            await WriteLineAsync(pipe, Ollama.Piece("start"));
            await pipe.Writer.CompleteAsync(new IOException("Connection reset by peer"));

            var problem = await Assert.ThrowsAsync<AiProviderException>(() => provider.StreamChatAsync(Ollama.Request()).CollectAsync());

            Assert.Equal(AiErrorKind.ConnectionLost, problem.Kind);
        }

        [Test]
        public async Task StreamChat_WhenOllamaIsNotRunning_ReportsNotRunning()
        {
            using var provider = Ollama.Provider(StubHandler.Throws(new HttpRequestException("refused")));

            var problem = await Assert.ThrowsAsync<AiProviderException>(() => provider.StreamChatAsync(Ollama.Request()).CollectAsync());

            Assert.Equal(AiErrorKind.NotRunning, problem.Kind);
        }

        [Test]
        public async Task StreamChat_WhenOllamaNeverStartsAnswering_TimesOut()
        {
            using var provider = Ollama.Provider(StubHandler.Silent(), responseTimeoutMs: 50);

            var problem = await Assert.ThrowsAsync<AiProviderException>(() => provider.StreamChatAsync(Ollama.Request()).CollectAsync());

            Assert.Equal(AiErrorKind.Timeout, problem.Kind);
        }

        [Test]
        public async Task StreamChat_WhenTheReplyGoesSilentPartWay_TimesOutAfterKeepingWhatArrived()
        {
            var pipe = new Pipe();
            using var handler = PipeHandler(pipe);
            using var provider = Ollama.Provider(handler, responseTimeoutMs: 100);
            await WriteLineAsync(pipe, Ollama.Piece("start"));
            var received = new List<string>();

            var problem = await Assert.ThrowsAsync<AiProviderException>(async () =>
            {
                await foreach (var piece in provider.StreamChatAsync(Ollama.Request()))
                {
                    received.Add(piece);
                }
            });

            Assert.Equal(new[] { "start" }, received);
            Assert.Equal(AiErrorKind.Timeout, problem.Kind);
            Assert.Contains("part-way", problem.Message, StringComparison.Ordinal);
        }

        [Test]
        public async Task StreamChat_ALongReplyThatKeepsArriving_IsNotTimedOut()
        {
            // The whole reply takes well over the allowed silence, but no single gap does.
            var pipe = new Pipe();
            using var handler = PipeHandler(pipe);
            using var provider = Ollama.Provider(handler, responseTimeoutMs: 600);
            var writing = Task.Run(async () =>
            {
                for (var i = 0; i < 6; i++)
                {
                    await Task.Delay(200);
                    await WriteLineAsync(pipe, Ollama.Piece($"{i}"));
                }

                await WriteLineAsync(pipe, Ollama.Done);
            });

            var pieces = await provider.StreamChatAsync(Ollama.Request()).CollectAsync();
            await writing;

            Assert.Equal(new[] { "0", "1", "2", "3", "4", "5" }, pieces);
        }

        [Test]
        public async Task StreamChat_WhenStoppedByTheCaller_StopsPromptlyWithoutReportingAProblem()
        {
            var pipe = new Pipe();
            using var handler = PipeHandler(pipe);
            using var provider = Ollama.Provider(handler);
            using var cancel = new CancellationTokenSource();
            await WriteLineAsync(pipe, Ollama.Piece("start"));
            var received = new List<string>();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var piece in provider.StreamChatAsync(Ollama.Request(), cancel.Token))
                {
                    received.Add(piece);
                    await cancel.CancelAsync();
                }
            }).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(new[] { "start" }, received);
        }

        [Test]
        public async Task StreamChat_WhenAlreadyCancelled_SendsNothing()
        {
            var handler = new StubHandler((_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });
            using var provider = Ollama.Provider(handler);
            using var cancel = new CancellationTokenSource();
            await cancel.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.StreamChatAsync(Ollama.Request(), cancel.Token).CollectAsync());
        }

        [Test]
        public async Task StreamChat_RejectsARequestWithNoModel()
        {
            var handler = StubHandler.Lines(Ollama.Done);
            using var provider = Ollama.Provider(handler);

            await Assert.ThrowsAnyAsync<ArgumentException>(() => provider.StreamChatAsync(Ollama.Request(model: " ")).CollectAsync());
            Assert.Empty(handler.Requests);
        }

        [Test]
        public async Task OverARealConnection_ModelsAreListedAndAReplyIsStreamed()
        {
            // A tiny stand-in for Ollama on a real local port, so the whole path through the network is exercised.
            using var listener = new HttpListener();
            var address = $"http://localhost:{FreePort()}/";
            listener.Prefixes.Add(address);
            listener.Start();
            var serving = Task.Run(async () =>
            {
                for (var served = 0; served < 2; served++)
                {
                    var context = await listener.GetContextAsync();
                    var isChat = context.Request.Url!.AbsolutePath == "/api/chat";
                    context.Response.ContentType = isChat ? "application/x-ndjson" : "application/json";
                    var lines = isChat ? new[] { Ollama.Piece("Hi "), Ollama.Piece("there"), Ollama.Done } : new[] { Tags };
                    foreach (var line in lines)
                    {
                        await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"));
                        await context.Response.OutputStream.FlushAsync();
                    }

                    context.Response.Close();
                }
            });
            using var provider = new OllamaProvider(
                new HttpClient { Timeout = Timeout.InfiniteTimeSpan },
                () => new OllamaConnection(new Uri(address), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10)));

            var models = await provider.ListModelsAsync();
            var pieces = await provider.StreamChatAsync(Ollama.Request(models[0].Name)).CollectAsync();
            await serving.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(2, models.Count);
            Assert.Equal("Hi there", string.Concat(pieces));
        }

        [Test]
        public void Name_MatchesTheNameStoredInSettings()
        {
            using var provider = Ollama.Provider(StubHandler.Json("{}"));

            Assert.Equal(Core.Configuration.AiSettings.OllamaProvider, provider.Name);
        }

        private static StubHandler PipeHandler(Pipe pipe) => new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(pipe.Reader.AsStream()),
        }));

        private static async Task WriteLineAsync(Pipe pipe, string line) =>
            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"));

        private static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            try
            {
                return ((IPEndPoint)probe.LocalEndpoint).Port;
            }
            finally
            {
                probe.Stop();
            }
        }
    }
}
