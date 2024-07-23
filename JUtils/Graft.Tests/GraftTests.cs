using System.Linq.Expressions;

namespace Graft.Tests;

public class GraftTests
{
    [Fact]
    public void Graft_Should_GenerateType()
    {
        Assert.True(false); //fail
    }

    public partial class UserStub : IGraft<User>
    {
        public static Expression<Func<User, object>> Graft => u => new {u.Id, u.Name, u.Access.IsAdmin};
    }
}

public class User
{
    public Guid Id {get;set;}
    public string Name {get;set;}
    public DateOnly BirthDay {get;set;}
    public Access Access {get;set;}

}

public class Access
{
    public bool IsAdmin {get;set;}
    public DateTime ExpireDate {get;set;}

}