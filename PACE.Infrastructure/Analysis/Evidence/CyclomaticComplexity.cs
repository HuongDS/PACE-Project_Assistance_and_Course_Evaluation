using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TreeSitter;
using PACE.Infrastructure.Analysis.Parsing;

namespace PACE.Infrastructure.Analysis.Evidence;

/// <summary>
/// Độ phức tạp McCabe của một hàm = 1 + số điểm quyết định: if, vòng lặp (for, foreach, while, do), mỗi nhãn case
/// (không tính default), catch, toán tử ba ngôi, &amp;&amp;, || và ??. Đếm cả trong lambda và hàm cục bộ.
/// Tính từ cú pháp nên không cần biên dịch được, và cùng một định nghĩa cho C#, Java, JavaScript, TypeScript.
/// </summary>
public static class CyclomaticComplexity
{
    /// <summary>Chuỗi PACE chèn vào cuối mã khi cắt quá dài (ParserText.Source); mã bị cắt thì không đếm được đủ.</summary>
    public const string TruncationMarker = "/* ... truncated ... */";

    public static bool IsTruncated(string sourceCode) => sourceCode.TrimEnd().EndsWith(TruncationMarker, StringComparison.Ordinal);

    public static int ForCSharp(SyntaxNode root)
    {
        var points = 0;
        foreach (var node in root.DescendantNodesAndSelf())
        {
            switch (node.Kind())
            {
                case SyntaxKind.IfStatement:
                case SyntaxKind.WhileStatement:
                case SyntaxKind.DoStatement:
                case SyntaxKind.ForStatement:
                case SyntaxKind.ForEachStatement:
                case SyntaxKind.ForEachVariableStatement:
                case SyntaxKind.CaseSwitchLabel:
                case SyntaxKind.CasePatternSwitchLabel:
                case SyntaxKind.SwitchExpressionArm:
                case SyntaxKind.ConditionalExpression:
                case SyntaxKind.CatchClause:
                case SyntaxKind.LogicalAndExpression:
                case SyntaxKind.LogicalOrExpression:
                case SyntaxKind.CoalesceExpression:
                case SyntaxKind.CoalesceAssignmentExpression:
                    points++;
                    break;
            }
        }
        return 1 + points;
    }

    /// <summary>Java, JavaScript, TypeScript và TSX bằng cây cú pháp tree-sitter.</summary>
    public static int ForTreeSitter(IEnumerable<Node> roots, bool java)
    {
        var points = 0;
        var stack = new Stack<Node>(roots);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            points += java ? JavaPoints(node) : JsPoints(node);
            var children = node.NamedChildren;
            for (var i = children.Count - 1; i >= 0; i--) stack.Push(children[i]);
        }
        return 1 + points;
    }

    private static int JavaPoints(Node node) => node.Type switch
    {
        "if_statement" or "while_statement" or "do_statement" or "for_statement" or "enhanced_for_statement"
            or "ternary_expression" or "catch_clause" => 1,
        "switch_label" => node.Text.TrimStart().StartsWith("case", StringComparison.Ordinal) ? 1 : 0,
        "binary_expression" => node.FieldText("operator") is "&&" or "||" ? 1 : 0,
        _ => 0
    };

    private static int JsPoints(Node node) => node.Type switch
    {
        "if_statement" or "while_statement" or "do_statement" or "for_statement" or "for_in_statement"
            or "ternary_expression" or "catch_clause" or "switch_case" => 1,
        "binary_expression" => node.FieldText("operator") is "&&" or "||" or "??" ? 1 : 0,
        "augmented_assignment_expression" => node.FieldText("operator") is "&&=" or "||=" or "??=" ? 1 : 0,
        _ => 0
    };
}
