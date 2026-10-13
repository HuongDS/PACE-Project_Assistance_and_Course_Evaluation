using TreeSitter;

namespace PACE.BLL.Analysis.Parsing;

internal static class TreeSitterExtensions
{
    public static Node? Field(this Node node, string name) => node.GetChildForField(name);

    public static string? FieldText(this Node node, string name) => node.GetChildForField(name)?.Text;

    public static int Line(this Node node) => node.StartPosition.Row + 1;

    public static IEnumerable<Node> Descendants(this Node node, Func<Node, bool>? descendInto = null)
    {
        var stack = new Stack<Node>();
        for (var i = node.NamedChildren.Count - 1; i >= 0; i--) stack.Push(node.NamedChildren[i]);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            if (descendInto is not null && !descendInto(current)) continue;

            var children = current.NamedChildren;
            for (var i = children.Count - 1; i >= 0; i--) stack.Push(children[i]);
        }
    }

    public static bool IsWithin(this Node node, Node? container)
        => container is not null && node.StartIndex >= container.StartIndex && node.EndIndex <= container.EndIndex;
}
