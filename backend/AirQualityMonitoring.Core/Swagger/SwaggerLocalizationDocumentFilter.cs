using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AirQualityMonitoring.Core.Swagger;

public class SwaggerLocalizationDocumentFilter : IDocumentFilter
{
    private readonly IStringLocalizerFactory _factory;

    public SwaggerLocalizationDocumentFilter(IStringLocalizerFactory factory)
    {
        _factory = factory;
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var localizer = SwaggerLocalization.Create(_factory);
        var culture = swaggerDoc.Info.Version;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        
        swaggerDoc.Info.Title = localizer["ApiTitle"];
        swaggerDoc.Info.Description = localizer["ApiDescription"];

        foreach (var path in swaggerDoc.Paths)
        {
            foreach (var op in path.Value.Operations)
            {
                if (op.Value.Tags == null) 
                    continue;

                foreach (var tag in op.Value.Tags)
                {
                    tag.Name = localizer[$"{op.Value.OperationId}Tag"];
                }
                
                op.Value.Description = localizer[$"{op.Value.OperationId}Description"];
            }
        }
    }
}