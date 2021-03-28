using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpCR.JobDispatcher.Models;
using SharpCR.JobDispatcher.Services;

namespace SharpCR.JobDispatcher
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<DispatcherConfig>(Configuration.GetSection("DispatcherConfig"));
            services.AddScoped<AuthenticationMiddleware>();
            services.AddSingleton(new JobProducerConsumerQueue(new CancellationTokenSource()));
            services.AddSingleton(new List<Job>());
            services.AddSingleton<Sweeper>();
            services.AddSingleton<ManifestProber>();
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, Sweeper sweeper, IHostApplicationLifetime lifetime)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMiddleware<AuthenticationMiddleware>();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
            
            sweeper.Start();
            lifetime.ApplicationStopping.Register(sweeper.Stop);
        }
    }
}
