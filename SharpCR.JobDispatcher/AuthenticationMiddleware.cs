using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace SharpCR.JobDispatcher
{
    public class AuthenticationMiddleware : IMiddleware
    {
        private readonly string _authKey;
        public AuthenticationMiddleware(IOptions<DispatcherConfig> dispatcherOptions)
        {
            _authKey = dispatcherOptions.Value.AuthorizationToken;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var authHeader = context.Request.Headers["Authorization"].ToString();
            if (!string.IsNullOrEmpty(_authKey) && !string.Equals(authHeader, _authKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.CompleteAsync();
                return;
            }

            await next(context);
        }
    }
}