export interface WeatherForecastDto {
  date: string; // ISO date string
  temperatureC: number;
  summary?: string;
  temperatureF?: number;
}

export interface WeatherForecastsDto {
  count: number;
  allCount: number;
  forecasts: WeatherForecastDto[];
}