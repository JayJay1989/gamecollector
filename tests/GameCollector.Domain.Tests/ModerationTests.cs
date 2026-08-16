using GameCollector.Domain.Catalog;
using GameCollector.Domain.Common;

namespace GameCollector.Domain.Tests;

public sealed class ModerationTests
{
    [Fact]
    public void DraftCanBeSubmittedAndApprovedWithRevisionHistory()
    {
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var game = Game.Create(Guid.NewGuid(), "Draft Game", null, null, null, null, null, null, null, null,
            ModerationStatus.Draft, userId, DateTime.UtcNow);

        game.Submit(DateTime.UtcNow);
        game.Approve(adminId, DateTime.UtcNow);

        Assert.Equal(ModerationStatus.Approved, game.ModerationStatus);
        Assert.Equal(adminId, game.ApprovedByUserId);
        Assert.Equal(3, game.Revision);
    }

    [Fact]
    public void NeedsChangesRequiresACommentAndBecomesEditable()
    {
        var game = Game.Create(Guid.NewGuid(), "Pending Game", null, null, null, null, null, null, null, null,
            ModerationStatus.Pending, Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<DomainValidationException>(() => game.RequestChanges(Guid.NewGuid(), " ", DateTime.UtcNow));
        game.RequestChanges(Guid.NewGuid(), "Correct the title.", DateTime.UtcNow);
        game.UpdateSubmission("Corrected Game", null, null, null, null, null, null, null, null, [], [], [], DateTime.UtcNow);

        Assert.Equal(ModerationStatus.NeedsChanges, game.ModerationStatus);
        Assert.Equal("Corrected Game", game.Title);
    }

    [Fact]
    public void ReviewedChangeRequestCannotBeReviewedTwice()
    {
        var request = GameChangeRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "{\"title\":\"New\"}", DateTime.UtcNow);
        request.Approve(Guid.NewGuid(), null, DateTime.UtcNow);

        Assert.Throws<DomainValidationException>(() => request.Reject(Guid.NewGuid(), "No", DateTime.UtcNow));
    }
}
