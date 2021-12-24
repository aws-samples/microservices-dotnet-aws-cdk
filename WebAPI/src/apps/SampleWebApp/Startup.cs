using System;
using Amazon;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Amazon.SimpleNotificationService;
using Amazon.XRay.Recorder.Handlers.AwsSdk;

namespace SampleWebApp
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

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "SampleWebApp Live", Version = "v1" });
            });

            //Register X-Ray
            AWSSDKHandler.RegisterXRayForAllServices();
            //Register Singleton SNS Client
            string region = Environment.GetEnvironmentVariable("AWS_REGION") ?? RegionEndpoint.USWest2.SystemName;
            services.AddSingleton<IAmazonSimpleNotificationService>(new AmazonSimpleNotificationServiceClient(region: RegionEndpoint.GetBySystemName(region)) );

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SampleWebApp v1"));
            }

            //X-Ray
            app.UseXRay("demo-web-api");

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
