using Microsoft.EntityFrameworkCore;
using Weaver.Application.Services;

namespace Weaver.Infrastructure.Tests;

/// <summary>
/// The SQL the search translates to, read without a database. Whether it matches the right rows
/// is covered against Postgres in <c>Weaver.Api.IntegrationTests</c>.
/// </summary>
[TestFixture]
public class PostgresWorkItemSearchTests
{
    private WeaverDbContext _db = null!;

    [SetUp]
    public void SetUp() =>
        _db = new WeaverDbContext(new DbContextOptionsBuilder<WeaverDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unused")
            .Options);

    [TearDown]
    public void TearDown() => _db.Dispose();

    // Postgres only uses an expression index for a query with the identical expression, so a
    // drift between the two would silently turn every search into a full scan.
    [Test]
    public void TextSearch_UsesTheSameExpressionAsTheTextSearchIndex()
    {
        var sql = new PostgresWorkItemSearch().MatchingText(_db.WorkItems, "hall").ToQueryString();

        Assert.That(
            sql,
            Does.Contain("""to_tsvector('simple', w."Title" || ' ' || COALESCE(w."Description", '') || ' ' || COALESCE(w."When", ''))""")
                .IgnoreCase);
    }

    [Test]
    public void TextWithNoWords_AppliesNoFilter()
    {
        var sql = new PostgresWorkItemSearch().MatchingText(_db.WorkItems, " !? ").ToQueryString();

        Assert.That(sql, Does.Not.Contain("to_tsvector"));
    }

    [Test]
    public void BothTagMatches_Translate()
    {
        var search = new PostgresWorkItemSearch();

        Assert.Multiple(() =>
        {
            Assert.That(() => search.Tagged(_db.WorkItems, ["a", "b"], TagMatch.Any).ToQueryString(), Throws.Nothing);
            Assert.That(() => search.Tagged(_db.WorkItems, ["a", "b"], TagMatch.All).ToQueryString(), Throws.Nothing);
        });
    }
}
