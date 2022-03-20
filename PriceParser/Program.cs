using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PriceParser;
using PriceParser.Core;
using PriceParser.Core.Interfaces;
using PriceParser.Data;
using PriceParser.DataAccess;
using PriceParser.Domain;
using Serilog;

namespace PriceParser
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((ctx, lc) => lc
                .WriteTo.Console()
                .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day));

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.AddControllersWithViews();

            builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

            builder.Services.AddScoped<IRepository<Product>, Repository<Product>>();
            builder.Services.AddScoped<IRepository<MarketSite>, Repository<MarketSite>>();
            builder.Services.AddScoped<IRepository<ProductFromSites>, Repository<ProductFromSites>>();
            builder.Services.AddScoped<IRepository<ProductPrice>, Repository<ProductPrice>>();
            builder.Services.AddScoped<IRepository<UserReview>, Repository<UserReview>>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<IProductsService, ProductService>();
            builder.Services.AddScoped<IMarketSitesService, MarketSitesService>();
            builder.Services.AddScoped<IProductsFromSitesService, ProductFromSitesService>();
            builder.Services.AddScoped<IUserReviewsService, UserReviewsService>();
            builder.Services.AddScoped<IProductPricesService, ProductPricesService>();
            builder.Services.AddScoped<IParsingPricesService, ParcingPricesService>();

            // Add Hangfire services.
            builder.Services.AddHangfire(configuration => configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(builder.Configuration.GetConnectionString("HangfireConnection"), new SqlServerStorageOptions
                {
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true
                }));

            // Add the processing server as IHostedService
            builder.Services.AddHangfireServer();


            var app = builder.Build();

            

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
            app.MapRazorPages();

            app.MapHangfireDashboard();


            //apply migrations on every startup
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    db.Database.Migrate();
                }
                catch (Exception ex)
                {

                    var logger = services.GetRequiredService<Microsoft.Extensions.Logging.ILogger>();
                    logger.LogError(ex, "An error occurred while migrating the database.");
                }
                try
                {
                    var parsingPricesService = scope.ServiceProvider.GetRequiredService<IParsingPricesService>();
                    RecurringJob.AddOrUpdate(
                        "ParsingPricesFromSites",
                        () => parsingPricesService.ParseSaveAllAvailablePricesAsync(),
                        Cron.Hourly
                        );
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IParsingPricesService>>();
                    logger.LogError(ex, "An error occurred while adding a recurring job");
                }

            }

            app.Run();
        }
    }
}