using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TransformalizeModule.Services.Contracts;
using TransformalizeModule.ViewModels;
using System.Collections.Generic;
using TransformalizeModule.Models;
using System.Linq;
using TransformalizeModule.Services;
using System.Dynamic;
using Transformalize.Configuration;
using OrchardCore.Users.Services;
using OrchardCore.Users.Models;
using System.Text;

namespace TransformalizeModule.Controllers {

   public class BulkActionController : Controller {

      private readonly ITaskService _taskService;
      private readonly IReportService _reportService;
      private readonly IFormService _formService;
      private readonly CombinedLogger<BulkActionController> _logger;
      private readonly IUserService _userService;

      public BulkActionController(
         ITaskService taskService,
         IReportService reportService,
         IFormService formService,
         IUserService userService,
         CombinedLogger<BulkActionController> logger
      ) {
         _taskService = taskService;
         _reportService = reportService;
         _formService = formService;
         _userService = userService;
         _logger = logger;
      }

      [HttpPost]
      public async Task<ActionResult> Create(BulkActionRequest request) {

         if (HttpContext == null || HttpContext.User == null || HttpContext.User.Identity == null || !HttpContext.User.Identity.IsAuthenticated) {
            return Unauthorized();
         }

         var userName = HttpContext.User.Identity.Name ?? "Anonymous";

         var report = await _reportService.Validate(new TransformalizeRequest(request.ContentItemId, userName));
         if (report.Fails()) {
            return report.ActionResult;
         }

         var referrer = Request.Headers.ContainsKey("Referer") ? Request.Headers["Referer"].ToString() : Url.Action("Index", "Report", new { request.ContentItemId });

         // confirm we have an action registered in the report
         if (report.Process.Actions.Any(a => a.Name == request.ActionName)) {

            var bulkAction = await _taskService.Validate(new TransformalizeRequest(request.ActionName, userName) { Secure = false });

            if (bulkAction.Fails() && bulkAction.Process.Message != Common.InvalidParametersMessage) {
               return bulkAction.ActionResult;
            }

            #region batch creation
            var user = await _userService.GetUserAsync(userName) as User;

            var createAlias = "batch-create";
            var createParameters = new Dictionary<string, string> {

               { "UserId", user.Id.ToString() },
               { "UserName", user.UserName },
               { "UserEmail", user.Email },

               { "ReportId", report.ContentItem.Id.ToString() },
               { "ReportContentItemId", report.ContentItem.ContentItemId },
               { "ReportContentItemVersionId", report.ContentItem.ContentItemVersionId },
               { "ReportTitle", report.ContentItem.DisplayText },

               { "TaskId", bulkAction.ContentItem.ContentItemId },
               { "TaskContentItemId", bulkAction.ContentItem.ContentItemId },
               { "TaskContentItemVersionId", bulkAction.ContentItem.ContentItemVersionId },
               { "TaskTitle", bulkAction.ContentItem.DisplayText },

               { "Description", report.Process.Actions.First(a=>a.Name == request.ActionName).Description }
            };

            var create = await _taskService.Validate(new TransformalizeRequest(createAlias, userName) { Secure = false, InternalParameters = createParameters });

            if (create.Fails()) {
               return create.ActionResult;
            }

            await _taskService.RunAsync(create.Process);
            if (create.Process.Status != 200) {
               _logger.Warn(() => $"User {userName} received error running action {createAlias}.");
               return View("Log", new LogViewModel(_logger.Log, create.Process, create.ContentItem));
            }

            var entity = create.Process.Entities.FirstOrDefault();

            if (entity == null) {
               _logger.Error(() => $"The {createAlias} task is missing an entity.  It needs an entity with at least one row that returns one row of data (e.g. the BatchId).");
               return View("Log", new LogViewModel(_logger.Log, create.Process, create.ContentItem));
            }

            if (!entity.Rows.Any()) {
               _logger.Error(() => $"The {createAlias} task didn't produce a row (e.g. a single row with a BatchId need to associate the batch values with this batch).");
               return View("Log", new LogViewModel(_logger.Log, create.Process, create.ContentItem));
            }
            #endregion

            #region batch writing
            var writeAlias = "batch-write";
            var writeParameters = new Dictionary<string, string>();
            foreach (var field in entity.GetAllOutputFields()) {
               writeParameters[field.Alias] = entity.Rows[0][field.Alias].ToString();
            }

            var write = await _taskService.Validate(new TransformalizeRequest(writeAlias, userName) { Secure = false, InternalParameters = writeParameters });

            if (write.Fails()) {
               return write.ActionResult;
            }

            // potential memory problem (could be solved by merging report and batch write into one process)
            var writeEntity = write.Process.Entities.First();
            var batchValueField = writeEntity.Fields.LastOrDefault(f => f.Input && f.Output);

            if (batchValueField == null) {
               _logger.Error(() => $"Could not identify batch value field in {writeAlias}.");
               return View("Log", new LogViewModel(_logger.Log, write.Process, write.ContentItem));
            }

            if (request.ActionCount == 0) {
               var batchProcess = _reportService.LoadForBatch(report.ContentItem);

               await _taskService.RunAsync(batchProcess);
               foreach (var batchRow in batchProcess.Entities.First().Rows) {
                  var row = new Transformalize.Impl.CfgRow(new[] { batchValueField.Alias });
                  row[batchValueField.Alias] = batchRow[report.Part.BulkActionValueField.Text];
                  writeEntity.Rows.Add(row);
               }
            } else {
               foreach (var batchValue in request.Records) {
                  var row = new Transformalize.Impl.CfgRow(new[] { batchValueField.Alias });
                  row[batchValueField.Alias] = batchValue;
                  writeEntity.Rows.Add(row);
               }
            }

            await _taskService.RunAsync(write.Process);
            #endregion

            writeParameters["ReportContentItemId"] = bulkAction.ContentItem.ContentItemId;
            writeParameters["TaskContentItemId"] = bulkAction.ContentItem.ContentItemId;
            return RedirectToAction("Review", ParametersToRouteValues(writeParameters));

         } else {
            _logger.Warn(() => $"User {userName} called missing action {request.ActionName} in {report.ContentItem.DisplayText}.");
         }

         return View("Log", new LogViewModel(_logger.Log, report.Process, report.ContentItem));

      }

