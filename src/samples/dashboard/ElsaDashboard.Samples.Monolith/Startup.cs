using System;
using System.Net.Http.Headers;
using Elsa;
using Elsa.Persistence.EntityFramework.Core.Extensions;
using Elsa.Persistence.EntityFramework.Sqlite;
using Elsa.Persistence.MongoDb.Extensions;
using ElsaDashboard.Backend.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ElsaDashboard.Samples.Monolith
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public IConfiguration Configuration { get; }
        public IHostEnvironment Environment { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Elsa Server.
            var elsaSection = Configuration.GetSection("Elsa");
            
            services
                .AddElsa(options => options
                    //.UseYesSqlPersistence()
                    .UseMongoDbPersistence(mongo => mongo.ConnectionString = "mongodb://localhost/elsa")
                    //.UseEntityFrameworkPersistence(ef => ef.UseSqlite())
                    .AddConsoleActivities()
                    .AddHttpActivities(elsaSection.GetSection("Http").Bind)
                    .AddEmailActivities(elsaSection.GetSection("Smtp").Bind)
                    .AddQuartzTemporalActivities()
                    .AddJavaScriptActivities()
                    .AddWorkflowsFrom<Startup>()
                );

            services
                .AddElsaApiEndpoints()
                .AddElsaSwagger();
            
            // Elsa Dashboard.
            services.AddRazorPages();
            services.AddElsaDashboardUI();
            services.AddElsaDashboardBackend(options => options.ServerUrl = Configuration.GetValue<Uri>("Elsa:Http:BaseUrl"));

            if (Program.RuntimeModel == BlazorRuntimeModel.Server) 
                services.AddServerSideBlazor(options =>
                {
                    options.DetailedErrors = !Environment.IsProduction();
                    options.JSInteropDefaultCallTimeout = TimeSpan.FromSeconds(30);
                });
            
            // Allow arbitrary client browser apps to access the API for demo purposes only.
            // In a production environment, make sure to allow only origins you trust.
            services.AddCors(cors => cors.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().WithExposedHeaders("Content-Disposition")));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Elsa"));
                
                if (Program.RuntimeModel == BlazorRuntimeModel.Browser)
                    app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            if (Program.RuntimeModel == BlazorRuntimeModel.Browser)
                app.UseBlazorFrameworkFiles();
            
            app.UseStaticFiles();
            app.UseHttpActivities();
            app.UseCors();
            app.UseRouting();
            app.UseElsaGrpcServices();
            
            app.UseEndpoints(endpoints =>
            {
                // Elsa Server uses ASP.NET Core Controllers.
                endpoints.MapControllers();
                
                if (Program.RuntimeModel == BlazorRuntimeModel.Server)
                    endpoints.MapBlazorHub();
                
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}