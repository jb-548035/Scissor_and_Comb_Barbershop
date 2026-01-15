using Microsoft.Extensions.Logging;
using hair.Services;

namespace hair
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

            builder.Services.AddSingleton<NotificationService>();

            // Register authentication services
            builder.Services.AddScoped(sp => new AuthenticationService(
                serverName: "db35666.public.databaseasp.net",
                databaseName: "db35666",
                userId: "db35666",  // Replace with your cloud database username
                password: "dA!56@qF_X8g",  // Replace with your cloud database password
                localServerName: $@"{Environment.MachineName}\SQLEXPRESS",
                localDatabaseName: "db_hair"
                // No localUserId/password = Windows Authentication
            ));
            builder.Services.AddSingleton<AuthStateService>();

            // Register cloud database service
            builder.Services.AddScoped(sp => new DatabaseService(
                serverName: "db35666.public.databaseasp.net",
                databaseName: "db35666",
                userId: "db35666",
                password: "dA!56@qF_X8g"
            ));

            // Register sync service for offline support
            builder.Services.AddSingleton<SyncService>(sp => 
            {
                var cloudDb = new DatabaseService(
                    serverName: "db35666.public.databaseasp.net",
                    databaseName: "db35666",
                    userId: "db35666",
                    password: "dA!56@qF_X8g"
                );
                return new SyncService(cloudDb);
            });

            // Register hybrid database service (routes between local SQL Server and cloud)
            builder.Services.AddScoped<HybridDatabaseService>(sp =>
            {
                // Local SQL Server instance (Windows Authentication)
                var localDb = new DatabaseService(
                    serverName: $@"{Environment.MachineName}\SQLEXPRESS",
                    databaseName: "db_hair"
                    // No userId/password = Windows Authentication
                );

                // Cloud SQL Server instance
                var cloudDb = new DatabaseService(
                    serverName: "db35666.public.databaseasp.net",
                    databaseName: "db35666",
                    userId: "db35666",
                    password: "dA!56@qF_X8g"
                );

                var syncService = sp.GetRequiredService<SyncService>();
                return new HybridDatabaseService(localDb, cloudDb, syncService);
            });

            // Register initialization service
            builder.Services.AddSingleton(sp => new InitializationService(
                serverName: "db35666.public.databaseasp.net",
                databaseName: "db35666",
                userId: "db35666",  // Replace with your cloud database username
                password: "dA!56@qF_X8g",  // Replace with your cloud database password
                localServerName: $@"{Environment.MachineName}\SQLEXPRESS",
                localDatabaseName: "db_hair"
                // No localUserId/password = Windows Authentication
            ));

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            var app = builder.Build();

            // Initialize system (create default Admin if needed) - Run asynchronously without blocking
            try
            {
                System.Diagnostics.Debug.WriteLine("[MauiProgram] Getting InitializationService...");
                var initService = app.Services.GetRequiredService<InitializationService>();
                System.Diagnostics.Debug.WriteLine("[MauiProgram] Starting async initialization (non-blocking)...");
                
                // Run initialization without blocking app startup
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await initService.InitializeSystemAsync();
                        System.Diagnostics.Debug.WriteLine("[MauiProgram] InitializeSystemAsync completed");

                        using var scope = app.Services.CreateScope();
                        var authService = scope.ServiceProvider.GetRequiredService<AuthenticationService>();
                        await authService.WarmLocalUserCacheFromCloudAsync();
                        System.Diagnostics.Debug.WriteLine("[MauiProgram] WarmLocalUserCacheFromCloudAsync completed");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MauiProgram] Async initialization failed: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MauiProgram] Failed to start initialization: {ex.Message}");
            }

            return app;
        }
    }
}
