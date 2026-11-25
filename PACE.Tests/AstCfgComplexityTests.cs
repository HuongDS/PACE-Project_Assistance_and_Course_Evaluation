using PACE.Domain.Enums;
using PACE.Infrastructure.Analysis.Evidence;
using Xunit;

namespace PACE.Tests;

public class AstCfgComplexityTests
{
    private static readonly Guid Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const int Budget = 2000;

    // Mỗi mẫu có cùng bộ điểm quyết định: if + && + if + || + for + foreach + while + 2 case + catch + ?: (+ ?? với C#/JS) → 12 hoặc 13 với hàm.
    private const string CSharpMethod = """
        public int Compute(int a, List<int> xs, string s)
        {
            if (a > 0 && a < 10) { }
            else if (a == 0 || a == -1) { }
            for (int i = 0; i < a; i++) { }
            foreach (var x in xs) { }
            while (a > 0) a--;
            switch (a) { case 1: break; case 2: break; default: break; }
            try { } catch (Exception) { }
            var t = a > 1 ? 1 : 2;
            var u = s ?? "x";
            return t + u.Length;
        }
        """;

    private const string JavaMethod = """
        public int compute(int a, List<Integer> xs, String s) {
            if (a > 0 && a < 10) { }
            else if (a == 0 || a == -1) { }
            for (int i = 0; i < a; i++) { }
            for (Integer x : xs) { }
            while (a > 0) a--;
            switch (a) { case 1: break; case 2: break; default: break; }
            try { } catch (Exception e) { }
            int t = a > 1 ? 1 : 2;
            return t;
        }
        """;

    private const string JsMethod = """
        async handle(a, xs, s) {
            if (a > 0 && a < 10) { } else if (a === 0 || a === -1) { }
            for (let i = 0; i < a; i++) { }
            for (const x of xs) { }
            while (a > 0) a--;
            switch (a) { case 1: break; case 2: break; default: break; }
            try { } catch (e) { }
            const t = a > 1 ? 1 : 2;
            const u = s ?? 'x';
            return t;
        }
        """;

    // ---- Độ phức tạp ----

    [Fact]
    public void CSharp_Ast_CountsDecisionPoints()
    {
        var result = CSharpStructure.BuildAst(Id, CSharpMethod, Budget, default);

        Assert.Null(result.Error);
        Assert.Equal(13, result.Complexity);
        Assert.Contains(result.Edges, e => e.Relation == "syntax-child");
    }

    [Fact]
    public void Java_Ast_CountsDecisionPoints()
    {
        var result = TreeSitterStructure.BuildAst(Id, JavaMethod, ProgrammingLanguage.Java, "A.java", Budget, default);

        Assert.Null(result.Error);
        Assert.Equal(12, result.Complexity);
        Assert.Equal("method_declaration", result.Nodes[0].Label);
    }

    [Fact]
    public void JavaScript_ClassMethod_ParsesWrappedAndCountsDecisionPoints()
    {
        var result = TreeSitterStructure.BuildAst(Id, JsMethod, ProgrammingLanguage.JavaScript, "svc.js", Budget, default);

        Assert.Null(result.Error);
        Assert.Equal(13, result.Complexity);
        Assert.Equal("method_definition", result.Nodes[0].Label);
    }

    [Theory]
    [InlineData("function f(a) { if (a) { return 1; } return 2; }", ProgrammingLanguage.JavaScript, "a.js", 2)]
    [InlineData("const h = (a) => a ? 1 : 2;", ProgrammingLanguage.JavaScript, "a.js", 2)]
    [InlineData("function f(a: number): number { return a > 0 && a < 5 ? 1 : 2; }", ProgrammingLanguage.TypeScript, "a.ts", 3)]
    [InlineData("const V = (p: { ok: boolean }) => <div>{p.ok ? 'y' : 'n'}</div>;", ProgrammingLanguage.TypeScript, "a.tsx", 2)]
    public void JsTs_TopLevelForms_AreSupported(string code, ProgrammingLanguage language, string file, int expected)
    {
        var result = TreeSitterStructure.BuildAst(Id, code, language, file, Budget, default);

        Assert.Null(result.Error);
        Assert.Equal(expected, result.Complexity);
    }

    [Fact]
    public void StraightLineMethod_HasComplexityOne()
    {
        Assert.Equal(1, CSharpStructure.BuildAst(Id, "int One() { return 1; }", Budget, default).Complexity);
        Assert.Equal(1, TreeSitterStructure.BuildAst(Id, "int one() { return 1; }", ProgrammingLanguage.Java, "A.java", Budget, default).Complexity);
    }

