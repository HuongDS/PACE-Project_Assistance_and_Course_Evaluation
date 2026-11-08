using System.IO.Compression;
using System.Text;
using PACE.Application.QuestionGeneration;
using PACE.Application.QuestionGeneration.Drawio;
using Xunit;

namespace PACE.Tests;

public class DrawioXmlParserTests
{
    private const string SampleStandardXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<mxfile host=""app.diagrams.net"" modified=""2026-11-08T00:00:00.000Z"" agent=""Mozilla"" version=""24.0.0"">
  <diagram id=""diagram-1"" name=""Quy Trinh Goi Mon"">
    <mxGraphModel dx=""1000"" dy=""1000"" grid=""1"" gridSize=""10"" guides=""1"" tooltips=""1"" connect=""1"" arrows=""1"" fold=""1"" page=""1"" pageScale=""1"" pageWidth=""850"" pageHeight=""1100"">
      <root>
        <mxCell id=""0"" />
        <mxCell id=""1"" parent=""0"" />
        <mxCell id=""node_start"" value=""Bắt đầu"" style=""ellipse;whiteSpace=wrap;html=1;"" vertex=""1"" parent=""1"">
          <mxGeometry x=""100"" y=""100"" width=""80"" height=""40"" as=""geometry"" />
        </mxCell>
        <mxCell id=""node_order"" value=""&lt;div&gt;&lt;b&gt;Gọi món ăn&lt;/b&gt;&lt;br&gt;&lt;/div&gt;"" style=""rounded=1;whiteSpace=wrap;html=1;"" vertex=""1"" parent=""1"">
          <mxGeometry x=""220"" y=""100"" width=""120"" height=""60"" as=""geometry"" />
        </mxCell>
        <mxCell id=""edge_1"" value="""" style=""edgeStyle=orthogonalEdgeStyle;"" edge=""1"" parent=""1"" source=""node_start"" target=""node_order"">
          <mxGeometry relative=""1"" as=""geometry"" />
        </mxCell>
        <mxCell id=""node_gateway"" value=""Còn món trong bếp?"" style=""rhombus;whiteSpace=wrap;html=1;"" vertex=""1"" parent=""1"">
          <mxGeometry x=""380"" y=""90"" width=""100"" height=""80"" as=""geometry"" />
        </mxCell>
        <mxCell id=""edge_2"" value="""" edge=""1"" parent=""1"" source=""node_order"" target=""node_gateway"">
          <mxGeometry relative=""1"" as=""geometry"" />
        </mxCell>
        <mxCell id=""node_pay"" value=""Thanh toán hoá đơn"" style=""rounded=1;whiteSpace=wrap;html=1;"" vertex=""1"" parent=""1"">
          <mxGeometry x=""540"" y=""100"" width=""120"" height=""60"" as=""geometry"" />
        </mxCell>
        <mxCell id=""edge_3"" value=""Còn món"" edge=""1"" parent=""1"" source=""node_gateway"" target=""node_pay"">
          <mxGeometry relative=""1"" as=""geometry"" />
        </mxCell>
        <mxCell id=""node_reject"" value=""Thông báo hết món"" style=""rounded=1;whiteSpace=wrap;html=1;"" vertex=""1"" parent=""1"">
          <mxGeometry x=""380"" y=""220"" width=""120"" height=""60"" as=""geometry"" />
        </mxCell>
        <mxCell id=""edge_4"" value=""Hết món"" edge=""1"" parent=""1"" source=""node_gateway"" target=""node_reject"">
          <mxGeometry relative=""1"" as=""geometry"" />
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>";

    [Fact]
    public void ParsesStandardDrawioXmlSuccessfully()
    {
        var businessId = Guid.NewGuid();
        var diagram = DrawioXmlParser.Parse(SampleStandardXml, businessId);

        Assert.Equal(businessId, diagram.BusinessId);
        Assert.Equal("Quy Trinh Goi Mon", diagram.DiagramName);
        Assert.Equal(5, diagram.Nodes.Count);
        Assert.Equal(4, diagram.Edges.Count);

        // Kiểm tra loại bỏ HTML tags
        var orderNode = diagram.Nodes.FirstOrDefault(n => n.Id == "node_order");
        Assert.NotNull(orderNode);
        Assert.Equal("Gọi món ăn", orderNode.Label);

        // Kiểm tra nhận diện Gateway
        var gatewayNode = diagram.Nodes.FirstOrDefault(n => n.Id == "node_gateway");
        Assert.NotNull(gatewayNode);
        Assert.Equal("DecisionGateway", gatewayNode.ShapeType);

        // Kiểm tra trích xuất paths
        Assert.NotEmpty(diagram.ExecutionPaths);
        Assert.Contains(diagram.ExecutionPaths, p => p.Contains("Bắt đầu") && p.Contains("Gọi món ăn") && p.Contains("Thanh toán hoá đơn"));

        // Kiểm tra text chuẩn hoá
        Assert.Contains("QUY TRÌNH NGHIỆP VỤ CHUẨN", diagram.NormalizedFlowText);
        Assert.Contains("Còn món trong bếp?", diagram.NormalizedFlowText);
        Assert.Contains("Thanh toán hoá đơn", diagram.NormalizedFlowText);
    }

