using Fakeeh.MedicalKit.Api.Http;
using Fakeeh.MedicalKit.Application.Patients;
using Fakeeh.MedicalKit.Application.Readings;
using Fakeeh.MedicalKit.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Fakeeh.MedicalKit.Api.Controllers;

/// <summary>The endpoints the Medical KIT calls. All take their parameters in the JSON body.</summary>
[ApiController]
[Route("api/MedicalKit")]
[Consumes("application/json")]
[Produces("application/json")]
public sealed class MedicalKitController(ISender sender) : ControllerBase
{
    /// <summary>Gets the patient's details from Fakeeh HIS by national ID or Iqama number.</summary>
    [HttpPost("GetPatientInfo")]
    [ProducesResponseType<PatientDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientInfo([FromBody] GetPatientInfoQuery request, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(request, cancellationToken));

    /// <summary>Saves a collection of device readings. Each reading is saved or rejected on its own; see results[].</summary>
    [HttpPost("AddVitals")]
    [ProducesResponseType<AddVitalsResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddVitals([FromBody] AddVitalsCommand request, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(request, cancellationToken));

    /// <summary>Gets the newest readings of each vital type for a patient.</summary>
    [HttpPost("GetLatestVitals")]
    [ProducesResponseType<LatestVitalsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetLatestVitals([FromBody] GetLatestVitalsQuery request, CancellationToken cancellationToken) =>
        ToResponse(await sender.Send(request, cancellationToken));

    private ObjectResult ToResponse<T>(Result<T> result) =>
        result.IsSuccess
            ? Ok(result.Value)
            : StatusCode(ErrorResponse.StatusFor(result.Error), ErrorResponse.From(result.Errors));
}
