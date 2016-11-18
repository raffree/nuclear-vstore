﻿using Amazon;
using Amazon.S3;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json.Converters;

using NuClear.VStore.Host.Convensions;
using NuClear.VStore.Host.Swashbuckle;
using NuClear.VStore.Json;
using NuClear.VStore.Locks;
using NuClear.VStore.Objects;
using NuClear.VStore.Options;
using NuClear.VStore.Sessions;
using NuClear.VStore.Templates;

namespace NuClear.VStore.Host
{
    public class Startup
    {
        private readonly IConfigurationRoot _configuration;

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            _configuration = builder.Build();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Console;

            services.AddMvc(options => options.UseGlobalRoutePrefix(new RouteAttribute("api/1.0")))
                    .AddJsonOptions(
                        options =>
                            {
                                options.SerializerSettings.Converters.Insert(0, new TemplateDescriptorJsonConverter());
                                options.SerializerSettings.Converters.Insert(1, new StringEnumConverter { CamelCaseText = true });
                            });

            services.AddSwaggerGen(x => x.OperationFilter<UploadFileOperationFilter>());

            services.AddOptions();
            services.AddDefaultAWSOptions(_configuration.GetAWSOptions());
            services.Configure<CephOptions>(_configuration.GetSection("Ceph"));
            services.Configure<LockOptions>(_configuration.GetSection("Ceph:Locks"));
            services.Configure<VStoreOptions>(_configuration.GetSection("VStore"));

            services.AddAWSService<IAmazonS3>();
            services.AddSingleton(x => new LockSessionManager(x.GetService<IAmazonS3>(), x.GetService<IOptions<LockOptions>>().Value));
            services.AddScoped(x => new LockSessionFactory(x.GetService<IAmazonS3>(), x.GetService<IOptions<LockOptions>>().Value));
            services.AddScoped(x => new TemplateStorageReader(x.GetService<IOptions<CephOptions>>().Value, x.GetService<IAmazonS3>()));
            services.AddScoped(
                x => new TemplateManagementService(
                         x.GetService<IOptions<CephOptions>>().Value,
                         x.GetService<IAmazonS3>(),
                         x.GetService<TemplateStorageReader>(),
                         x.GetService<LockSessionFactory>()));
            services.AddScoped(
                x => new ObjectStorageReader(
                         x.GetService<IOptions<CephOptions>>().Value,
                         x.GetService<IAmazonS3>(),
                         x.GetService<TemplateStorageReader>()));
            services.AddScoped(
                x => new ObjectManagementService(
                         x.GetService<IOptions<CephOptions>>().Value,
                         x.GetService<IAmazonS3>(),
                         x.GetService<TemplateStorageReader>(),
                         x.GetService<LockSessionFactory>()));
            services.AddScoped(
                x => new SessionManagementService(
                         x.GetService<IOptions<VStoreOptions>>().Value.Endpoint,
                         x.GetService<IOptions<VStoreOptions>>().Value.FileStorageEndpoint,
                         x.GetService<IOptions<CephOptions>>().Value.FilesBucketName,
                         x.GetService<IAmazonS3>(),
                         x.GetService<TemplateStorageReader>()));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(_configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseDeveloperExceptionPage();

            app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
            app.UseMvc();
            app.UseSwagger();
            app.UseSwaggerUi();
        }
    }
}
