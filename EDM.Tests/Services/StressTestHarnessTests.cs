using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using EDM.Tools.StressTest;

namespace EDM.Tests.Services
{
    public class StressTestHarnessTests : TestBase
    {
        [Fact]
        [Trait("Category", "Stress")]
        public async Task RunFullStressTestSuite_ExecutesCleanly()
        {
            // Act
            await StressTestProgram.Main(Array.Empty<string>());

            // Assert
            Assert.True(true);
        }
    }
}
