using Microsoft.EntityFrameworkCore;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure.Configurations;
using Weaver.Infrastructure.Services;

namespace Weaver.Infrastructure.Tests;

[TestFixture]
public class BoardServiceTests
{
    private WeaverDbContext _db = null!;
    private BoardService _boards = null!;
    private WorkItemService _workItems = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<WeaverDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WeaverDbContext(options);
        _db.Database.EnsureCreated();
        _boards = new BoardService(_db);
        _workItems = new WorkItemService(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task CreateAsync_WithoutScopeItem_CreatesTopLevelBoard()
    {
        var board = await _boards.CreateAsync("Main board", null);

        Assert.That(board.Name, Is.EqualTo("Main board"));
        Assert.That(board.ScopeItemId, Is.Null);
    }

    [Test]
    public async Task CreateAsync_WithExistingScopeItem_CreatesScopedBoard()
    {
        var scopeItem = await _workItems.CreateAsync("Project", null, null, StatusConfiguration.ToDoId);

        var board = await _boards.CreateAsync("Project board", scopeItem.Id);

        Assert.That(board.ScopeItemId, Is.EqualTo(scopeItem.Id));
    }

    [Test]
    public void CreateAsync_WithUnknownScopeItem_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _boards.CreateAsync("Board", Guid.NewGuid()));
    }

    [Test]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        Assert.That(await _boards.GetByIdAsync(Guid.NewGuid()), Is.Null);
    }

    [Test]
    public async Task GetAllAsync_ReturnsEveryCreatedBoard()
    {
        await _boards.CreateAsync("A", null);
        await _boards.CreateAsync("B", null);

        var boards = await _boards.GetAllAsync();

        Assert.That(boards, Has.Count.EqualTo(2));
    }
}
