using System.Reflection;
using LLMClient.Models;
using LLMClient.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LLMClient.Tests.Services
{
    [TestFixture]
    public class SwitchableLocalModelServiceTests
    {
        private static async Task InvokePrivateSwitchAsync(SwitchableLocalModelService service, EngineType newEngine)
        {
            var mi = typeof(SwitchableLocalModelService)
                .GetMethod("SwitchEngineAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(mi, Is.Not.Null, "Could not find SwitchEngineAsync via reflection");
            var task = (Task)mi!.Invoke(service, new object[] { newEngine })!;
            await task;
        }

        [Test]
        public async Task EngineSwitch_RewiresEvents_PropagatesState_And_UnloadsOldEngine()
        {
            // Arrange mocks for two engines
            var onnx = new Mock<ILocalModelService>(MockBehavior.Loose);
            var llama = new Mock<ILocalModelService>(MockBehavior.Loose);

            // Initial states/properties
            onnx.SetupGet(m => m.State).Returns(LocalModelState.Loaded);
            onnx.SetupGet(m => m.IsLoaded).Returns(true);
            onnx.Setup(m => m.UnloadModelAsync()).Returns(Task.CompletedTask);
            onnx.Setup(m => m.GetModelInfoAsync()).ReturnsAsync(new LocalModelInfo { ModelId = "onnx" });

            llama.SetupGet(m => m.State).Returns(LocalModelState.Downloaded);
            llama.SetupGet(m => m.IsLoaded).Returns(false);
            llama.Setup(m => m.GetModelInfoAsync()).ReturnsAsync(new LocalModelInfo { ModelId = "llama" });

            var logger = new Mock<ILogger<SwitchableLocalModelService>>();
            var service = new SwitchableLocalModelService(
                logger.Object,
                () => onnx.Object,
                () => llama.Object);

            var receivedStates = new List<LocalModelState>();
            service.StateChanged += s => receivedStates.Add(s);

            // Before switch: raising ONNX event should propagate
            onnx.Raise(m => m.StateChanged += null!, LocalModelState.Loading);
            Assert.That(receivedStates, Does.Contain(LocalModelState.Loading));

            // Act: switch to LLama
            await InvokePrivateSwitchAsync(service, EngineType.LLamaSharp);

            // Assert: immediate propagation of new engine state
            Assert.That(receivedStates.Last(), Is.EqualTo(LocalModelState.Downloaded));

            // Old engine should be unloaded if it was loaded
            onnx.Verify(m => m.UnloadModelAsync(), Times.Once);

            // After switch: ONNX events should no longer propagate
            var countAfterSwitch = receivedStates.Count;
            onnx.Raise(m => m.StateChanged += null!, LocalModelState.NotDownloaded);
            Assert.That(receivedStates.Count, Is.EqualTo(countAfterSwitch), "Old engine event should not propagate after switch");

            // After switch: LLama events should propagate
            llama.Raise(m => m.StateChanged += null!, LocalModelState.Loading);
            Assert.That(receivedStates.Last(), Is.EqualTo(LocalModelState.Loading));

            // Delegation should go to the current engine
            var info = await service.GetModelInfoAsync();
            Assert.That(info.ModelId, Is.EqualTo("llama"));
        }

        [Test]
        public async Task Events_Propagate_From_Active_Engine_Only()
        {
            var onnx = new Mock<ILocalModelService>(MockBehavior.Loose);
            var llama = new Mock<ILocalModelService>(MockBehavior.Loose);

            // Initial states
            onnx.SetupGet(m => m.State).Returns(LocalModelState.Downloaded);
            llama.SetupGet(m => m.State).Returns(LocalModelState.NotDownloaded);

            var logger = new Mock<ILogger<SwitchableLocalModelService>>();
            var service = new SwitchableLocalModelService(
                logger.Object,
                () => onnx.Object,
                () => llama.Object);

            var progresses = new List<double>();
            var errors = new List<string>();
            service.DownloadProgress += p => progresses.Add(p);
            service.ErrorOccurred += e => errors.Add(e);

            // From initial (ONNX) engine
            onnx.Raise(m => m.DownloadProgress += null!, 10.0);
            onnx.Raise(m => m.ErrorOccurred += null!, "onnx err");
            Assert.That(progresses.Last(), Is.EqualTo(10.0));
            Assert.That(errors.Last(), Is.EqualTo("onnx err"));

            // Switch to LLama
            await InvokePrivateSwitchAsync(service, EngineType.LLamaSharp);

            // Old engine should no longer affect events
            var pCount = progresses.Count;
            var eCount = errors.Count;
            onnx.Raise(m => m.DownloadProgress += null!, 50.0);
            onnx.Raise(m => m.ErrorOccurred += null!, "old err");
            Assert.That(progresses.Count, Is.EqualTo(pCount));
            Assert.That(errors.Count, Is.EqualTo(eCount));

            // New engine events should propagate
            llama.Raise(m => m.DownloadProgress += null!, 33.3);
            llama.Raise(m => m.ErrorOccurred += null!, "llama err");
            Assert.That(progresses.Last(), Is.EqualTo(33.3).Within(0.001));
            Assert.That(errors.Last(), Is.EqualTo("llama err"));
        }
    }
}
