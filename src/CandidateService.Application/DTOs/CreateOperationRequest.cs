using System.ComponentModel.DataAnnotations;

namespace CandidateService.Application.DTOs
{
    public class CreateOperationRequest
    {
        [Required]
        public string OperationId { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^\d+(\.\d{1,2})?$")]
        public string Amount { get; set; } = string.Empty;

        [Required]
        public string Currency { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }
}
