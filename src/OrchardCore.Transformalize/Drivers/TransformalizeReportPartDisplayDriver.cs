using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Views;
using TransformalizeModule.Models;
using TransformalizeModule.ViewModels;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.DisplayManagement.Notify;
using System;
using Transformalize.Logging;
using Transformalize.Contracts;
using System.Collections.Generic;
using TransformalizeModule.Services.Contracts;
using OrchardCore.Environment.Cache;
using Autofac;
using Transformalize.Configuration;
using System.Linq;

namespace TransformalizeModule.Drivers {
   public class TransformalizeReportPartDisplayDriver : ContentPartDisplayDriver<TransformalizeReportPart> {

      private readonly IStringLocalizer<TransformalizeReportPartDisplayDriver> S;
      private readonly IHtmlLocalizer<TransformalizeReportPartDisplayDriver> H;
      private readonly IConfigurationContainer _container;
      private readonly INotifier _notifier;
      private readonly ISignal _signal;

      public TransformalizeReportPartDisplayDriver(
         IStringLocalizer<TransformalizeReportPartDisplayDriver> localizer,
         IHtmlLocalizer<TransformalizeReportPartDisplayDriver> htmlLocalizer,
         IConfigurationContainer container,
         INotifier notifier,
         ISignal signal
      ) {
         S = localizer;
         H = htmlLocalizer;
         _container = container;
         _notifier = notifier;
         _signal = signal;
      }

      // change display for admin content items view?
      // public override IDisplayResult Display(TransformalizeReportPart part) => View(nameof(TransformalizeReportPart), part);

      public override IDisplayResult Edit(TransformalizeReportPart part) {
         return Initialize<EditTransformalizeReportPartViewModel>("TransformalizeReportPart_Edit", model => {
            // it did not work out when i tried to flatten model (e.g. BulkActions.Value => BulkActions (a bool))
            model.TransformalizeReportPart = part;
            model.Arrangement = part.Arrangement;
            model.PageSizes = part.PageSizes;
            model.BulkActions = part.BulkActions;
            model.BulkActionValueField = part.BulkActionValueField;
         }).Location("Content:1");
      }

      public override async Task<IDisplayResult> UpdateAsync(TransformalizeReportPart part, IUpdateModel updater, UpdatePartEditorContext context) {

         // this driver override makes sure all the 
         // part fields are updated before the arrangement model is updated / validated

         //_notifier.Information(H["Part - Bulk Actions:{0}", part.BulkActions.Value]);
         //_notifier.Information(H["Part - Bulk Action Field:{0}", part.BulkActionValueField.Text]);

         var model = new EditTransformalizeReportPartViewModel();

         if (await updater.TryUpdateModelAsync(model, Prefix, m => m.BulkActions, m => m.BulkActionValueField, m => m.PageSizes, m => m.Arrangement)) {
            //_notifier.Information(H["Model - Bulk Actions:{0}", model.BulkActions.Value]);
            //_notifier.Information(H["Model - Bulk Action Field:{0}", model.BulkActionValueField.Text]);

            part.BulkActions.Value = model.BulkActions.Value;
            part.BulkActionValueField.Text = model.BulkActionValueField.Text;
            part.PageSizes.Text = model.PageSizes.Text;
         }

         if (model.BulkActions.Value) {
            if (string.IsNullOrEmpty(model.BulkActionValueField.Text)) {
               updater.ModelState.AddModelError(Prefix, S["Please set the bulk action value field for bulk actions."]);
            }
         }

         if (string.IsNullOrWhiteSpace(model.PageSizes.Text)) {
            model.PageSizes.Text = string.Empty;
         } else {
            foreach (var size in model.PageSizes.Text.Split(',', StringSplitOptions.RemoveEmptyEntries)) {
               if (!int.TryParse(size, out int result)) {
                  updater.ModelState.AddModelError(Prefix, S["{0} is not a valid integer in page sizes.", size]);
               }
            }
         }

         try {
            var logger = new MemoryLogger(LogLevel.Error);
            var process = _container.CreateScope(model.Arrangement.Text, part.ContentItem, new Dictionary<string, string>(), false).Resolve<Process>();
            if (process.Errors().Any()) {
               foreach (var error in process.Errors()) {
                  updater.ModelState.AddModelError(Prefix, S[error]);
               }
            }
            if (process.Warnings().Any()) {
               foreach (var warning in process.Warnings()) {
                  _notifier.Warning(H[warning]);
               }
            }
            if (process.Entities.Any()) {
               if (part.BulkActions.Value) {
                  if (!string.IsNullOrEmpty(part.BulkActionValueField.Text)) {
                     if (process.Entities[0].GetAllFields().All(f => f.Alias != part.BulkActionValueField.Text)) {
                        updater.ModelState.AddModelError(Prefix, S["The field {0} does not exist.", part.BulkActionValueField.Text]);
                     }
                  }
               }
            } else {
               updater.ModelState.AddModelError(Prefix, S["Please define an entity."]);
            }

         } catch (Exception ex) {
            updater.ModelState.AddModelError(Prefix, S[ex.Message]);
         }

         if (updater.ModelState.IsValid) {
            _signal.SignalToken(Common.GetCacheKey(part.ContentItem.Id));
         }

         return Edit(part, context);

      }


   }
}
