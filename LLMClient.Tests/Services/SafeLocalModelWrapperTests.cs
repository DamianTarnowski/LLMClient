using LLMClient.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using LLMClient.Models;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LLMClient.Tests.Services
{
    [TestFixture]
    public class SafeLocalModelWrapperTests
    {
        private static async IAsyncEnumerable<string> Stream(params string[] chunks)
        {
            foreach (var c in chunks)
            {
                await Task.Yield();
                yield return c;
            }
        }

        [Test]
        public void Propagates_InnerEvents_WhenEnabled()
        {
            var inner = new Mock<ILocalModelService>(MockBehavior.Loose);
            inner.SetupGet(i => i.IsLoaded).Returns(false);
            inner.SetupGet(i => i.IsDownloading).Returns(false);
            var logger = new Mock<ILogger<SafeLocalModelWrapper>>();

            var wrapper = new SafeLocalModelWrapper(logger.Object, inner.Object);

            var states = new List<LocalModelState>();
            var progresses = new List<double>();
            var errors = new List<string>();

            wrapper.StateChanged += s => states.Add(s);
            wrapper.DownloadProgress += p => progresses.Add(p);
            wrapper.ErrorOccurred += e => errors.Add(e);

            // Raise inner events
            inner.Raise(i => i.StateChanged += null!, LocalModelState.Downloading);
            inner.Raise(i => i.DownloadProgress += null!, 42.5);
            inner.Raise(i => i.ErrorOccurred += null!, "inner error");

            Assert.That(states, Does.Contain(LocalModelState.Downloading));
            Assert.That(progresses.Last(), Is.EqualTo(42.5).Within(0.001));
            Assert.That(errors.Last(), Is.EqualTo("Inner service error: inner error"));
        }

        [Test]
        public async Task Disables_After_Three_Consecutive_Failures_And_Blocks_Operations()
        {
            var inner = new Mock<ILocalModelService>(MockBehavior.Strict);
            inner.SetupGet(i => i.IsLoaded).Returns(true);
            inner.SetupGet(i => i.IsDownloading).Returns(false);
            var logger = new Mock<ILogger<SafeLocalModelWrapper>>();

            // Cause exceptions in a safe-wrapped method to trigger failure counting
            inner.Setup(i => i.IsModelDownloadedAsync()).ThrowsAsync(new Exception("boom"));

            var wrapper = new SafeLocalModelWrapper(logger.Object, inner.Object);

            var receivedStates = new List<LocalModelState>();
            var receivedErrors = new List<string>();
            wrapper.StateChanged += s => receivedStates.Add(s);
            wrapper.ErrorOccurred += e => receivedErrors.Add(e);

            // 3 failures to hit the threshold
            Assert.That(await wrapper.IsModelDownloadedAsync(), Is.False);
            Assert.That(await wrapper.IsModelDownloadedAsync(), Is.False);
            Assert.That(await wrapper.IsModelDownloadedAsync(), Is.False);

            // After threshold, wrapper should emit error and move to Error state
            Assert.That(receivedStates.Last(), Is.EqualTo(LocalModelState.Error));
            Assert.That(receivedErrors.Last(), Is.EqualTo("Local model temporarily unavailable. App functionality not affected."));

            // Subsequent operations should be blocked without invoking inner
            inner.Setup(i => i.LoadModelAsync()).ThrowsAsync(new Exception("should not be called"));

            await wrapper.LoadModelAsync(); // Should short-circuit and not call inner
            inner.Verify(i => i.LoadModelAsync(), Times.Never);

            // GenerateResponseAsync should throw when disabled
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await wrapper.GenerateResponseAsync("hello"));
        }

        [Test]
        public async Task Streaming_Returns_No_Chunks_When_NotLoaded_Or_Disabled()
        {
            var inner = new Mock<ILocalModelService>(MockBehavior.Loose);
            var logger = new Mock<ILogger<SafeLocalModelWrapper>>();

            inner.SetupGet(i => i.IsLoaded).Returns(false);
            var wrapper = new SafeLocalModelWrapper(logger.Object, inner.Object);

            var chunks = new List<string>();
            await foreach (var c in wrapper.GenerateStreamingResponseAsync("hi"))
                chunks.Add(c);
            Assert.That(chunks, Is.Empty);

            // Now make it loaded and return a stream
            inner.SetupGet(i => i.IsLoaded).Returns(true);
            inner.Setup(i => i.GenerateStreamingResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .Returns((string _, CancellationToken __) => Stream("A", "B"));

            var chunks2 = new List<string>();
            await foreach (var c in wrapper.GenerateStreamingResponseAsync("hi"))
                chunks2.Add(c);
            Assert.That(chunks2, Is.EquivalentTo(new[] { "A", "B" }));
        }

        [Test]
        public async Task Onboarding_And_Help_Fallback_When_NotLoaded_Or_Error()
        {
            var inner = new Mock<ILocalModelService>(MockBehavior.Loose);
            var logger = new Mock<ILogger<SafeLocalModelWrapper>>();

            inner.SetupGet(i => i.IsLoaded).Returns(false);
            var wrapper = new SafeLocalModelWrapper(logger.Object, inner.Object);

            var onboardingPl = await wrapper.GenerateOnboardingResponseAsync("pl");
            Assert.That(onboardingPl, Does.Contain("Witaj"));

            var helpPl = await wrapper.GenerateHelpResponseAsync("Jak używać pamięci?", "pl");
            Assert.That(helpPl, Does.Contain("Pytanie:"));

            // Now set loaded but make inner throw -> still return fallback and not throw
            inner.SetupGet(i => i.IsLoaded).Returns(true);
            inner.Setup(i => i.GenerateOnboardingResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new Exception("gen fail"));
            var onboardingFallback = await wrapper.GenerateOnboardingResponseAsync("pl");
            Assert.That(onboardingFallback, Does.Contain("Witaj"));

            inner.Setup(i => i.GenerateHelpResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new Exception("help fail"));
            var helpFallback = await wrapper.GenerateHelpResponseAsync("Jak?", "pl");
            Assert.That(helpFallback, Does.Contain("Pytanie:"));
        }

        [Test]
        public async Task FailureCounter_Resets_On_Successful_StateChange()
        {
            var inner = new Mock<ILocalModelService>(MockBehavior.Loose);
            inner.SetupGet(i => i.IsLoaded).Returns(true);
            var logger = new Mock<ILogger<SafeLocalModelWrapper>>();
            var wrapper = new SafeLocalModelWrapper(logger.Object, inner.Object);

            // Cause 2 failures
            inner.Setup(i => i.IsModelDownloadedAsync()).ThrowsAsync(new Exception("fail"));
            Assert.That(await wrapper.IsModelDownloadedAsync(), Is.False);
            Assert.That(await wrapper.IsModelDownloadedAsync(), Is.False);

            // Recovery event should reset failure counter
            inner.Raise(i => i.StateChanged += null!, LocalModelState.Loaded);

            // Another 2 failures should NOT disable yet
            Assert.That(await wrapper.IsModelDownloadedAsync(), Is.False);
            Assert.That(await wrapper.IsModelDownloadedAsync(), Is.False);

            // Third failure now should disable (fresh counter)
            Assert.That(await wrapper.IsModelDownloadedAsync(), Is.False);
        }
    }
}
