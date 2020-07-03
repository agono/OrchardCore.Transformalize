using Autofac;
using TransformalizeModule.Services.Contracts;
using StackExchange.Profiling;
using System.Linq;
using System.Threading.Tasks;
using Transformalize.Configuration;
using Transformalize.Contracts;
using IContainer = TransformalizeModule.Services.Contracts.IContainer;

namespace TransformalizeModule.Services {

   public class ArrangementRunService : IArrangementRunService {

      private readonly IContainer _container;
      private readonly CombinedLogger<ArrangementRunService> _logger;

      public ArrangementRunService(
         IContainer container,
         CombinedLogger<ArrangementRunService> logger
      ) {
         _container = container;
         _logger = logger;
      }

      public async Task RunAsync(Process process) {

         IProcessController controller;

         using (StackExchange.Profiling.MiniProfiler.Current.Step("Run.Prepare")) {
            controller = _container.CreateScope(process, _logger).Resolve<IProcessController>();
         }

         using (StackExchange.Profiling.MiniProfiler.Current.Step("Run.Execute")) {
            await controller.ExecuteAsync();
         }

         if (process.Errors().Any() || _logger.Log.Any(l => l.LogLevel == LogLevel.Error)) {
            process.Status = 500;
            process.Message = "Error";
         } else {
            process.Status = 200;
            process.Message = "Ok";
         }

         return;

      }
   }
}
