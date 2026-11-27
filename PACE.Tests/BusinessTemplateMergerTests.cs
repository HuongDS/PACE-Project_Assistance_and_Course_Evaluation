using PACE.Application.Analysis.Template;
using Xunit;

namespace PACE.Tests;

public class BusinessTemplateMergerTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();

    private static ApiTemplate Api(string controller, string function, string verb = "POST")
        => new() { Controller = controller, Function = function, HttpMethod = verb, Path = $"/api/{function.ToLowerInvariant()}" };

    private static List<BusinessTemplate> Current() =>
    [
        new() { BusinessId = OrderId, BusinessName = "Đặt món", Description = "Nhóm tự viết", Apis = [Api("OrderController", "Create")] },
        new() { BusinessId = PaymentId, BusinessName = "Thanh toán", Apis = [Api("PaymentController", "Pay")] }
    ];

    [Fact]
    public void Keeps_existing_businesses_and_their_apis_untouched()
    {
        var generated = new List<BusinessTemplate>
        {
            // AI gom API của nhóm sang nghiệp vụ khác tên: không được kéo API khỏi nghiệp vụ cũ.
            new() { BusinessName = "Quản lý đơn hàng", Description = "AI viết", Apis = [Api("OrderController", "Create"), Api("PaymentController", "Pay")] }
        };

        var result = BusinessTemplateMerger.Merge(Current(), generated);

        Assert.Equal(2, result.Templates.Count);
        Assert.Equal("Nhóm tự viết", result.Templates[0].Description);
        Assert.Single(result.Templates[0].Apis);
        Assert.Single(result.Templates[1].Apis);
        Assert.Empty(result.ChangedBusinessIds);
        Assert.Equal(0, result.AddedApiCount);
        Assert.Equal(0, result.AddedBusinessCount);
    }

    [Fact]
    public void Adds_new_api_to_business_with_same_name()
    {
        var generated = new List<BusinessTemplate>
        {
            new() { BusinessName = "  đặt MÓN ", Apis = [Api("OrderController", "Create"), Api("OrderController", "Cancel", "DELETE")] }
        };

        var result = BusinessTemplateMerger.Merge(Current(), generated);

        Assert.Equal(2, result.Templates.Count);
        var order = result.Templates.Single(t => t.BusinessId == OrderId);
        Assert.Equal(new[] { "Create", "Cancel" }, order.Apis.Select(a => a.Function));
        Assert.Equal("Nhóm tự viết", order.Description);
        Assert.Equal(new[] { OrderId }, result.ChangedBusinessIds);
        Assert.Equal(1, result.AddedApiCount);
    }

    [Fact]
    public void Creates_new_business_only_for_apis_not_assigned_anywhere()
    {
        var generated = new List<BusinessTemplate>
        {
            new() { BusinessName = "Đánh giá món", Description = "Khách chấm điểm", Apis = [Api("ReviewController", "Rate"), Api("PaymentController", "Pay")] }
        };

        var result = BusinessTemplateMerger.Merge(Current(), generated);

        Assert.Equal(3, result.Templates.Count);
        var review = result.Templates[2];
        Assert.Null(review.BusinessId);
        Assert.Equal("Đánh giá món", review.BusinessName);
        Assert.Equal("Rate", Assert.Single(review.Apis).Function);
        Assert.Equal(1, result.AddedBusinessCount);
        Assert.Empty(result.ChangedBusinessIds);
    }

    [Fact]
    public void Same_new_api_in_two_generated_businesses_is_added_once()
    {
        var generated = new List<BusinessTemplate>
        {
            new() { BusinessName = "Báo cáo", Apis = [Api("ReportController", "Export", "GET")] },
            new() { BusinessName = "Thống kê", Apis = [Api("reportcontroller", "export", "GET")] }
        };

        var result = BusinessTemplateMerger.Merge(Current(), generated);

        Assert.Equal(3, result.Templates.Count);
        Assert.Equal(1, result.AddedApiCount);
        Assert.DoesNotContain(result.Templates, t => t.BusinessName == "Thống kê");
    }

    [Fact]
    public void Does_not_mutate_the_input_lists()
    {
        var current = Current();
        BusinessTemplateMerger.Merge(current, [new() { BusinessName = "Đặt món", Apis = [Api("OrderController", "Cancel")] }]);

        Assert.Single(current[0].Apis);
    }
}
