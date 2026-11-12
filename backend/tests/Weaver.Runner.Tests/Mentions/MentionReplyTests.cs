using Weaver.Runner.Mentions;

namespace Weaver.Runner.Tests.Mentions;

[TestFixture]
public class MentionReplyTests
{
    [Test]
    public void AReplyWithNothingAdded_IsJustTheModelsReply()
    {
        Assert.That(new MentionReply().Render("Ask about lead times."), Is.EqualTo("Ask about lead times."));
    }

    [Test]
    public void WhatWasAddedAndWhatWasnt_FollowTheReply()
    {
        var reply = new MentionReply();
        reply.Added("sub-item #12 Call fitters");
        reply.NotDone("link https://x.example/", "Weaver refused it");

        Assert.That(reply.Render("Done."), Is.EqualTo(
            "Done.\n\nAdded:\n- sub-item #12 Call fitters\n\nNot done:\n- link https://x.example/: Weaver refused it"));
    }

    [Test]
    public void AnOverlongReply_IsShortenedToFitWeaversCommentLimit_KeepingTheNotes()
    {
        var reply = new MentionReply();
        reply.Added("sub-item #12 Call fitters");

        var body = reply.Render(new string('a', MentionReply.MaxLength * 2));

        Assert.Multiple(() =>
        {
            Assert.That(body, Has.Length.EqualTo(MentionReply.MaxLength));
            Assert.That(body, Does.EndWith("- sub-item #12 Call fitters"));
        });
    }

    [Test]
    public void ManyLongNotes_StillFitWeaversCommentLimit()
    {
        var reply = new MentionReply();
        for (var i = 0; i < 20; i++)
        {
            reply.NotDone($"link https://x.example/{i}", new string('r', 1000));
        }

        Assert.That(reply.Render("Done."), Has.Length.LessThanOrEqualTo(MentionReply.MaxLength));
    }
}
