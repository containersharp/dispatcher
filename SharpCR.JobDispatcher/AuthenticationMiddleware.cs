using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace SharpCR.JobDispatcher
{
    public class AuthenticationMiddleware : IMiddleware
    {
        private readonly string _authKey;
        public AuthenticationMiddleware(IConfiguration configuration)
        {
            _authKey = configuration["DISPATCHER_AUTH_TOKEN"];
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