      public async Task<ActionResult> Review(BulkActionReviewRequest request) {

         if (HttpContext == null || HttpContext.User == null || HttpContext.User.Identity == null || !HttpContext.User.Identity.IsAuthenticated) {
            return Unauthorized();
         }

         var user = HttpContext.User.Identity.Name ?? "Anonymous";

         var batchSummaryAlias = "batch-summary";
         var batchSummary = await _taskService.Validate(new TransformalizeRequest(batchSummaryAlias, user) { Secure = false });

         if (batchSummary.Fails()) {
            return batchSummary.ActionResult;
         }

         await _taskService.RunAsync(batchSummary.Process);

         // if batch summary can provide provide content item id(s)
         if (request.TaskContentItemId == null) {
            request.TaskContentItemId = GetFieldFromSummary(batchSummary.Process, "TaskContentItemId");
         }
         if (request.ReportContentItemId == null) {
            request.ReportContentItemId = GetFieldFromSummary(batchSummary.Process, "ReportContentItemId");
         }

         var bulkAction = await _formService.Validate(new TransformalizeRequest(request.TaskContentItemId, user));

         if (bulkAction.Fails()) {
            return bulkAction.ActionResult;
         }

         // because they will need to come back to review page after validating their parameters
         bulkAction.Process.Parameters.Add(new Parameter() { Name = "TaskContentItemId", Value = request.TaskContentItemId });
         bulkAction.Process.Parameters.Add(new Parameter() { Name = "ReportContentItemId", Value = request.ReportContentItemId });

         return View(new BulkActionViewModel(bulkAction, batchSummary.Process));
      }


      public async Task<ActionResult> Form(BulkActionReviewRequest request) {

         if (HttpContext == null || HttpContext.User == null || HttpContext.User.Identity == null || !HttpContext.User.Identity.IsAuthenticated) {
            return Unauthorized();
         }

         var user = HttpContext.User.Identity.Name ?? "Anonymous";
         var bulkAction = await _formService.Validate(new TransformalizeRequest(request.TaskContentItemId, user));

         if (bulkAction.Fails()) {
            return bulkAction.ActionResult;
         }

         // because they will need to come back to review page after validating their parameters
         bulkAction.Process.Parameters.Add(new Parameter() { Name = "TaskContentItemId", Value = request.TaskContentItemId });
         bulkAction.Process.Parameters.Add(new Parameter() { Name = "ReportContentItemId", Value = request.ReportContentItemId });

         return View("Form", bulkAction);
      }


