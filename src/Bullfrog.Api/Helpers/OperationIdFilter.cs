﻿using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bullfrog.Api.Helpers
{
    /// <summary>
    /// Adds default OperationId values for an operation.
    /// </summary>
    internal class OperationIdFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if(operation.OperationId is null)
            {
                operation.OperationId = context.MethodInfo?.Name;
            }
        }
    }
}
