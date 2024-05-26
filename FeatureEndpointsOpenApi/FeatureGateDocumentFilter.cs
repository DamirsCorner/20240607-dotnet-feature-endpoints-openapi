using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FeatureEndpointsOpenApi;

public class FeatureGateDocumentFilter(IFeatureManager featureManager) : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach (var apiDescription in context.ApiDescriptions)
        {
            var filterPipeline = apiDescription.ActionDescriptor.FilterDescriptors;
            var featureAttributes = filterPipeline
                .Select(filterInfo => filterInfo.Filter)
                .OfType<FeatureGateAttribute>()
                .ToList();

            bool endpointEnabled = true;
            foreach (var attribute in featureAttributes)
            {
                var featureValues = attribute.Features.Select(feature =>
                    featureManager.IsEnabledAsync(feature).GetAwaiter().GetResult()
                );
                endpointEnabled &=
                    attribute.RequirementType == RequirementType.Any
                        ? featureValues.Any(isEnabled => isEnabled)
                        : featureValues.All(isEnabled => isEnabled);
            }

            if (!endpointEnabled)
            {
                var path = $"/{apiDescription.RelativePath}";
                var apiPath = swaggerDoc.Paths[path];
                if (apiPath != null)
                {
                    if (
                        Enum.TryParse<OperationType>(
                            apiDescription.HttpMethod,
                            true,
                            out var operationType
                        )
                    )
                    {
                        apiPath.Operations.Remove(operationType);
                    }

                    if (apiPath.Operations.Count == 0)
                    {
                        swaggerDoc.Paths.Remove(path);
                    }
                }
            }
        }
    }
}
