using Microsoft.AspNetCore.Mvc;
using Api.Models;

namespace Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            var summaries = new[]
            {
                "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
            };

            var forecast =  Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast(
                    System.DateOnly.FromDateTime(System.DateTime.Now.AddDays(index)),
                    System.Random.Shared.Next(-20, 55),
                    summaries[System.Random.Shared.Next(summaries.Length)]
                )).ToArray();

            return Ok(forecast);
        }
    }
}
