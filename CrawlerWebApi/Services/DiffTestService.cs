using CrawlerWebApi.Interfaces;
using CrawlerWebApi.Models;
using IC.Test.Playwright.Crawler.Drivers;
using IC.Test.Playwright.Crawler.Models;
using IC.Test.Playwright.Crawler.Utility;
using NLog;
using System.Text;
using Microsoft.Extensions.Configuration;
using DiffPlex;
using System;

namespace CrawlerWebApi.Services
{
    public class DiffTestService : IDiffTestService
    {
        //private readonly CrawlTest CrawlTest;
        private readonly DiffTest DiffTest;
        private readonly DiffDriver DiffDriver;
        private readonly DiffContext DiffContext;
        private readonly CrawlArtifactManager CrawlArtifactManager;
        private readonly Logger Logger;
        private readonly string SiteArtifactsWinPath;
        private readonly WriterQueueService WriterQueueService;

        public DiffTestService(
            DiffTest DiffTest,
            DiffDriver DiffDriver,
            DiffContext DiffContext,
            CrawlArtifactManager CrawlArtifactManager,
            IConfiguration AppConfiguration,
            WriterQueueService WriterQueueService
            )
        {
            this.DiffTest = DiffTest;
            this.DiffDriver = DiffDriver;
            this.DiffContext = DiffContext;
            this.CrawlArtifactManager = CrawlArtifactManager;
            Logger = LogManager.GetCurrentClassLogger();
            SiteArtifactsWinPath = AppConfiguration["SiteArtifactsWinPath"];
            this.WriterQueueService = WriterQueueService;
        }
        public async Task<TestResult> RunDiffTestAsync(DiffTestPostRequestModel request)
        {
            try
            {
                Logger.Info("<<TestStarted>>");

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

                // -
                // --
                // ---
                // ----
                // -----
                // Page Screenshot Diff
                // -----
                // ----
                // ---
                // --
                // -

                List<PageScreenshot> test1PageScreenshots = await CrawlArtifactManager.GetPageScreenshots(baseTestGuid);
                List<PageScreenshot> test2PageScreenshots = await CrawlArtifactManager.GetPageScreenshots(newTestGuid);

                PageScreenshot test2ps;

                Logger.Info("Starting page screenshot comparison(s)");
                DiffContext.DiffTestTotals.PageScreenshotsDiffTotal = 0;

                foreach (PageScreenshot test1ps in test1PageScreenshots)
                {
                    test2ps = test2PageScreenshots.FirstOrDefault(item => item.UrlModel.Title.Equals(test1ps.UrlModel.Title));

                    if (test2ps != null) {
                        // titles matched
                        StringBuilder logText = new StringBuilder();
                        logText.AppendLine($"Page screenshot of a page with title '{test1ps.UrlModel.Title}' was found in both tests");

                        // compare file sizes and log
                        if (test1ps.FileSize == test2ps.FileSize)
                        {
                            logText.AppendLine($"Both screenshots have the same file size: '{test1ps.FileSize}'");
                        }
                        else
                        {
                            logText.AppendLine($"Screenshots have different files sizes, test1 file size:'{test1ps.FileSize}', test2 file size: '{test2ps.FileSize}'");
                        }

                        // compare widths and log
                        if (test1ps.Width == test2ps.Width)
                        {
                            logText.AppendLine($"Both screenshots have the same width: '{test1ps.Width}'");
                        } else
                        {
                            logText.AppendLine($"Screenshots have different widths, test1 width:'{test1ps.Width}', test2 width: '{test2ps.Width}'");
                        }

                        // compare heights and log
                        if (test1ps.Height == test2ps.Height)
                        {
                            logText.AppendLine($"Both screenshots have the same height: '{test1ps.Height}'");
                        }
                        else
                        {
                            logText.AppendLine($"Screenshots have different heights, test1 height:'{test1ps.Height}', test2 height: '{test2ps.Height}'");
                        }
                        Logger.Info(logText.ToString());

                        // ------------------------------------------------
                        // PageScreenshot Diff
                        // ------------------------------------------------
                        string img1 = Path.Combine(test1ps.Path, test1ps.FileName);
                        string img2 = Path.Combine(test2ps.Path, test2ps.FileName);
                        string diffImg = Path.Combine(DiffTest.BaseSaveFolder, "page-screenshots", test1ps.FileName.Split("__")[0] + "__" + Guid.NewGuid() + ".png");
                        DiffDriver.DiffImages(img1, img2, diffImg, test1ps.UrlModel.Title, null);

                    } 
                    else
                    {
                        // did not match
                        // log an error
                        // @todo: add to errors array
                        Logger.Error($"Page screenshot of a page with title '{test1ps.UrlModel.Title}' was not found in new test, skipping comparison on this one ...");
                        DiffContext.DiffTestTotals.PageScreenshotsDiffTotal++;
                        continue;
                    }
                }

                string pageScreenshotPath = Path.Combine(DiffTest.BaseSaveFolder, "page-screenshots");
                DiffTest.DiffTestTotals.PageScreenshotsDiffTotal = DiffContext.DiffTestTotals.PageScreenshotsDiffTotal;

                ReportWriter.SaveReport(DiffContext.PageScreenshots, pageScreenshotPath, "screenshots");

                // -
                // --
                // ---
                // ----
                // -----
                // App Screenshot Diff
                // -----
                // ----
                // ---
                // --
                // -

                DiffContext.DiffTestTotals.AppScreenshotsDiffTotal = 0;
                DiffContext.DiffTestTotals.AppHtmlDiffTotal = 0;
                DiffContext.DiffTestTotals.AppTextDiffTotal = 0;

                List<AppArtifactManifest> test1AppArtifacts = await CrawlArtifactManager.GetAppArtifacts(baseTestGuid);
                List<AppArtifactManifest> test2AppArtifacts = await CrawlArtifactManager.GetAppArtifacts(newTestGuid);

                AppArtifactManifest test2aa;

                Logger.Info("Starting app screenshot comparison(s)");

                foreach (AppArtifactManifest test1aa in test1AppArtifacts)
                {
                    StringBuilder logText = new StringBuilder();

                    string _appNameSlug;

                    if ((bool)test1aa.IsTabbedApp)
                    {
                        test2aa = test2AppArtifacts.FirstOrDefault(item => item.AppName.Equals(test1aa.AppName) && item.TabApp.UniqueTabName.Equals(test1aa.TabApp.UniqueTabName) && item.UrlModel.Title.Equals(test1aa.UrlModel.Title));
                        if (test2aa == null)
                        {
                            Logger.Error($"<<Error>>App '{test1aa.AppName}' > '{test1aa.TabApp.UniqueTabName}' on page '{test1aa.UrlModel.Title}' was found on baseline but not the new test, skipping comparison on this one ...");

                            DiffContext.DiffTestTotals.AppScreenshotsDiffTotal++;
                            DiffContext.DiffTestTotals.AppHtmlDiffTotal++;
                            DiffContext.DiffTestTotals.AppTextDiffTotal++;
                            continue;
                        }
                        _appNameSlug = test1aa.AppName + "__" + test1aa.TabApp.UniqueTabName + "__" + test1aa.Id;
                    }
                    else
                    {
                        test2aa = test2AppArtifacts.FirstOrDefault(item => item.AppName.Equals(test1aa.AppName) && item.UrlModel.Title.Equals(test1aa.UrlModel.Title));
                        if (test2aa == null)
                        {
                            Logger.Error($"<<Error>>App '{test1aa.AppName}' on page '{test1aa.UrlModel.Title}' was found on baseline but not the new test, skipping comparison on this one ...");

                            DiffContext.DiffTestTotals.AppScreenshotsDiffTotal++;
                            DiffContext.DiffTestTotals.AppHtmlDiffTotal++;
                            DiffContext.DiffTestTotals.AppTextDiffTotal++;
                            continue;
                        }
                        _appNameSlug = test1aa.AppName + "__" + test1aa.Id;
                    }

                    // 'AppName' and 'Title' (Page title) matched
                    logText.AppendLine($"App '{test1aa.AppName}' on page '{test1aa.UrlModel.Title}' was found on both tests");

                    // Compare dimensions
                    if (test1aa.Width == test2aa.Width && test1aa.Height == test2aa.Height)
                    {
                        logText.AppendLine($"Web element on both tests had the same dimensions ('{test1aa.Width}px' w x '{test1aa.Height}'px h)");
                    }
                    else
                    {
                        logText.AppendLine($"Web element had different dimensions from test to test");
                        logText.AppendLine($"Baseline Test: width: '{test1aa.Width}px' x height: '{test1aa.Height}'px");
                        logText.AppendLine($"New Test: width: '{test2aa.Width}px' x height: '{test2aa.Height}'px");
                    }

                    Logger.Info(logText.ToString());

                    // Compare position
                    if (test1aa.Top == test2aa.Top && test1aa.Left == test2aa.Left)
                    {
                        logText.AppendLine($"Web element on both tests had the same position: Top: '{test1aa.Top}px', Left: '{test1aa.Left}px'");
                    }
                    else
                    {
                        logText.AppendLine($"Web element had different positions from test to test");
                        logText.AppendLine($"Baseline Test: Top: '{test1aa.Top}px', Left: '{test1aa.Left}px'");
                        logText.AppendLine($"New Test: Top: '{test2aa.Top}px', Left: '{test2aa.Left}px'");
                    }

                    Logger.Info(logText.ToString());

                    // ----------------------
                    // Diff App screenshots
                    // ----------------------
                    string img1 = Path.Combine(test1aa.ScreenshotSavePath, test1aa.ScreenshotFileName);
                    string img2 = Path.Combine(test2aa.ScreenshotSavePath, test2aa.ScreenshotFileName);
                    string diffImg = Path.Combine(DiffTest.BaseSaveFolder, "app-screenshots", _appNameSlug + ".png");
                    ScreenshotDiff ssDiff = DiffDriver.DiffImages(img1, img2, diffImg, test1aa.UrlModel.Title, test1aa.AppName);

                    // ----------------------
                    // Diff App html
                    // ---------------------- 
                    AppHtml app1Html = await CrawlArtifactManager.GetAppHTML(baseTestGuid, test1aa.HtmlFileName);
                    AppHtml app2Html = await CrawlArtifactManager.GetAppHTML(newTestGuid, test2aa.HtmlFileName);
                    string DiffSavePath = Path.Combine(DiffTest.BaseSaveFolder, "app-html");
                    AppHtmlDiff appHtmlDiff = await DiffDriver.DiffHtml(app1Html, app2Html, DiffSavePath, _appNameSlug + ".html", true);

                    // ----------------------
                    // Diff App text
                    // ---------------------- 
                    AppText app1Text = await CrawlArtifactManager.GetAppText(baseTestGuid, test1aa.TextFileName);
                    AppText app2Text = await CrawlArtifactManager.GetAppText(newTestGuid, test2aa.TextFileName);
                    string TextDiffSavePath = Path.Combine(DiffTest.BaseSaveFolder, "app-html");
                    AppTextDiff appTextDiff = await DiffDriver.DiffText(app1Text, app2Text, TextDiffSavePath, _appNameSlug + ".html", false);

                    var _appDiffs = new AppDiffs()
                    {
                        ScreenshotDiff = ssDiff,
                        AppHtmlDiff = appHtmlDiff,
                        AppTextDiff = appTextDiff
                    };
                    DiffContext.AppDiffs.Add(_appDiffs);
                }

                // Capturing totals to include in DiffTest
                DiffTest.DiffTestTotals = DiffContext.DiffTestTotals;

                ReportWriter.SaveReport(DiffContext.AppScreenshots, Path.Combine(DiffTest.BaseSaveFolder, "app-screenshots"), "screenshots");
                ReportWriter.SaveReport(DiffContext.AppHtmls, Path.Combine(DiffTest.BaseSaveFolder, "app-html"), "app-html");
                ReportWriter.SaveReport(DiffContext.AppTexts, Path.Combine(DiffTest.BaseSaveFolder, "app-text"), "app-text");
                ReportWriter.SaveReport(DiffContext.AppDiffs, DiffTest.BaseSaveFolder, "app-diffs");


                /*

                // ----------------------------------------------------
                // Compare Page screenshots and create diffs
                // ----------------------------------------------------
                string imageDir = "page-screenshots";
                string BaseTestImgPath = Path.Combine(DiffContext.BaseTestSavePath, imageDir);
                string NewTestImgPath = Path.Combine(DiffContext.NewTestSavePath, imageDir);
                string DiffTestImgPath = Path.Combine(CrawlTest.BaseSaveFolder, imageDir);
                DiffDriver.DiffImgDirs(BaseTestImgPath, NewTestImgPath, DiffTestImgPath);
                string pageScreenshotPath = Path.Combine(CrawlTest.BaseSaveFolder, imageDir);
                ReportWriter.SaveReport(DiffContext.ScreenshotDiffs, pageScreenshotPath, "screenshot-diffs");

                // ----------------------------------------------------
                // Compare App screenshots and create diffs
                // ----------------------------------------------------
                imageDir = "app-screenshots";
                BaseTestImgPath = Path.Combine(DiffContext.BaseTestSavePath, imageDir);
                NewTestImgPath = Path.Combine(DiffContext.NewTestSavePath, imageDir);
                DiffTestImgPath = Path.Combine(CrawlTest.BaseSaveFolder, imageDir);
                DiffDriver.DiffImgDirs(BaseTestImgPath, NewTestImgPath, DiffTestImgPath);
                string appScreenshotPath = Path.Combine(CrawlTest.BaseSaveFolder, imageDir);
                ReportWriter.SaveReport(DiffContext.ScreenshotDiffs, appScreenshotPath, "screenshot-diffs");

                // ----------------------------------------------------
                // Compare App Html markup and create reports
                // ----------------------------------------------------
                string dotFileType = ".html";
                string subDirPath = "app-html";
                string jsonFile = "app-html.json";
                bool ignoreTextContent = true;
                string baseJsonFilePath = Path.Combine(DiffContext.BaseTestSavePath, jsonFile);
                string newJsonFilePath = Path.Combine(DiffContext.NewTestSavePath, jsonFile);
                string DiffSavePath = Path.Combine(CrawlTest.BaseSaveFolder, subDirPath);
                FileInfo _baseJsonFile = new FileInfo(baseJsonFilePath);
                FileInfo _newJsonFile = new FileInfo(newJsonFilePath);

                await DiffDriver.DiffHtmlFiles(_baseJsonFile, _newJsonFile, DiffSavePath, dotFileType, ignoreTextContent);

                // ----------------------------------------------------
                // Compare App text and create reports
                // ----------------------------------------------------
                dotFileType = ".txt";
                subDirPath = "app-innertext";
                jsonFile = "app-text.json";
                ignoreTextContent = false;
                baseJsonFilePath = Path.Combine(DiffContext.BaseTestSavePath, jsonFile);
                newJsonFilePath = Path.Combine(DiffContext.NewTestSavePath, jsonFile);
                DiffSavePath = Path.Combine(CrawlTest.BaseSaveFolder, subDirPath);
                _baseJsonFile = new FileInfo(baseJsonFilePath);
                _newJsonFile = new FileInfo(newJsonFilePath);
                await DiffDriver.DiffTextFiles(_baseJsonFile, _newJsonFile, DiffSavePath, ignoreTextContent);

                */


                // stop timer
                TimerUtil.StopTimer(DiffTest.Timers, "DiffDuration");
                DiffTest.Duration = TimerUtil.GetElapsedTime(DiffTest.Timers, "DiffDuration");

                // Update diff manifest
                await WriterQueueService.EnqueueAsync(async () =>
                {
                    string diffTestsManifestFile = Path.Combine(SiteArtifactsWinPath, "diff-tests", "tests.json");
                    ReportWriter.UpdateJsonManifest(diffTestsManifestFile, DiffTest);
                });

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
                return new TestResult { Success = true, ErrorMessage = "Successfully completed diff test" };

            } catch (Exception ex)
            {
                Logger.Info("<<TestError>>, <<TestEnded>>");
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
