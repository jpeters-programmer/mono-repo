using System.Linq.Expressions;
using static Graft.Tests.GraftTests;

public class GrafterTests
{
    public void Thing_Should_Thing()
    {
        var g = new Grafter();
    
    }
}

public class MyGrafter : Grafter
{
    public Expression<Func<User, UserStub>> From<User>(Expression<Func<User, object>> expr)
    {
        //TODO: generate code here
        throw new NotImplementedException();
    }
}