using Itmas3.Domain.Common;

namespace Itmas3.UnitTests.Domain;

public sealed class EntityTests
{
    private sealed class SampleEntity(Guid id) : Entity<Guid>(id);

    private sealed class OtherEntity(Guid id) : Entity<Guid>(id);

    [Fact]
    public void Entities_with_same_type_and_id_are_equal()
    {
        var id = Guid.NewGuid();

        Assert.Equal(new SampleEntity(id), new SampleEntity(id));
        Assert.True(new SampleEntity(id) == new SampleEntity(id));
    }

    [Fact]
    public void Entities_with_different_type_are_not_equal_even_with_same_id()
    {
        var id = Guid.NewGuid();

        Assert.False(new SampleEntity(id).Equals(new OtherEntity(id)));
    }

    [Fact]
    public void Entities_with_different_id_are_not_equal()
    {
        Assert.True(new SampleEntity(Guid.NewGuid()) != new SampleEntity(Guid.NewGuid()));
    }
}
