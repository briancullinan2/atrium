using System.Xml.Linq;

namespace Extensions.AlienVisitors
{


    public class ExpressionMinifier : ExpressionVisitor
    {
        private readonly XElement _root = new("Exp");
        private XElement _current;

        public ExpressionMinifier() => _current = _root;

        public static string Minify(Expression node)
        {
            var visitor = new ExpressionMinifier();
            visitor.Visit(node);
            return visitor._root.ToString(SaveOptions.DisableFormatting);
        }

        public override Expression? Visit(Expression? node)
        {
            if (node == null) return null;

            // Truncate "MemberExpression" to "Mem", "ConstantExpression" to "Con", etc.
            var nodeName = node.GetType().Name[..3];
            var element = new XElement(nodeName);

            var parent = _current;
            _current = element;
            parent.Add(element);

            base.Visit(node);

            _current = parent;
            return node;
        }
    }

}
