using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Collections.Generic;

namespace FotoFromFaceControl.Filters
{
    public class SwaggerFileOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var fileParams = context.MethodInfo.GetParameters()
                .Where(p => p.ParameterType == typeof(IFormFile));

            if (fileParams.Any())
            {
                var properties = new Dictionary<string, OpenApiSchema>();
                var required = new HashSet<string>();

                foreach (var fileParam in fileParams)
                {
                    properties[fileParam.Name] = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary"
                    };
                    required.Add(fileParam.Name);
                }

                operation.RequestBody = new OpenApiRequestBody
                {
                    Content =
                    {
                        ["multipart/form-data"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = "object",
                                Properties = properties,
                                Required = required
                            }
                        }
                    }
                };

                // Query parametreleri ekle
                foreach (var param in context.MethodInfo.GetParameters().Where(p => p.ParameterType != typeof(IFormFile)))
                {
                    if (operation.Parameters.All(p => p.Name != param.Name))
                    {
                        operation.Parameters.Add(new OpenApiParameter
                        {
                            Name = param.Name,
                            In = ParameterLocation.Query,
                            Required = false,
                            Schema = new OpenApiSchema { Type = "string" }
                        });
                    }
                }
            }
        }
    }
}
