#nullable disable
namespace Ot
{
    public sealed class Query
    {
        public FilterExpr Where { get; private set; }

        private Query() { }

        public Query(FilterExpr where)
        {
            Where = where;
        }

        public static Query All => new Query(null);
    }
}