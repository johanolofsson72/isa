using Agent.Services;
using Agent.Services.Interfaces;
using FakeItEasy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Shared.Services;
using Xunit;

namespace Agent.Tests.Services;

/// <summary>
/// Tests for conversation memory and context preservation
/// </summary>
public class ConversationMemoryTests
{
    private readonly ChatExecutionService _chatService;
    private readonly IProductionMemoryService _memoryService;
    private readonly IChatHistoryFactory _chatHistoryFactory;

    public ConversationMemoryTests()
    {
        _memoryService = A.Fake<IProductionMemoryService>();
        _chatHistoryFactory = A.Fake<IChatHistoryFactory>();

        // Create a real Kernel instance since it's sealed and can't be mocked
        var kernelBuilder = Kernel.CreateBuilder();
        var kernel = kernelBuilder.Build();

        var logger = A.Fake<ILogger<ChatExecutionService>>();
        var cacheService = A.Fake<IOpenAICacheService>();
        var auditLogService = A.Fake<IAuditLogService>();
        var memoryExtractor = A.Fake<IIntelligentMemoryExtractor>();
        var onlineSearchService = A.Fake<IOnlineSearchService>();
        var conversationHistoryService = A.Fake<IConversationHistoryService>();
        var environment = A.Fake<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        var httpContextAccessor = A.Fake<Microsoft.AspNetCore.Http.IHttpContextAccessor>();

        // Create a real in-memory database context for testing
        var options = new DbContextOptionsBuilder<Agent.Data.FilesDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var filesDbContext = new Agent.Data.FilesDbContext(options);

        // Create a real SmartUserIdentificationService since it can't be mocked
        var smartUserLogger = A.Fake<ILogger<SmartUserIdentificationService>>();
        var configuration = A.Fake<Microsoft.Extensions.Configuration.IConfiguration>();
        var smartUserIdentificationService = new SmartUserIdentificationService(
            filesDbContext,
            smartUserLogger,
            configuration);

        var tempFilesService = A.Fake<ITempFilesService>();
        var httpClientFactory = A.Fake<IHttpClientFactory>();
        var fileProcessingService = A.Fake<FileProcessingService>();

        _chatService = new ChatExecutionService(
            kernel,
            cacheService,
            auditLogService,
            _memoryService,
            memoryExtractor,
            onlineSearchService,
            conversationHistoryService,
            _chatHistoryFactory,
            smartUserIdentificationService,
            logger,
            environment,
            httpContextAccessor,
            filesDbContext,
            tempFilesService,
            httpClientFactory,
            fileProcessingService);
    }

    [Fact]
    public async Task EnhanceChatWithMemory_Should_Preserve_Existing_System_Prompt()
    {
        // Arrange
        var originalSystemPrompt = "You are Isa, a helpful accounting assistant.";
        var enhancedSystemPrompt = originalSystemPrompt + "\n[SYSTEM - Memory Context]\n- User name is Anna";
        
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(originalSystemPrompt);
        chatHistory.AddUserMessage("What's my name?");

        A.CallTo(() => _memoryService.GenerateSystemPromptAsync(A<string>._, A<string>._))
            .Returns(enhancedSystemPrompt);

        // Act
        await CallEnhanceChatWithMemoryAsync(chatHistory, "test-device");

        // Assert
        A.CallTo(() => _memoryService.GenerateSystemPromptAsync("test-device", originalSystemPrompt))
            .MustHaveHappenedOnceExactly();

        chatHistory.First().Content.Should().Contain("You are Isa");
        chatHistory.First().Content.Should().Contain("User name is Anna");
    }

    [Fact]
    public async Task EnhanceChatWithMemory_Should_Preserve_Conversation_History()
    {
        // Arrange
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage("System prompt");
        chatHistory.AddUserMessage("I need help with Fortnox invoice");
        chatHistory.AddAssistantMessage("I can help with that. What VAT type do you need?");
        chatHistory.AddUserMessage("Normal");

        A.CallTo(() => _memoryService.GenerateSystemPromptAsync(A<string>._, A<string>._))
            .Returns("Enhanced system prompt");

        var originalMessageCount = chatHistory.Count;

        // Act
        await CallEnhanceChatWithMemoryAsync(chatHistory, "test-device");

        // Assert
        chatHistory.Count.Should().Be(originalMessageCount); // Same number of messages
        chatHistory.Skip(1).Should().HaveCount(3); // 3 non-system messages preserved
        chatHistory.Skip(1).First().Content.Should().Contain("Fortnox invoice");
        chatHistory.Skip(1).Last().Content.Should().Contain("Normal");
    }

    [Fact]
    public async Task EnhanceChatWithMemory_Should_Handle_Missing_System_Message()
    {
        // Arrange
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Hello");

        A.CallTo(() => _memoryService.GenerateSystemPromptAsync(A<string>._, A<string>._))
            .Returns("Generated system prompt");

        // Act
        await CallEnhanceChatWithMemoryAsync(chatHistory, "test-device");

        // Assert
        A.CallTo(() => _memoryService.GenerateSystemPromptAsync("test-device", ""))
            .MustHaveHappenedOnceExactly();

        chatHistory.First().Role.Should().Be(AuthorRole.System);
        chatHistory.First().Content.Should().Be("Generated system prompt");
    }

    [Fact]
    public async Task EnhanceChatWithMemory_Should_Not_Lose_Context_On_Enhancement()
    {
        // Arrange - Simulate the Fortnox VAT type conversation from the image
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage("You are Isa, an accounting assistant.");
        chatHistory.AddUserMessage("Det blev ett fel vid skapandet av leverantörsfakturan i Fortnox. 'Ogiltig momstyp'.");
        chatHistory.AddAssistantMessage("Vill du att jag ska använda 'Normal', 'EUInternal' eller 'Reverse' som momstyp?");
        chatHistory.AddUserMessage("Normal");

        var enhancedPrompt = "You are Isa, an accounting assistant.\n[SYSTEM - Memory Context]\n- Working on Fortnox invoice issue";
        A.CallTo(() => _memoryService.GenerateSystemPromptAsync(A<string>._, A<string>._))
            .Returns(enhancedPrompt);

        // Act
        await CallEnhanceChatWithMemoryAsync(chatHistory, "test-device");

        // Assert - The conversation context should be preserved
        var conversationMessages = chatHistory.Where(m => m.Role != AuthorRole.System).ToList();
        conversationMessages.Should().HaveCount(3);
        conversationMessages[0].Content.Should().Contain("leverantörsfakturan");
        conversationMessages[1].Content.Should().Contain("momstyp");
        conversationMessages[2].Content.Should().Contain("Normal");

        // The system prompt should be enhanced but conversation preserved
        chatHistory.First().Content.Should().Contain("Isa");
        chatHistory.First().Content.Should().Contain("Memory Context");
    }

    // Helper method to access the private EnhanceChatWithMemoryAsync method
    private async Task CallEnhanceChatWithMemoryAsync(ChatHistory chatHistory, string deviceFingerprint)
    {
        var method = typeof(ChatExecutionService).GetMethod("EnhanceChatWithMemoryAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (method != null)
        {
            await (Task)method.Invoke(_chatService, new object[] { chatHistory, deviceFingerprint })!;
        }
    }
}
