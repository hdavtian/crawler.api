using CrawlerWebApi.Interfaces;
using CrawlerWebApi.Models;
using IC.Test.Playwright.Crawler.Drivers;
using IC.Test.Playwright.Crawler.Models;
using IC.Test.Playwright.Crawler.Utility;
using System.Text;
using Microsoft.Extensions.Configuration;
using DiffPlex;
using System;
using IC.Test.Playwright.Crawler.Providers.Logger;
using IC.Test.Playwright.Crawler.Providers.Logger.Enums;

namespace CrawlerWebApi.Services
{
    public class DiffTestService : IDiffTestService
    {
        //private readonly CrawlTest CrawlTest;
        private readonly DiffTest DiffTest;
        private readonly DiffDriver DiffDriver;
        private readonly DiffContext DiffContext;
        private readonly CrawlArtifactManager CrawlArtifactManager;
        private readonly ILoggingProvider Logger;
        private readonly string SiteArtifactsWinPath;
        private readonly WriterQueueService WriterQueueService;

        public DiffTestService(
            DiffTest DiffTest,
            DiffDriver DiffDriver,
            DiffContext DiffContext,
            CrawlArtifactManager CrawlArtifactManager,
            IConfiguration AppConfiguration,
            WriterQueueService WriterQueueService,
            ILoggingProvider loggingProvider
            )
        {
            this.DiffTest = DiffTest;
            this.DiffDriver = DiffDriver;
            this.DiffContext = DiffContext;
            this.CrawlArtifactManager = CrawlArtifactManager;
            Logger = loggingProvider;
            SiteArtifactsWinPath = AppConfiguration["SiteArtifactsWinPath"];
            this.WriterQueueService = WriterQueueService;
        }
        public async Task<TestResult> RunDiffTestAsync(DiffTestPostRequestModel request)
        {
            try
            {
                Logger.Info("<<TestStarted>>");
                Logger.RaiseEvent(TaffieEventType.DiffTestStarted, "Diff test has  started");

                TimerUtil.StartTimer(DiffTest.Timers, "DiffDuration");

                string baseTestGuidStr = request.BaseTestId!.ToString();
                string newTestGuidStr = request.NewTestId!.ToString();

                var baseTestGuid = Guid.Parse(baseTestGuidStr);
                var newTestGuid = Guid.Parse(newTestGuidStr);

                var baseTest = await CrawlArtifactManager.GetCrawlTest(baseTestGuidStr);
                var newTest = await CrawlArtifactManager.GetCrawlTest(newTestGuidStr);

                // these paths will be used to copy some files later later
                DiffContext.BaseTestSavePath = baseTest.BaseSaveFolder;
                DiffContext.NewTestSavePath = newTest.BaseSaveFolder;

                // Set values for test model
                DiffTest.Name = "Diff test";
                DiffTest.BaseSaveFolder = PathUtil.CreateSavePath("diff-tests", DiffTest.Id.ToString());
                DiffTest.Description = $"Comparing baseline test '{DiffContext.BaseTestSavePath}' and new test '{DiffContext.NewTestSavePath}'";
                DiffTest.DateTime = DateTime.Now;
                DiffTest.BaseTest = baseTest;
                DiffTest.NewTest = newTest;

                // Page Screenshot Diff
                await DiffDriver.RunPageScreenshotDiffsOnTests(baseTestGuid, newTestGuid);

                // App Screenshot Diff
                await DiffDriver.RunAppDiffsOnTests(baseTestGuid, newTestGuid);

                // Stop timer
                TimerUtil.StopTimer(DiffTest.Timers, "DiffDuration");
                DiffTest.Duration = TimerUtil.GetElapsedTime(DiffTest.Timers, "DiffDuration");

                // Update diff manifest
                await WriterQueueService.EnqueueAsync(async () =>
                {
                    string diffTestsManifestFile = Path.Combine(SiteArtifactsWinPath, "diff-tests", "tests.json");
                    ReportWriter.UpdateJsonManifest(diffTestsManifestFile, DiffTest);
                });

                // write any final reports
                ReportWriter.SaveReport(DiffContext.DiffTestDiscrepancy, DiffTest.BaseSaveFolder, "missing-artifacts");

                // copy baseline and newtest manifest files to diff base save for easy access
                string sourceInfoFile = "test-info.json";
                await FileUtil.CopyFileAsync(DiffContext.BaseTestSavePath, DiffTest.BaseSaveFolder, sourceInfoFile, "baseline-test-info.json");
                await FileUtil.CopyFileAsync(DiffContext.NewTestSavePath, DiffTest.BaseSaveFolder, sourceInfoFile, "new-test-info.json");

                // log all timers
                var allTimings = TimerUtil.GetAllTimings(DiffTest.Timers);
                StringBuilder sb = new StringBuilder();
                sb.Append("\r\n==== Time Reports ====\r\n");
                foreach (var timing in allTimings)
                {
                    sb.Append($"- {timing.Key} took {timing.Value.TotalSeconds} seconds.\r\n");
                }
                Logger.Info(sb.ToString());

                // copy log to save path
                CopySpecflowLogToSavePath(DiffTest.BaseSaveFolder);

                // ***************************************
                Logger.Info("<<TestEnded>>");
                Logger.RaiseEvent(TaffieEventType.DiffTestEnded, "Diff test has ended");
                return new TestResult { Success = true, ErrorMessage = "Successfully completed diff test" };

            } 
            catch (Exception ex)
            {
                Logger.Info("<<TestError>>, <<TestEnded>>");
                Logger.RaiseEvent(TaffieEventType.DiffTestEnded, "Diff test has ended");
                Logger.Error(ex, "<<Error>> Unexpected error during diff test execution.");
                return new TestResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        private void CopySpecflowLogToSavePath(string savePath)
        {
            try
            {
                // copy specflow log file
                string LogFilePath = @"C:\temp";
                //string LogFileName = "specflow-console.log";
                string LogFileFullPath = Path.Combine(LogFilePath, DiffTest.LogFileName);
                string LogFileDestFullPath = Path.Combine(savePath, DiffTest.LogFileName);
                int maxRetries = 5;
                int delay = 1000; // milliseconds

                //await FileUtil.CopyFileAsync(LogFilePath, savePath, LogFileName);
                for (int retry = 0; retry < maxRetries; retry++)
                {
                    try
                    {
                        // Attempt to copy the log file
                        Logger.Info($"Will try to copy log file from '{LogFileFullPath}' to '{LogFileDestFullPath}'");
                        File.Copy(LogFileFullPath, LogFileDestFullPath, true);
                        Logger.Info("Log file was copied successfully");
                        break; // Exit loop if copy is successful
                    }
                    catch (IOException ex)
                    {
                        // Log the exception if needed
                        Logger.Error($"<<Error>> Failed to copy log file: {ex.Message}");

                        // Wait for the specified delay before retrying
                        Thread.Sleep(delay);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"<<Error>> Something went wrong trying to copy the log file. Error: {ex.Message}");
            }
        }
    }
}
