#nullable disable
using System.Collections.Generic;

namespace Ot
{
    /// <summary>
    /// Get 呼び出し1回分の集約コンテキスト。
    /// visited は Field 去重と Obj 去重の2種類。handle は結果の入れ物。
    /// </summary>
    public sealed class GetContext
    {
        public HashSet<IField> VisitedFields = new HashSet<IField>();
        public HashSet<string> VisitedObjs = new HashSet<string>();
        public GetHandle Handle = new GetHandle();
    }
}