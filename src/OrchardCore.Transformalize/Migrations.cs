using OrchardCore.Alias.Settings;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentFields.Settings;
using OrchardCore.Features.Services;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace TransformalizeModule {
   public class Migrations : DataMigration {

      private readonly IContentDefinitionManager _contentDefinitionManager;
      private readonly IModuleService _moduleService;
      private readonly ILogger<Migrations> _logger;

      public Migrations(
         IContentDefinitionManager contentDefinitionManager,
         IModuleService moduleService,
         ILogger<Migrations> logger
      ) {
         _contentDefinitionManager = contentDefinitionManager;
         _moduleService = moduleService;
         _logger = logger;
      }

      public int Create() {

         _contentDefinitionManager.AlterPartDefinition("TransformalizeReportPart", part => part
             .WithDisplayName("Transformalize Report Part")
             .WithDescription("Fields for Transformalize Report content type")
             .WithField("Arrangement", field => field
                 .OfType(nameof(TextField))
                 .WithDisplayName("Arrangement")
                 .WithPosition("3")
                 .WithEditor("TransformalizeArrangement")
                 .WithSettings(new TextFieldSettings {
                    Hint = string.Empty,
                    Required = true
                 }
                 )
             ).WithField("PageSizes", field => field
                 .OfType(nameof(TextField))
                 .WithDisplayName("Page Sizes")
                 .WithPosition("4")
                 .WithSettings(new TextFieldSettings {
                    Required = false,
                    Hint = "To overide default page sizes, specify a comma delimited list of page sizes (integers). To use the common page sizes defined in settings, leave it blank.  To disable pagination altogether, set this to 0."
                 }
                )
             ).WithField("BulkActions", field => field
                .OfType(nameof(BooleanField))
                .WithDisplayName("Bulk Actions")
                .WithPosition("1")
                .WithSettings(new BooleanFieldSettings {
                   DefaultValue = false,
                   Hint = "Allow user to select one, many, or all records for a bulk action?",
                   Label = "Bulk Actions"
                }
                )
              ).WithField("BulkActionValueField", field => field
                .OfType(nameof(TextField))
                .WithDisplayName("Bulk Action Value Field")
                .WithPosition("2")
                .WithSettings(new TextFieldSettings {
                   Required = false,
                   Hint = "Specify which field or calculated field provides the value for bulk actions."
                }
                )
              ).WithField("BulkActionCreateTask", field => field
                  .OfType(nameof(TextField))
                  .WithDisplayName("Bulk Action Create Task")
                  .WithPosition("3")
                  .WithSettings(new TextFieldSettings {
                     Required = false,
                     Hint = Common.BulkActionCreateTaskHint + " Leave blank to use default task defined in settings."
                  })
              ).WithField("BulkActionWriteTask", field => field
                  .OfType(nameof(TextField))
                  .WithDisplayName("Bulk Action Write Task")
                  .WithPosition("4")
                  .WithSettings(new TextFieldSettings {
                     Required = false,
                     Hint = Common.BulkActionWriteTaskHint + " Leave blank to use default task defined in settings."
                  })
              ).WithField("BulkActionSummaryTask", field => field
                  .OfType(nameof(TextField))
                  .WithDisplayName("Bulk Action Summary Task")
                  .WithPosition("5")
                  .WithSettings(new TextFieldSettings {
                     Required = false,
                     Hint = Common.BulkActionSummaryTaskHint + " Leave blank to use default task defined in settings."
                  })
              ).WithField("BulkActionRunTask", field => field
                  .OfType(nameof(TextField))
                  .WithDisplayName("Bulk Action Run Task")
                  .WithPosition("6")
                  .WithSettings(new TextFieldSettings {
                     Required = false,
                     Hint = Common.BulkActionRunTaskHint + " Leave blank to use default task defined in settings."
                  })
              ).WithField("BulkActionSuccessTask", field => field
                  .OfType(nameof(TextField))
                  .WithDisplayName("Bulk Action Success Task")
                  .WithPosition("7")
                  .WithSettings(new TextFieldSettings {
                     Required = false,
                     Hint = Common.BulkActionSuccessTaskHint + " Leave blank to use default task defined in settings."
                  })
              ).WithField("BulkActionFailTask", field => field
                  .OfType(nameof(TextField))
                  .WithDisplayName("Bulk Action Fail Task")
                  .WithPosition("8")
                  .WithSettings(new TextFieldSettings {
                     Required = false,
                     Hint = Common.BulkActionFailTaskHint + " Leave blank to use default task defined in settings."
                  })
              )
         );

         _contentDefinitionManager.AlterTypeDefinition("TransformalizeReport", builder => builder
             .Creatable()
             .Listable()
             .WithPart("TitlePart", part => part.WithPosition("1"))
             .WithPart("AliasPart", part => part
                 .WithPosition("2")
                 .WithSettings(new AliasPartSettings {
                    Pattern = "{{ ContentItem | title | slugify }}"
                 })
             )
             .WithPart("TransformalizeReportPart", part => part.WithPosition("3"))
             .WithPart("CommonPart", part => part.WithPosition("4"))
         );

         _contentDefinitionManager.AlterPartDefinition("TransformalizeTaskPart", part => part
             .WithDisplayName("Transformalize Task Part")
             .WithDescription("Provides fields for Transformalize Task content type")
             .WithField("Arrangement", field => field
                 .OfType(nameof(TextField))
                 .WithDisplayName("Arrangement")
                 .WithPosition("3")
                 .WithEditor("TransformalizeArrangement")
                 .WithSettings(new TextFieldSettings {
                    Hint = string.Empty,
                    Required = true
                 }
               )
            )
         );

         _contentDefinitionManager.AlterTypeDefinition("TransformalizeTask", builder => builder
             .Creatable()
             .Listable()
             .WithPart("TitlePart", part => part.WithPosition("1"))
             .WithPart("AliasPart", part => part
                 .WithPosition("2")
                 .WithSettings(new AliasPartSettings {
                    Pattern = "{{ ContentItem | title | slugify }}"
                 })
             )
             .WithPart("TransformalizeTaskPart", part => part.WithPosition("3"))
             .WithPart("CommonPart", part => part.WithPosition("4"))
         );

         return 1;
      }

      public int UpdateFrom1() {
         _contentDefinitionManager.AlterPartDefinition("TransformalizeReportPart", part => part
          .WithField("Map", field => field
             .OfType(nameof(BooleanField))
             .WithDisplayName("Map")
             .WithPosition("1")
             .WithSettings(new BooleanFieldSettings {
                DefaultValue = false,
                Hint = "Allow user to view a map of the report's records.",
                Label = "Map"
             }
             )
           )
          .WithField("MapColorField", field => field
             .OfType(nameof(TextField))
             .WithDisplayName("Map Color Field")
             .WithPosition("2")
             .WithSettings(new TextFieldSettings {
                Required = false,
                Hint = "Specify a field to control the color of the dots on the map.  Or, specify a hex representation of a color (e.g. #ffc0cb)."
             }
             )
           ).WithField("MapDescriptionField", field => field
               .OfType(nameof(TextField))
               .WithDisplayName("Map Description Field")
               .WithPosition("3")
               .WithSettings(new TextFieldSettings {
                  Required = false,
                  Hint = "This field's content is placed in the GEO JSON Description property, and is used in the pop up.  This is usually an HTML snippet you want to display."
               })
           ).WithField("MapLatitudeField", field => field
               .OfType(nameof(TextField))
               .WithDisplayName("Map Latititude Field")
               .WithPosition("4")
               .WithSettings(new TextFieldSettings {
                  Required = false,
                  Hint = "This field's value is used as the record's latitude on the map."
               })
           ).WithField("MapLongitudeField", field => field
               .OfType(nameof(TextField))
               .WithDisplayName("Map Longitude Field")
               .WithPosition("5")
               .WithSettings(new TextFieldSettings {
                  Required = false,
                  Hint = "This field's value is used as the record's longitude on the map."
               })
            )
         );

         return 2;
      }

      public int UpdateFrom2() {
         _contentDefinitionManager.AlterPartDefinition("TransformalizeReportPart", part => part
          .WithField("MapRadiusField", field => field
             .OfType(nameof(TextField))
             .WithDisplayName("Map Radius Field")
             .WithPosition("7")
             .WithSettings(new TextFieldSettings {
                Required = false,
                Hint = "Specify a field to control the radius of the dots on the map.  Or, just set an integer value (e.g. 7)."
             }
             )
           ).WithField("MapOpacityField", field => field
             .OfType(nameof(TextField))
             .WithDisplayName("Map Opacity Field")
             .WithPosition("8")
             .WithSettings(new TextFieldSettings {
                Required = false,
                Hint = "Specify a field to control the opacity of the dots on the map.  Or, just set a numeric value between 0.1 and 1.0 (e.g. 0.8)."
             }
             )
           )
         );

         return 3;
      }


      private async Task EnableFeature(string id) {

         var availableFeatures = await _moduleService.GetAvailableFeaturesAsync();

         var contentFields = availableFeatures.FirstOrDefault(f => f.Descriptor.Id == id);
         if (contentFields != null) {
            if (!contentFields.IsEnabled) {
               _logger.LogInformation($"Enabling {id}");
               await _moduleService.EnableFeaturesAsync(new[] { id });
            }
         } else {
            _logger.LogError($"Unable to find {id} features required for Transformalize.");
         }
      }
   }
}
