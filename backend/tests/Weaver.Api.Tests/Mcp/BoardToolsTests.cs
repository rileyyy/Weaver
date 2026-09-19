using ModelContextProtocol;
using Moq;
using Weaver.Api.Mcp;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class BoardToolsTests
{
    private Mock<IBoardService> _boards = null!;
    private BoardTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _boards = new Mock<IBoardService>(MockBehavior.Strict);
        _tools = new BoardTools(_boards.Object);
    }

    [Test]
    public async Task ListBoards_DelegatesToServiceAndMapsResults()
    {
        var board = new Board { Id = Guid.NewGuid(), Name = "Main" };
        _boards.Setup(b => b.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([board]);

        var result = await _tools.ListBoards(CancellationToken.None);

        Assert.That(result.Single().Id, Is.EqualTo(board.Id));
    }

    [Test]
    public void GetBoard_WhenNotFound_ThrowsMcpException()
    {
        var id = Guid.NewGuid();
        _boards.Setup(b => b.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Board?)null);

        Assert.ThrowsAsync<McpException>(() => _tools.GetBoard(id, CancellationToken.None));
    }

    [Test]
    public void CreateBoard_WhenScopeItemMissing_ThrowsMcpExceptionWithSameMessage()
    {
        var scopeItemId = Guid.NewGuid();
        var domainException = new EntityNotFoundException(nameof(WorkItem), scopeItemId);
        _boards.Setup(b => b.CreateAsync("Board", scopeItemId, It.IsAny<CancellationToken>())).ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            _tools.CreateBoard("Board", scopeItemId, CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }
}
