using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.Server;
using VirtoCommerce.DescriptionExportImportModule.Core.Models;
using VirtoCommerce.DescriptionExportImportModule.Core.Services;
using VirtoCommerce.DescriptionExportImportModule.Data.Helpers;
using VirtoCommerce.Platform.Core.Exceptions;
using VirtoCommerce.Platform.Core.PushNotifications;
using VirtoCommerce.Platform.Hangfire;

namespace VirtoCommerce.DescriptionExportImportModule.Web.BackgroundJobs
{
    public class ImportJob
    {
        private readonly IPushNotificationManager _pushNotificationManager;
        private readonly IEnumerable<ICsvPagedDataImporter> _dataImporters;

        public ImportJob(IPushNotificationManager pushNotificationManager, IEnumerable<ICsvPagedDataImporter> dataImporters)
        {
            _pushNotificationManager = pushNotificationManager;
            _dataImporters = dataImporters;
        }

        public async Task ImportBackgroundAsync(ImportDataRequest request, ImportPushNotification pushNotification, IJobCancellationToken jobCancellationToken, PerformContext context)
        {
            ValidateParameters(request, pushNotification);

            try
            {
                var importer = _dataImporters.First(x => x.MemberType == request.DataType);

                await importer.ImportAsync(request,
                    progressInfo => ProgressCallback(progressInfo, pushNotification, context),
                    new JobCancellationTokenWrapper(jobCancellationToken));
            }
            catch (JobAbortedException)
            {
                // job is aborted, do nothing
            }
            catch (Exception ex)
            {
                pushNotification.Errors.Add(ex.ExpandExceptionMessage());
            }
            finally
            {
                pushNotification.Description = "Import finished";
                pushNotification.Finished = DateTime.UtcNow;

                await _pushNotificationManager.SendAsync(pushNotification);
            }
        }

        private void ProgressCallback(ImportProgressInfo x, ImportPushNotification pushNotification, PerformContext context)
        {
            pushNotification.Patch(x);
            pushNotification.JobId = context.BackgroundJob.Id;
            _pushNotificationManager.Send(pushNotification);
        }

        private void ValidateParameters(ImportDataRequest request, ImportPushNotification pushNotification)
        {
            if (pushNotification == null)
            {
                throw new ArgumentNullException(nameof(pushNotification));
            }

            var importer = _dataImporters.FirstOrDefault(x => x.MemberType == request.DataType);

            if (importer == null)
            {
                throw new ArgumentException($"Not allowed argument value in field {nameof(request.DataType)}", nameof(request));
            }
        }
    }
}
