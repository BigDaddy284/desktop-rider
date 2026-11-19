#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DesktopRider.Core.Infrastructure;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public sealed class LocalJsonFileTests
    {
        private TempDirectory _folder = null!;

        private string FilePath => Path.Combine(_folder.Path, "notes.json");

        // NUnit uses one object for every test in the class, so each test is given a fresh folder here.
        [SetUp]
        public void SetUp() => _folder = new TempDirectory();

        [TearDown]
        public void TearDown() => _folder.Dispose();

        [Test]
        public async Task Read_WithNoFile_SaysMissing()
        {
            using var file = new LocalJsonFile<Note>(FilePath);

            var read = await file.ReadAsync();

            Assert.Equal(LocalFileStatus.Missing, read.Status);
            Assert.Null(read.Value);
        }

        [Test]
        public async Task Write_ThenRead_GivesTheSameValue_AndLeavesNoTemporaryFile()
        {
            using var file = new LocalJsonFile<Note>(FilePath);

            await file.WriteAsync(new Note { Words = "Check the chain", Kind = NoteKind.Important });
            var read = await file.ReadAsync();

            Assert.Equal(LocalFileStatus.Loaded, read.Status);
            Assert.Equal("Check the chain", read.Value!.Words);
            Assert.Equal(NoteKind.Important, read.Value.Kind);
            Assert.Equal(new[] { "notes.json" }, Directory.GetFiles(_folder.Path).Select(Path.GetFileName));
            Assert.Contains("\"kind\": \"important\"", await File.ReadAllTextAsync(FilePath), StringComparison.Ordinal);
        }

        [Test]
        public async Task Write_CreatesTheFolderIfNeeded()
        {
            var nested = Path.Combine(_folder.Path, "new", "notes.json");
            using var file = new LocalJsonFile<Note>(nested);

            await file.WriteAsync(new Note { Words = "Hello" });

            Assert.True(File.Exists(nested));
        }
        [TestCase("{ not json")]
        [TestCase("null")]
        [TestCase("")]
        public async Task Read_WithADamagedFile_SaysDamaged_AndSetsItAside(string content)
        {
            await File.WriteAllTextAsync(FilePath, content);
            using var file = new LocalJsonFile<Note>(FilePath, new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)));

            var read = await file.ReadAsync();

            Assert.Equal(LocalFileStatus.Damaged, read.Status);
            Assert.Null(read.Value);
            Assert.NotNull(read.Problem);
            Assert.Equal(Path.Combine(_folder.Path, "notes.damaged-20260102-030405.json"), read.BackupFile);
            Assert.Equal(content, await File.ReadAllTextAsync(read.BackupFile!));
            Assert.False(File.Exists(FilePath));
        }

        [Test]
        public async Task Delete_RemovesTheFile_AndIsHarmlessWhenThereIsNothingThere()
        {
            using var file = new LocalJsonFile<Note>(FilePath);
            using var nowhere = new LocalJsonFile<Note>(Path.Combine(_folder.Path, "missing-folder", "notes.json"));

            await file.DeleteAsync();
            await nowhere.DeleteAsync();
            await file.WriteAsync(new Note { Words = "Hello" });
            await file.DeleteAsync();

            Assert.False(File.Exists(FilePath));
        }

        [Test]
        public async Task ManyWritesAtOnce_LeaveOneWholeFile()
        {
            using var file = new LocalJsonFile<Note>(FilePath);

            await Task.WhenAll(Enumerable.Range(0, 20).Select(n => file.WriteAsync(new Note { Words = $"note {n}" })));
            var read = await file.ReadAsync();

            Assert.Equal(LocalFileStatus.Loaded, read.Status);
            Assert.StartsWith("note ", read.Value!.Words, StringComparison.Ordinal);
        }

        private enum NoteKind
        {
            Ordinary,
            Important,
        }

        private sealed class Note
        {
            public string Words { get; set; } = string.Empty;

            public NoteKind Kind { get; set; }
        }
    }
}
