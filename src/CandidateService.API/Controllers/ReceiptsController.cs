using CandidateService.Application.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CandidateService.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReceiptsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ReceiptsController> _logger;
        public ReceiptsController(IMediator mediator, ILogger<ReceiptsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> ProcessReceipt([FromBody] ProcessReceiptCommand command)
        {
            try
            {
                await _mediator.Send(command);
                return NoContent();
            }
            catch (ConflictException)
            {
                return Conflict();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing receipt for operation {OperationId}", command.OperationId);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