    [Fact]
    public void ParsesCompressedDrawioXmlSuccessfully()
    {
        // Giả lập Draw.io lưu nội dung dưới dạng Deflate raw + Base64
        var innerXml = @"<mxGraphModel><root>
<mxCell id=""0""/>
<mxCell id=""1"" parent=""0""/>
<mxCell id=""A"" value=""Bước 1"" vertex=""1"" parent=""1""/>
<mxCell id=""B"" value=""Bước 2"" vertex=""1"" parent=""1""/>
<mxCell id=""E1"" edge=""1"" source=""A"" target=""B"" parent=""1""/>
</root></mxGraphModel>";

        var urlEscaped = Uri.EscapeDataString(innerXml);
        var bytes = Encoding.UTF8.GetBytes(urlEscaped);
        using var ms = new MemoryStream();
        using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(bytes, 0, bytes.Length);
        }
        var base64 = Convert.ToBase64String(ms.ToArray());

        var compressedXml = $@"<mxfile><diagram name=""Compressed Page"">{base64}</diagram></mxfile>";

        var diagram = DrawioXmlParser.Parse(compressedXml);

        Assert.Equal("Compressed Page", diagram.DiagramName);
        Assert.Equal(2, diagram.Nodes.Count);
        Assert.Single(diagram.Edges);
        Assert.Equal("Bước 1", diagram.Nodes[0].Label);
        Assert.Equal("Bước 2", diagram.Nodes[1].Label);
    }

    [Fact]
    public void QuestionPromptBuilderIncludesGroundTruthSpecification()
    {
        var groundTruth = "Quy trình chuẩn: Khách hàng Gọi món trước khi Thanh toán.";
        var prompt = QuestionPromptBuilder.Build(
            businessName: "Quản lý Nhà hàng",
            codeBuilder: "// f1: Thanh toán, f2: Gọi món",
            contextBuilder: "flowchart TD\n  f1 --> f2",
            numberOfQuestions: 3,
            difficulty: "Khó",
            groundTruthSpecification: groundTruth);

        Assert.Contains("ĐIỂM NEO TRI THỨC NGHIỆP VỤ CHUẨN", prompt.SystemInstruction);
        Assert.Contains("GROUND TRUTH SPECIFICATION", prompt.SystemInstruction);
        Assert.Contains("NGHỊCH ĐẢO LUỒNG", prompt.SystemInstruction);
        Assert.Contains("--- QUY TRÌNH NGHIỆP VỤ CHUẨN ĐÃ CHỐT VỚI GIẢNG VIÊN (GROUND TRUTH SPECIFICATION) ---", prompt.UserPrompt);
        Assert.Contains(groundTruth, prompt.UserPrompt);
    }

    [Fact]
    public async Task FileDrawioSpecificationStorageSavesAndRetrievesSuccessfully()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pace_spec_test_" + Guid.NewGuid());
        try
        {
            var options = Microsoft.Extensions.Options.Options.Create(new QuestionGenerationOptions
            {
                SpecificationStoragePath = tempDir
            });
            var storage = new FileDrawioSpecificationStorage(options, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileDrawioSpecificationStorage>.Instance);

            var businessId = Guid.NewGuid();
            var diagram = new DrawioProcessDiagram
            {
                BusinessId = businessId,
                DiagramName = "Test Spec",
                NormalizedFlowText = "Step 1 -> Step 2",
                Nodes = [new DrawioNode("1", "Step 1")],
                Edges = []
            };

            Assert.False(await storage.HasSpecificationAsync(businessId));

            await storage.SaveSpecificationAsync(businessId, diagram);
            Assert.True(await storage.HasSpecificationAsync(businessId));

            var loaded = await storage.GetSpecificationAsync(businessId);
            Assert.NotNull(loaded);
            Assert.Equal("Test Spec", loaded.DiagramName);
            Assert.Equal("Step 1 -> Step 2", loaded.NormalizedFlowText);

            await storage.DeleteSpecificationAsync(businessId);
            Assert.False(await storage.HasSpecificationAsync(businessId));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}
