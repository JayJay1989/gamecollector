using GameCollector.Domain.Collections;

namespace GameCollector.Domain.Tests;

public sealed class CollectionTests
{
    [Fact]
    public void TransferOwnershipKeepsExactlyOneStructuralOwner()
    {
        var oldOwnerId = Guid.NewGuid();
        var newOwnerId = Guid.NewGuid();
        var collection = Collection.Create(Guid.NewGuid(), "Our Games", oldOwnerId, DateTime.UtcNow);
        collection.AddMember(Guid.NewGuid(), newOwnerId, CollectionRole.Viewer, DateTime.UtcNow);

        collection.TransferOwnership(newOwnerId, previousOwnerLeaves: false, DateTime.UtcNow);

        Assert.Equal(newOwnerId, collection.OwnerUserId);
        Assert.DoesNotContain(collection.Members, member => member.UserId == newOwnerId);
        Assert.Equal(CollectionRole.Editor, collection.GetMemberRole(oldOwnerId));
    }
}
