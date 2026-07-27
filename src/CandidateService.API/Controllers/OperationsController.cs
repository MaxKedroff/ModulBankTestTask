using CandidateService.Application.Commands;
using CandidateService.Application.DTOs;
using CandidateService.Application.Exceptions;
using CandidateService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CandidateService.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OperationsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<OperationsController> _logger;

        public OperationsController(IMediator mediator, ILogger<OperationsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOperation([FromBody] CreateOperationRequest request)
        {
            try
            {
                var command = new CreateOperationCommand
                {
                    OperationId = request.OperationId,
                    Amount = decimal.Parse(request.Amount),
                    Currency = request.Currency,
                    Description = request.Description
                };

                var result = await _mediator.Send(command);
                return CreatedAtAction(nameof(GetOperation), new { id = result.OperationId }, result);
            }
            catch (ConflictException)
            {
                return Conflict();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating operation");
                return StatusCode(500, "Internal server error");
            }

        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetOperation(string id)
        {
            try
            {
                var query = new GetOperationQuery { OperationId = id };
                var result = await _mediator.Send(query);
                return Ok(result);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting operation {OperationId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/submit")]
        public async Task<IActionResult> SubmitOperation(string id)
        {
            try
            {
                var command = new SubmitOperationCommand { OperationId = id };
                var result = await _mediator.Send(command);

                if (result.IsNew)
                {
                    return Accepted(result.Operation);
                }

                return Ok(result.Operation);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting operation {OperationId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}/events")]
        public async Task<IActionResult> GetEvents(string id)
        {
            try
            {
                var query = new GetEventsQuery { OperationId = id };
                var result = await _mediator.Send(query);
                return Ok(result);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting events for operation {OperationId}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
