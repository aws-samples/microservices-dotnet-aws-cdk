using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.SQS;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging;
namespace WorkerDb
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                // .ConfigureLogging(builder =>
                // {
                //     builder.AddConsoleFormatter
                // })
                .ConfigureServices((hostContext, services) =>
                {
                    //Register X-Ray
                    AWSXRayRecorder.InitializeInstance(hostContext.Configuration);
                    AWSSDKHandler.RegisterXRayForAllServices();

                    //Register Services
                    services.AddDefaultAWSOptions(hostContext.Configuration.GetAWSOptions());
                    services.AddAWSService<IAmazonDynamoDB>();
                    services.AddAWSService<IAmazonSQS>();

                    services.AddHostedService<Worker>();
                });
    }
}
