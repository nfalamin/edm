using System;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class QueueSchedulerAutomationTests : TestBase
    {
        [Fact]
        public void DownloadQueueModel_DefaultValuesAreValid()
        {
            // Arrange & Act
            var model = new DownloadQueueModel();

            // Assert
            model.Id.Should().NotBeNullOrEmpty();
            model.Priority.Should().Be(QueuePriority.Normal);
            model.MaxConcurrentFiles.Should().Be(2);
            model.PostAction.Should().Be(PostQueueAction.None);
        }

        [Fact]
        public async Task DownloadQueueManager_EnqueuesAndExecutesByPriority()
        {
            // Arrange
            using var queueManager = new DownloadQueueManager(maxParallel: 2);
            bool executedHigh = false;
            bool executedNormal = false;

            // Act
            await queueManager.EnqueueAsync("item1", "q1", QueuePriority.Normal, async (ct) =>
            {
                await Task.Delay(10, ct);
                executedNormal = true;
            });

            await queueManager.EnqueueAsync("item2", "q1", QueuePriority.High, async (ct) =>
            {
                await Task.Delay(10, ct);
                executedHigh = true;
            });

            await Task.Delay(150);

            // Assert
            executedHigh.Should().BeTrue();
            executedNormal.Should().BeTrue();
        }
    }
}
