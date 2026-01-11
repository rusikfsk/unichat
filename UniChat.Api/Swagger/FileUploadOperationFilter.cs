using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace UniChat.Api.Swagger;

public sealed class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var apiDesc = context.ApiDescription;
        if (!IsMultipartWithFile(apiDesc)) return;

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content =
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties =
                        {
                            ["file"] = new OpenApiSchema { Type = "string", Format = "binary" }
                        },
                        Required = new HashSet<string> { "file" }
                    }
                }
            }
        };
    }

    private static bool IsMultipartWithFile(ApiDescription apiDesc)
    {
        return apiDesc.ParameterDescriptions.Any(p =>
            p.Source?.Id == "Form" &&
            string.Equals(p.Name, "file", StringComparison.OrdinalIgnoreCase));
    }
}
