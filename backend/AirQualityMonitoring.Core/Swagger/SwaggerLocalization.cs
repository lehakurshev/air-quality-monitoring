using System.Reflection;
using Microsoft.Extensions.Localization;

namespace AirQualityMonitoring.Core.Swagger;

public static class SwaggerLocalization
{
    public static IStringLocalizer Create(IStringLocalizerFactory factory)
    {
        return factory.Create("SwaggerResources", Assembly.GetExecutingAssembly().GetName().Name!);
    }
}