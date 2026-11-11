using Weaver.Application.Auth;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>
/// Comments that mention an account by handle, and which of those mentions are still pending:
/// waiting for the mentioned account to reply on the same item (ticket 51).
/// </summary>
[TestFixture]
public class CommentMentionTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private FakeCallerContext _caller = null!;
    private CommentService _comments = null!;
    private MentionService _mentions = null!;
    private WorkItemService _workItems = null!;
    private TestSpace _space = null!;
    private User _riley = null!;
    private User _research = null!;
    private User _llama = null!;
    private Guid _itemId;

    [SetUp]
    public async Task SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _caller = FakeCallerContext.Anyone();
        _comments = new CommentService(_db, _clock, _caller);
        _mentions = new MentionService(_db, _caller);
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), new FixedServerCalendar(), FakeCallerContext.Anyone());
        _space = TestSpaces.Add(_db);

        _riley = TestUsers.Add(_db, "riley");
        _research = AddAgent("Claude research", "claude-research");
        _llama = AddAgent("Llama", "llama");
        _itemId = (await _workItems.CreateAsync("Kitchen quotes", null, null, _space.ToDo, spaceId: _space.Id)).Id;
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private User AddAgent(string name, string handle)
    {
        var agent = User.CreateAgent(name, null);
        agent.SetHandle(handle);
        _db.Users.Add(agent);
        _db.SaveChanges();
        return agent;
    }

    /// <summary>Each call happens a minute after the last, so "after" is never a tie.</summary>
    private T As<T>(User user, Func<T> act)
    {
        _clock.Advance(TimeSpan.FromMinutes(1));
        _caller.Current = new Caller(user.Id, null, null);
        return act();
    }

    private Task<CommentView> Comment(User author, string body, Guid? itemId = null) =>
        As(author, () => _comments.CreateAsync(itemId ?? _itemId, body));

    private Task<IReadOnlyList<PendingMention>> PendingFor(User user) =>
        As(user, () => _mentions.ListPendingAsync());

    [Test]
    public async Task AMention_IsPendingForTheMentionedAgent_WithItsCommentAuthorAndItem()
    {
        var comment = await Comment(_riley, "@claude-research find three quotes for this");

        var pending = await PendingFor(_research);

        Assert.That(pending, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(pending[0].Comment.Id, Is.EqualTo(comment.Id));
            Assert.That(pending[0].Comment.Body, Is.EqualTo("@claude-research find three quotes for this"));
            Assert.That(pending[0].Comment.AuthorUsername, Is.EqualTo("riley"));
            Assert.That(pending[0].Comment.WorkItemId, Is.EqualTo(_itemId));
            Assert.That(pending[0].WorkItem.Title, Is.EqualTo("Kitchen quotes"));
        });
    }

    [Test]
    public async Task AMention_IsOnlyPendingForTheAccountItNames()
    {
        await Comment(_riley, "@claude-research find three quotes");

        Assert.Multiple(async () =>
        {
            Assert.That(await PendingFor(_llama), Is.Empty);
            Assert.That(await PendingFor(_riley), Is.Empty);
        });
    }

    [Test]
    public async Task AnUnknownHandle_MentionsNobody()
    {
        await Comment(_riley, "@nobody-here and @riley, have a look");

        Assert.That(_db.CommentMentions, Is.Empty);
    }

    [Test]
    public async Task ACommentMentioningSeveralAgents_IsPendingForEach()
    {
        await Comment(_riley, "@claude-research and @llama: compare notes");

        Assert.Multiple(async () =>
        {
            Assert.That(await PendingFor(_research), Has.Count.EqualTo(1));
            Assert.That(await PendingFor(_llama), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task TheAgentReplyingOnTheItem_ClearsTheMention()
    {
        await Comment(_riley, "@claude-research find three quotes");

        await Comment(_research, "Here are three quotes.");

        Assert.That(await PendingFor(_research), Is.Empty);
    }

    [Test]
    public async Task AReplyFromAnotherAccount_OrOnAnotherItem_DoesntClearTheMention()
    {
        var otherItem = await _workItems.CreateAsync("Bathroom", null, null, _space.ToDo, spaceId: _space.Id);
        await Comment(_riley, "@claude-research find three quotes");

        await Comment(_llama, "I'm not the one asked.");
        await Comment(_riley, "Any news?");
        await Comment(_research, "Wrong item.", otherItem.Id);

        Assert.That(await PendingFor(_research), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task AnAgentCommentFromBeforeTheMention_DoesntClearIt()
    {
        await Comment(_research, "An earlier note.");

        await Comment(_riley, "@claude-research find three quotes");

        Assert.That(await PendingFor(_research), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task EditingTheMentioningCommentAfterAReply_DoesntMakeItPendingAgain()
    {
        var request = await Comment(_riley, "@claude-research find three quotes");
        await Comment(_research, "Here are three quotes.");

        await As(_riley, () => _comments.UpdateAsync(request.Id, "@claude-research find three quotes, please"));

        Assert.That(await PendingFor(_research), Is.Empty);
    }

    [Test]
    public async Task EditingInANewMention_MakesItPending()
    {
        var request = await Comment(_riley, "Need three quotes");
        await Comment(_research, "An unrelated note.");

        await As(_riley, () => _comments.UpdateAsync(request.Id, "@claude-research need three quotes"));

        Assert.That(await PendingFor(_research), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task EditingAMentionOut_RemovesIt()
    {
        var request = await Comment(_riley, "@claude-research and @llama: find three quotes");

        await As(_riley, () => _comments.UpdateAsync(request.Id, "@llama: find three quotes"));

        Assert.Multiple(async () =>
        {
            Assert.That(await PendingFor(_research), Is.Empty);
            Assert.That(await PendingFor(_llama), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task DeletingTheMentioningComment_RemovesItsMentions()
    {
        var request = await Comment(_riley, "@claude-research find three quotes");

        await As(_riley, () => _comments.DeleteAsync(request.Id));

        Assert.That(await PendingFor(_research), Is.Empty);
    }

    [Test]
    public async Task AMentionWrittenByAnAgent_IsRecordedButNeverPending()
    {
        await Comment(_llama, "@claude-research can you check my numbers?");

        Assert.Multiple(async () =>
        {
            Assert.That(_db.CommentMentions.Count(), Is.EqualTo(1));
            Assert.That(await PendingFor(_research), Is.Empty);
        });
    }

    [Test]
    public async Task PendingMentions_AreListedOldestFirst()
    {
        var first = await Comment(_riley, "@claude-research first");
        var otherItem = await _workItems.CreateAsync("Bathroom", null, null, _space.ToDo, spaceId: _space.Id);
        var second = await Comment(_riley, "@claude-research second", otherItem.Id);

        var pending = await PendingFor(_research);

        Assert.That(pending.Select(p => p.Comment.Id), Is.EqualTo(new[] { first.Id, second.Id }));
    }

    [Test]
    public async Task AMentionOnATrashedItem_IsntPending()
    {
        await Comment(_riley, "@claude-research find three quotes");

        await new WorkItemTrashService(_db, new IterativeWorkItemHierarchy(_db), _caller, _clock).MoveToTrashAsync(_itemId);

        Assert.That(await PendingFor(_research), Is.Empty);
    }
}
