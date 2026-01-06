using DotnetTelemetryPlayground.ApiService.Modules.Weather.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DotnetTelemetryPlayground.ApiService.Modules.Weather.Presentation;

public static class WeatherForecastEndpoints
{
    public static void AddRoutes(this RouteGroupBuilder app)
    {
        // /// <summary>
        // /// Creates a new Weather Forecast
        // /// </summary>
        // /// <param name="app">The route group builder</param>
        // /// <remarks>
        // /// This endpoint allows you to create a new weather forecast by providing the necessary details such as date, temperature, and summary.
        // /// The request body should contain a JSON object with the following properties:
        // /// - Date: The date of the weather forecast (required)
        // /// - TemperatureC: The temperature in Celsius (required)
        // /// - Summary: A brief summary of the weather (required)
        // /// </remarks>
        // app.MapPost("/", async ([FromBody] WeatherForecastDto request, ISender sender, CancellationToken cancellationToken) => {
        //     // Create the command
        //     CreateWeatherForecastCommand command = new(request);

        //     // Send the command
        //     CreateWeatherForecastResponse response = await sender.Send(command, cancellationToken);

        //     return Results.Created($"/weather/{response.Id}", response);
        // });

        // /// <summary>
        // /// Updates an existing Weather Forecast
        // /// </summary>
        // /// <param name="app">The route group builder</param>
        // /// <remarks>
        // /// This endpoint allows you to update an existing weather forecast by providing the necessary details such as date, temperature, and summary.
        // /// The request body should contain a JSON object with the following properties:
        // /// - Date: The date of the weather forecast (required)
        // /// - TemperatureC: The temperature in Celsius (required)
        // /// - Summary: A brief summary of the weather (required)
        // /// </remarks>
        // app.MapPut("/", async ([FromBody] WeatherForecastDto request, ISender sender, CancellationToken cancellationToken) => {
        //     // Create the command
        //     UpdateWeatherForecastCommand command = new(request);

        //     // Send the command
        //     UpdateWeatherForecastResponse response = await sender.Send(command, cancellationToken);

        //     return Results.Ok(response);
        // });


        /// <summary>
        /// Retrieves a weather forecasts
        /// </summary>
        /// <param name="app">The route group builder</param>
        /// <remarks>
        /// This endpoint allows you to retrieve a weather forecasts, limited by the provided query parameters.
        ///  The request body should contain a JSON object with the following properties:
        ///  - MaxResults: The maximum number of results to return (optional, default is 10)
        ///  The response will contain a list of weather forecasts, each with the following properties:
        ///  - Id: The unique identifier of the weather forecast
        ///  - Date: The date of the weather forecast
        ///  - TemperatureC: The temperature in Celsius
        ///  - Summary: A brief summary of the weather
        /// </remarks>
        app.MapGet("/", async ([FromQuery] int? maxResults, IMediator sender, CancellationToken cancellationToken) => {

            // Create param
            GetWeatherQuery request = new()
            {
                MaxResults = maxResults
            };

            // Send the command
            WeatherForecastsDto res = await sender.Send(request, cancellationToken);

            return Results.Ok(res);
        });
    }
}

