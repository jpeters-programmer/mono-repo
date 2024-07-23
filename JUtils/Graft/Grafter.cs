using System.Linq.Expressions;

namespace Graft;

public partial class Grafter
{
    public object From<T>(Expression<Func<T, object>> expr)
    {
        throw new NotImplementedException();
    }
}