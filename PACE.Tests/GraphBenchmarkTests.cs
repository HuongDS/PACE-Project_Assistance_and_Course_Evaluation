using System.Globalization;
using PACE.Application.Analysis.Flow;
using PACE.Application.Analysis.Models;
using PACE.Application.Analysis.Template;
using PACE.Domain.Enums;
using PACE.Infrastructure.Analysis.Parsing;
using Xunit;
using Xunit.Abstractions;

namespace PACE.Tests;

public sealed class GraphBenchmarkTests(ITestOutputHelper output)
{
    private sealed record BenchmarkCase(
        string Name,
        string Source,
        string ExpectedEntry,
        string[] ExpectedCalls,
        ApiTemplate? Endpoint = null,
        SourceFile[]? AdditionalFiles = null);

    [Fact]
    public void ReportCSharpGraphBaseline()
    {
        BenchmarkCase[] cases =
        [
            new("ordinary-call", "class Example { void Entry() => Save(); void Save() {} }",
                "Example.Entry()", ["Example.Entry() -> Example.Save()"]),
            new("renamed-call", "class A { void Entry() => z(); void z() {} }",
                "A.Entry()", ["A.Entry() -> A.z()"]),
            new("overload", "class Example { void Entry() => Save(1); void Save(int x) {} void Save(string x) {} }",
                "Example.Entry()", ["Example.Entry() -> Example.Save(int)"]),
            new("namespace-collision", "class Example { void Entry() => new A.Store().Save(); }",
                "Example.Entry()", ["Example.Entry() -> A.cs:Store.Save()"],
                AdditionalFiles:
                [
                    new("A.cs", "A.cs", "namespace A { class Store { public void Save() {} } }"),
                    new("B.cs", "B.cs", "namespace B { class Store { public void Save() {} } }")
                ]),
            new("repeated-call", "class Example { void Entry() { Save(); Save(); } void Save() {} }",
                "Example.Entry()", ["Example.Entry() -> Example.Save()", "Example.Entry() -> Example.Save()"]),
            new("direct-recursion", "class Example { void Entry(int n) { if (n > 0) Entry(n - 1); } }",
                "Example.Entry(int)", ["Example.Entry(int) -> Example.Entry(int)"]),
            new("short-accessor", "class Example { decimal Entry() => getDiscount(); decimal getDiscount() => 0.15m; }",
                "Example.Entry()", ["Example.Entry() -> Example.getDiscount()"]),
            new("route-vs-name", """
                using System;
                class HttpGetAttribute(string path) : Attribute {}
                class OrdersController
                {
                    [HttpGet("/orders")] public void List() {}
                    [HttpGet("/admin/orders")] public void AdminList() {}
                }
                """, "OrdersController.AdminList()", [],
                new ApiTemplate { Controller = "OrdersController", Function = "List", HttpMethod = "GET", Path = "/admin/orders" })
        ];

        int correctEntries = 0, tp = 0, fp = 0, fn = 0;
        foreach (var item in cases)
        {
            var parsed = new CSharpRoslynParser().Parse(
                [new SourceFile("Fixture.cs", "Fixture.cs", item.Source), .. item.AdditionalFiles ?? []], default);
            Assert.Empty(parsed.Warnings);
            var expectedEntry = Assert.Single(parsed.Methods, method => Label(method) == item.ExpectedEntry);
            var entry = item.Endpoint is null
                ? expectedEntry
                : new EntryPointMatcher(parsed.Methods).Match(item.Endpoint, out _);
            if (item.Endpoint is not null && entry == expectedEntry)
                correctEntries++;

            var graph = entry is null ? null : new FlowBuilder(new CallResolver(parsed.Classes, parsed.Methods)).Build(entry);
            var actual = graph?.Steps.Where(step => step.EdgeType == FlowEdgeType.Call)
                .Select(step => $"{Label(step.From)} -> {Label(step.To)}").ToArray() ?? [];

            // Keep repeated edges so a silently collapsed call is counted as missing.
            var remaining = item.ExpectedCalls.ToList();
            var unexpected = actual.Where(edge => !remaining.Remove(edge)).ToArray();
            var matched = actual.Length - unexpected.Length;
            tp += matched;
            fp += unexpected.Length;
            fn += remaining.Count;
            output.WriteLine($"{item.Name}: entry={entry != null && entry == expectedEntry}, TP={matched}, FP={unexpected.Length}, FN={remaining.Count}");
            if (unexpected.Length > 0) output.WriteLine($"  extra: {string.Join(", ", unexpected)}");
            if (remaining.Count > 0) output.WriteLine($"  missing: {string.Join(", ", remaining)}");
        }

        output.WriteLine($"Endpoint matcher: {correctEntries}/{cases.Count(item => item.Endpoint is not null)}");
        output.WriteLine($"Call edges: TP={tp} FP={fp} FN={fn}");
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Precision={tp / (double)(tp + fp):P1} Recall={tp / (double)(tp + fn):P1} F1={2.0 * tp / (2 * tp + fp + fn):P1}"));
    }

    private static string Label(ParsedMethod method)
    {
        var file = method.FilePath == "Fixture.cs" ? "" : $"{method.FilePath}:";
        return $"{file}{method.ClassName}.{method.MethodName}({string.Join(", ", method.Parameters.Select(parameter => parameter.Type))})";
    }
}