    [Fact]
    public void TruncatedSource_HasNoComplexity_InsteadOfAWrongNumber()
    {
        var truncated = "int M(int a) { if (a > 0) { return 1; } return 2; }\n" + CyclomaticComplexity.TruncationMarker;

        Assert.Null(CSharpStructure.BuildAst(Id, truncated, Budget, default).Complexity);
        Assert.Null(TreeSitterStructure.BuildAst(Id, truncated.Replace("int M", "int m"), ProgrammingLanguage.Java, "A.java", Budget, default).Complexity);
    }

    // ---- CFG của C#: không còn 422 khi hàm dùng kiểu của project ----

    private const string ProjectDependentMethod = """
        public async Task<int> CreateAsync(CreateRequest request, CancellationToken ct)
        {
            var user = await uow.Users.GetByIdAsync(request.UserId, ct);
            if (user == null) throw new NotFoundException("user", request.UserId);
            foreach (var item in request.Items) { await uow.Items.AddAsync(item, ct); }
            return await uow.SaveChangesAsync(ct);
        }
        """;

    [Fact]
    public void Cfg_BuildsForMethodsThatUseProjectTypes_AndReportsSemanticErrors()
    {
        var result = CSharpStructure.BuildCfg(Id, "CreateAsync", ProjectDependentMethod, Budget, default);

        Assert.Null(result.Error);
        Assert.NotEmpty(result.Nodes);
        Assert.Contains(result.Edges, e => e.Relation == "conditional");
        Assert.True(result.SemanticErrors > 0, "uow, CreateRequest và NotFoundException không có trong mã đã lưu");
        Assert.Equal(3, result.Complexity); // 1 + if + foreach
    }

    [Fact]
    public void Cfg_OfSelfContainedMethod_HasNoSemanticErrors()
    {
        var result = CSharpStructure.BuildCfg(Id, "Compute", CSharpMethod, Budget, default);

        Assert.Null(result.Error);
        Assert.Equal(0, result.SemanticErrors);
        Assert.Equal(13, result.Complexity);
        Assert.Contains(result.Nodes, n => n.Label.StartsWith("Compute / Entry"));
    }

    [Fact]
    public void Cfg_StillRejectsSyntaxErrors_TruncatedCode_AndNonMethods()
    {
        Assert.Contains("syntax", CSharpStructure.BuildCfg(Id, "M", "int M(int a) { if (a > 0 { return 1; } }", Budget, default).Error);
        Assert.Contains("truncated", CSharpStructure.BuildCfg(Id, "M", "int M() { return 1; }\n" + CyclomaticComplexity.TruncationMarker, Budget, default).Error);
        Assert.Contains("method declarations", CSharpStructure.BuildCfg(Id, "P", "public int Count { get; set; }", Budget, default).Error);
    }

    // ---- Giới hạn và lỗi ----

    [Fact]
    public void Ast_RejectsFragmentsThatCannotBeParsed()
    {
        Assert.NotNull(CSharpStructure.BuildAst(Id, "int M( {", Budget, default).Error);
        Assert.NotNull(TreeSitterStructure.BuildAst(Id, "public int m( {", ProgrammingLanguage.Java, "A.java", Budget, default).Error);
        Assert.NotNull(TreeSitterStructure.BuildAst(Id, "function f( {", ProgrammingLanguage.JavaScript, "a.js", Budget, default).Error);
    }

    [Fact]
    public void Ast_RespectsTheNodeBudget()
    {
        Assert.Contains("2000", CSharpStructure.BuildAst(Id, CSharpMethod, 10, default).Error);
        Assert.Contains("2000", TreeSitterStructure.BuildAst(Id, JavaMethod, ProgrammingLanguage.Java, "A.java", 10, default).Error);
    }

    [Fact]
    public void TreeSitter_SupportsOnlyJavaJavaScriptTypeScript()
    {
        Assert.True(TreeSitterStructure.Supports(ProgrammingLanguage.Java));
        Assert.True(TreeSitterStructure.Supports(ProgrammingLanguage.JavaScript));
        Assert.True(TreeSitterStructure.Supports(ProgrammingLanguage.TypeScript));
        Assert.False(TreeSitterStructure.Supports(ProgrammingLanguage.Python));
        Assert.False(TreeSitterStructure.Supports(ProgrammingLanguage.Other));
    }
}