      public async Task<ActionResult> Run(BulkActionReviewRequest request) {

         if (HttpContext == null || HttpContext.User == null || HttpContext.User.Identity == null || !HttpContext.User.Identity.IsAuthenticated) {
            return Unauthorized();
         }

         var user = HttpContext.User.Identity.Name ?? "Anonymous";

         var bulkAction = await _taskService.Validate(new TransformalizeRequest(request.TaskContentItemId, user));

         if (bulkAction.Fails()) {
            return bulkAction.ActionResult;
         }

         await _taskService.RunAsync(bulkAction.Process);

         var recordsAffected = bulkAction.Process.Actions.Where(a => a.RowCount > 0).Sum(a => a.RowCount) + bulkAction.Process.Entities.Where(a => a.Hits > 0).Sum(e => e.Hits);
         var closeParameters = new Dictionary<string, string>() { { "RecordsAffected", recordsAffected.ToString() } };

         if (bulkAction.Process.Status == 200) {
            var batchSuccessAlias = "batch-success";
            var batchSuccess = await _taskService.Validate(new TransformalizeRequest(batchSuccessAlias, user) { InternalParameters = closeParameters });

            if (batchSuccess.Fails()) {
               _logger.Warn(() => $"{bulkAction.ContentItem.DisplayText} succeeded but {batchSuccessAlias} failed to load.");
            } else {
               await _taskService.RunAsync(batchSuccess.Process);
            }
         } else {
            var batchFailAlias = "batch-fail";
            var message = new StringBuilder(bulkAction.Process.Message);
            foreach(var error in _logger.Log.Where(l=>l.LogLevel == Transformalize.Contracts.LogLevel.Error)) {
               message.AppendLine(error.Message);
            }
            closeParameters["Message"] = message.ToString();
            var batchFail = await _taskService.Validate(new TransformalizeRequest(batchFailAlias, user) { InternalParameters = closeParameters });

            if (batchFail.Fails()) {
               _logger.Warn(() => $"{bulkAction.ContentItem.DisplayText} failed and {batchFailAlias} failed to load.");
            } else {
               await _taskService.RunAsync(batchFail.Process);
            }
         }

         return RedirectToAction("Result", ParametersToRouteValues(bulkAction.Process.Parameters));
      }

      public async Task<ActionResult> Result() {

         if (HttpContext == null || HttpContext.User == null || HttpContext.User.Identity == null || !HttpContext.User.Identity.IsAuthenticated) {
            return Unauthorized();
         }

         var user = HttpContext.User.Identity.Name ?? "Anonymous";

         var batchSummaryAlias = "batch-summary";
         var batchSummary = await _taskService.Validate(new TransformalizeRequest(batchSummaryAlias, user) { Secure = false });

         if (batchSummary.Fails()) {
            return batchSummary.ActionResult;
         }

         await _taskService.RunAsync(batchSummary.Process);

         return View(batchSummary.Process);
      }



      private dynamic ParametersToRouteValues(IDictionary<string, string> parameters) {
         var routeValues = new ExpandoObject();
         var editable = (ICollection<KeyValuePair<string, object>>)routeValues;
         foreach (var kvp in parameters) {
            editable.Add(new KeyValuePair<string, object>(kvp.Key, kvp.Value));
         }
         dynamic d = routeValues;
         return d;
      }

      private dynamic ParametersToRouteValues(List<Parameter> parameters) {
         var routeValues = new ExpandoObject();
         var editable = (ICollection<KeyValuePair<string, object>>)routeValues;
         foreach (var p in parameters) {
            editable.Add(new KeyValuePair<string, object>(p.Name, p.Value));
         }
         dynamic d = routeValues;
         return d;
      }

      public string GetFieldFromSummary(Process process, string fieldName) {
         if (process != null) {
            if (process.Entities.Any() && process.Entities[0].Rows.Any()) {
               var fields = process.Entities[0].GetAllOutputFields();
               var field = fields.FirstOrDefault(f => f.Alias == fieldName);
               if (field != null) {
                  return process.Entities[0].Rows[0][fieldName]?.ToString() ?? string.Empty;
               }
            }
         }
         return null;
      }

   }
}
