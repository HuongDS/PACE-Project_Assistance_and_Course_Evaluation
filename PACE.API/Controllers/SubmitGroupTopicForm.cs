using System.ComponentModel.DataAnnotations;

namespace PACE.API.Controllers;

public class SubmitGroupTopicForm
{
    [Required(ErrorMessage = "Tên đề tài không được để trống.")]
    [MaxLength(255, ErrorMessage = "Tên đề tài tối đa 255 ký tự.")]
    public string Title { get; set; } = null!;

    [MaxLength(4000, ErrorMessage = "Mô tả đề tài tối đa 4000 ký tự.")]
    public string? Description { get; set; }

    [Range(0, int.MaxValue - 1)]
    public int PreviousVersion { get; set; }

    public IFormFile? Proposal { get; set; }
}
