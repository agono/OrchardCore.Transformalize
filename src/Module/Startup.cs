using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Module.Fields;
using Module.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.ResourceManagement;
using Fluid;
using OrchardCore.Data.Migration;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using Module.Drivers;
using Module.ViewModels;
using Module.Services.Contracts;
using Module.Services;
using Module.Handlers;
using OrchardCore.ContentManagement.Handlers;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Settings;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using Module.Navigation;

namespace Module {
   public class Startup : StartupBase {

      public Startup() {
         TemplateContext.GlobalMemberAccessStrategy.Register<TransformalizeArrangementField>();
         TemplateContext.GlobalMemberAccessStrategy.Register<DisplayTransformalizeArrangementFieldViewModel>();
         TemplateContext.GlobalMemberAccessStrategy.Register<PageSizesField>();
         TemplateContext.GlobalMemberAccessStrategy.Register<DisplayPageSizesFieldViewModel>();
      }

      public override void ConfigureServices(IServiceCollection services) {

         services.AddSession();
         services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

         services.AddScoped<ILinkService, LinkService>();
         services.AddScoped<ISortService, SortService>();
         services.AddScoped<IStickyParameterService, StickyParameterService>();
         services.AddScoped<IArrangementLoadService, ArrangementLoadService>();
         services.AddScoped<IArrangementRunService, ArrangementRunService>();
         services.AddScoped<IParameterService, ParameterService>();
         services.AddScoped<IReportService, ReportService>();
         services.AddScoped<ISettingsService, SettingsService>();

         services.AddScoped<IDataMigration, Migrations>();
         services.AddScoped<IResourceManifestProvider, ResourceManifest>();

         services.AddContentField<TransformalizeArrangementField>(); // UseDisplayDriver in dev branch
         services.AddScoped<IContentFieldDisplayDriver, TransformalizeArrangementFieldDisplayDriver>();
         services.AddContentField<PageSizesField>(); // UseDisplayDriver in dev branch
         services.AddScoped<IContentFieldDisplayDriver, PageSizesFieldDisplayDriver>();
         services.AddContentPart<TransformalizeReportPart>();

         services.AddScoped<IContentHandler, TransformalizeHandler>();

         // Transformalize Settings
         services.AddScoped<IDisplayDriver<ISite>, TransformalizeSettingsDisplayDriver>();
         services.AddScoped<IPermissionProvider, Permissions>();
         services.AddScoped<INavigationProvider, TransformalizeSettingsAdminMenu>();

      }

      public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider) {

         routes.MapAreaControllerRoute(
             name: null,
             areaName: Common.ModuleName,
             pattern: "report/{ContentItemId}",
             defaults: new { controller = "Report", action = "Index" }
         );

         routes.MapAreaControllerRoute(
             name: null,
             areaName: Common.ModuleName,
             pattern: "report/{format}/{ContentItemId}",
             defaults: new { controller = "Report", action = "Run", format = "json" }
         );

         routes.MapAreaControllerRoute(
             name: null,
             areaName: Common.ModuleName,
             pattern: "report/download/csv/{ContentItemId}",
             defaults: new { controller = "Report", action = "SaveAsCsv" }
         );

         routes.MapAreaControllerRoute(
            name: null,
             areaName: Common.ModuleName,
             pattern: "report/download/json/{ContentItemId}",
             defaults: new { controller = "Report", action = "SaveAsJson" }
         );

         builder.UseSession();
      }
   }
}