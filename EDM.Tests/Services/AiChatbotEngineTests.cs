using System;
using System.Linq;
using System.Threading.Tasks;
using EDM.Services;
using EDM.Services.AI;
using EDM.ViewModels;
using Xunit;

namespace EDM.Tests.Services
{
    [Collection("LocalizationTestCollection")]
    public class AiChatbotEngineTests : IDisposable
    {
        public AiChatbotEngineTests()
        {
            LocalizationService.Instance.SetLanguage("en-US");
        }

        public void Dispose()
        {
            LocalizationService.Instance.SetLanguage("en-US");
        }

        [Fact]
        public async Task OfflineAiChat_InitialPrompt_ReturnsLocalizedGreetingAndQuickPrompts()
        {
            LocalizationService.Instance.SetLanguage("en-US");
            var engine = OfflineAiChatEngine.Instance;
            var resp = await engine.ProcessUserPromptAsync(string.Empty);

            Assert.NotNull(resp);
            Assert.False(string.IsNullOrWhiteSpace(resp.ReplyText));
            Assert.Contains("EDM AI", resp.ReplyText);
            Assert.True(resp.SuggestedFollowUps.Count > 0);
        }

        [Fact]
        public async Task OfflineAiChat_SpeedQuestion_ReturnsDiagnosticAndOptimizationTips()
        {
            LocalizationService.Instance.SetLanguage("en-US");
            var engine = OfflineAiChatEngine.Instance;
            var resp = await engine.ProcessUserPromptAsync("Why is my download speed slow?");

            Assert.NotNull(resp);
            Assert.Contains("Diagnostic", resp.ReplyText);
            Assert.True(resp.IsLiveDiagnosis);
            Assert.False(string.IsNullOrEmpty(resp.ActionCommand));
        }

        [Theory]
        [InlineData("pause all downloads", "ACTION_PAUSE_ALL")]
        [InlineData("resume all downloads", "ACTION_RESUME_ALL")]
        [InlineData("open settings", "ACTION_OPEN_SETTINGS")]
        [InlineData("download youtube 4k video", "ACTION_ADD_URL")]
        public async Task OfflineAiChat_ActionPrompts_RecognizeActionCommands(string userPrompt, string expectedAction)
        {
            LocalizationService.Instance.SetLanguage("en-US");
            var engine = OfflineAiChatEngine.Instance;
            var resp = await engine.ProcessUserPromptAsync(userPrompt);

            Assert.NotNull(resp);
            Assert.Equal(expectedAction, resp.ActionCommand);
            Assert.False(string.IsNullOrWhiteSpace(resp.ActionLabel));
        }

        [Fact]
        public async Task OfflineAiChat_MultiLanguageBangla_GeneratesBengaliResponse()
        {
            var loc = LocalizationService.Instance;
            loc.SetLanguage("bn-BD");

            try
            {
                var engine = OfflineAiChatEngine.Instance;
                var resp = await engine.ProcessUserPromptAsync("আমার ডাউনলোড স্পিড কম কেন?");

                Assert.NotNull(resp);
                Assert.Contains("ডায়াগনস্টিক", resp.ReplyText);
            }
            finally
            {
                loc.SetLanguage("en-US");
            }
        }

        [Fact]
        public async Task OfflineAiChat_MultiLanguageHindi_GeneratesHindiResponse()
        {
            var loc = LocalizationService.Instance;
            loc.SetLanguage("hi-IN");

            try
            {
                var engine = OfflineAiChatEngine.Instance;
                var resp = await engine.ProcessUserPromptAsync("यूट्यूब वीडियो कैसे डाउनलोड करें?");

                Assert.NotNull(resp);
                Assert.Contains("गाइड", resp.ReplyText);
            }
            finally
            {
                loc.SetLanguage("en-US");
            }
        }

        [Fact]
        public void AiChatHistoryService_AddAndClear_MaintainsBoundedList()
        {
            var history = AiChatHistoryService.Instance;
            history.ClearHistory();

            Assert.Empty(history.GetHistory());

            history.AddMessage(new AiChatMessage { Sender = "User", Content = "Test Question" });
            history.AddMessage(new AiChatMessage { Sender = "Assistant", Content = "Test Answer" });

            var items = history.GetHistory();
            Assert.Equal(2, items.Count);
            Assert.Equal("User", items[0].Sender);
            Assert.Equal("Assistant", items[1].Sender);

            history.ClearHistory();
            Assert.Empty(history.GetHistory());
        }
    }
}
