//using Microsoft.OpenApi.Models;
//using Swashbuckle.AspNetCore.SwaggerGen;

//public class SwaggerFileOperationFilter2 : IOperationFilter
//{
//    public void Apply(OpenApiOperation operation, OperationFilterContext context)
//    {
//        var hasCompareFaces = context.MethodInfo.Name == "CompareFaces";

//        if (hasCompareFaces)
//        {
//            operation.RequestBody = new OpenApiRequestBody
//            {
//                Content =
//                {
//                    ["multipart/form-data"] = new OpenApiMediaType
//                    {
//                        Schema = new OpenApiSchema
//                        {
//                            Type = "object",
//                            Properties =
//                            {
//                                ["file1"] = new OpenApiSchema
//                                {
//                                    Type = "string",
//                                    Format = "binary"
//                                },
//                                ["file2"] = new OpenApiSchema
//                                {
//                                    Type = "string",
//                                    Format = "binary"
//                                }
//                            },
//                            Required = new HashSet<string> { "file1", "file2" }
//                        }
//                    }
//                }
//            };
//        }
//    }
//}
