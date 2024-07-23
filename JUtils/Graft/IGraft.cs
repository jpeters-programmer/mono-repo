using System.Linq.Expressions;

namespace Graft;
public interface IGraft<T>
{
    public abstract static Expression<Func<T, object>> Graft {get;}
}