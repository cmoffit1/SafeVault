using Microsoft.AspNetCore.Mvc;
using Api.Models;
using Api.Utilities;

namespace Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ValidateController : ControllerBase
    {
        [HttpPost]
        public IActionResult Post([FromBody] InputModel input)
        {
            var sanitized = Api.Utilities.ValidationHelpers.Sanitize(input.UserInput);
            return Ok(new { sanitized });
        }
    }
}
