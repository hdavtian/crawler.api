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
        private readonly CrawlTest CrawlTest;
        private readonly DiffDriver DiffDriver;
        private readonly DiffContext DiffContext;
        private readonly CrawlArtifacts CrawlArtifacts;
        private readonly Logger Logger;
        private readonly string SiteArtifactsWinPath;
        private readonly WriterQueueService WriterQueueService;

        public DiffTestService(
            CrawlTest CrawlTest,
            DiffDriver DiffDriver,
            DiffContext DiffContext,
            CrawlArtifacts CrawlArtifacts,
            IConfiguration AppConfiguration,
            WriterQueueService WriterQueueService
            )
        {
            this.CrawlTest = CrawlTest;
            this.DiffDriver = DiffDriver;
            this.DiffContext = DiffContext;
            this.CrawlArtifacts = CrawlArtifacts;
            Logger = LogManager.GetCurrentClassLogger();
            SiteArtifactsWinPath = AppConfiguration["SiteArtifactsWinPath"];
            this.WriterQueueService = WriterQueueService;
        }

        public async Task<TestResult> RunDiffTestAsync(DiffTestPostRequestModel request)
        {
            try
            {
                Logger.Info("<<TestStarted>>");

                TimerUtil.StartTimer(CrawlTest.Timers, "DiffDuration");

                string baseTestGuidStr = request.BaseTestId!.ToString();
                string newTestGuidStr = request.NewTestId!.ToString();

                var baseTestGuid = Guid.Parse(baseTestGuidStr);
                var newTestGuid = Guid.Parse(newTestGuidStr);

                var baseTest = await CrawlArtifacts.GetCrawlTestAsync(baseTestGuidStr);
                var newTest = await CrawlArtifacts.GetCrawlTestAsync(newTestGuidStr);

                // these paths will be used to copy some files later later
                DiffContext.BaseTestSavePath = baseTest.BaseSaveFolder;
                DiffContext.NewTestSavePath = newTest.BaseSaveFolder;

                // Set values for test model
                CrawlTest.Name = "Diff test";
                CrawlTest.BaseSaveFolder = PathUtil.CreateSavePath("diff-tests", CrawlTest.Id.ToString());
                CrawlTest.Description = $"Comparing baseline test '{DiffContext.BaseTestSavePath}' and new test '{DiffContext.NewTestSavePath}'";
                CrawlTest.DateTime = DateTime.Now;

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

                // New page diffing strategy

                // Operation 1: Selecting the correct two files for comparison
                // First we need to look through files and make sure we select the right two
                // images between the two tests to compare with each other
                //
                // - Load and create objects from 'page-screenshots.json' for both tests and put in arrays

                List<PageScreenshot> test1PageScreenshots = await CrawlArtifacts.GetPageScreenshots(baseTestGuid);
                List<PageScreenshot> test2PageScreenshots = await CrawlArtifacts.GetPageScreenshots(newTestGuid);

                // - Loop through baseline test array
                //   Take first object.UrlModel.Title and search other test array for an object with match
                //   if no match then log and continue to next record
                //   If matched on 'Title' then see if it matches Width and Height and FileSize, log if they don't, either way compare

                PageScreenshot test2ps;

                Logger.Info("Starting page screenshot comparison(s)");

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
                            logText.AppendLine($"Both screenshots have the same height: '{test1ps.FileSize}'");
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
                        // Page Diff
                        // ------------------------------------------------
                        string img1 = Path.Combine(test1ps.Path, test1ps.FileName);
                        string img2 = Path.Combine(test2ps.Path, test2ps.FileName);
                        string diffImg = Path.Combine(CrawlTest.BaseSaveFolder, "page-screenshots", test1ps.FileName.Split("__")[0] + "__" + Guid.NewGuid() + ".png");
                        DiffDriver.DiffImages(img1, img2, diffImg, test1ps.UrlModel.Title, null);

                    } 
                    else
                    {
                        // did not match
                        // log an error
                        // @todo: add to errors array
                        Logger.Error($"Page screenshot of a page with title '{test1ps.UrlModel.Title}' was not found in new test, skipping comparison on this one ...");
                        continue;
                    }
                }

                string pageScreenshotPath = Path.Combine(CrawlTest.BaseSaveFolder, "page-screenshots");
                ReportWriter.SaveReport(DiffContext.PageScreenshotDiffs, pageScreenshotPath, "screenshot-diffs");

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

                List<AppArtifactManifest> test1AppArtifacts = await CrawlArtifacts.GetAppArtifacts(baseTestGuid);
                List<AppArtifactManifest> test2AppArtifacts = await CrawlArtifacts.GetAppArtifacts(newTestGuid);

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
                            Logger.Error($"<<Error>>Screenshot for app '{test1aa.AppName}' > '{test1aa.TabApp.UniqueTabName}' on page '{test1aa.UrlModel.Title}' was found on baseline but not the new test, skipping comparison on this one ...");
                            continue;
                        }
                        _appNameSlug = test1aa.AppName + "__" + test1aa.TabApp.UniqueTabName + "__" + test1aa.Id;
                    }
                    else
                    {
                        test2aa = test2AppArtifacts.FirstOrDefault(item => item.AppName.Equals(test1aa.AppName) && item.UrlModel.Title.Equals(test1aa.UrlModel.Title));
                        if (test2aa == null)
                        {
                            Logger.Error($"<<Error>>Screenshot for app '{test1aa.AppName}' on page '{test1aa.UrlModel.Title}' was found on baseline but not the new test, skipping comparison on this one ...");
                            continue;
                        }
                        _appNameSlug = test1aa.AppName + "__" + test1aa.Id;
                    }

                    // 'AppName' and 'Title' (Page title) matched
                    logText.AppendLine($"Screenshot for app '{test1aa.AppName}' on page '{test1aa.UrlModel.Title}' was found on both tests");

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
                    string diffImg = Path.Combine(CrawlTest.BaseSaveFolder, "app-screenshots", _appNameSlug + ".png");
                    DiffDriver.DiffImages(img1, img2, diffImg, test1aa.UrlModel.Title, test1aa.AppName);

                    // ----------------------
                    // Diff App html
                    // ---------------------- 
                    AppHtml app1Html = await CrawlArtifacts.GetAppHTML(baseTestGuid, test1aa.HtmlFileName);
                    AppHtml app2Html = await CrawlArtifacts.GetAppHTML(newTestGuid, test2aa.HtmlFileName);
                    string DiffSavePath = Path.Combine(CrawlTest.BaseSaveFolder, "app-html");
                    await DiffDriver.DiffHtml(app1Html, app2Html, DiffSavePath, _appNameSlug + ".html", true);

                    // ----------------------
                    // Diff App text
                    // ---------------------- 
                    AppText app1Text = await CrawlArtifacts.GetAppText(baseTestGuid, test1aa.TextFileName);
                    AppText app2Text = await CrawlArtifacts.GetAppText(newTestGuid, test2aa.TextFileName);
                    string TextDiffSavePath = Path.Combine(CrawlTest.BaseSaveFolder, "app-html");
                    await DiffDriver.DiffText(app1Text, app2Text, TextDiffSavePath, _appNameSlug + ".html", false);
                }

                ReportWriter.SaveReport(DiffContext.AppScreenshotDiffs, Path.Combine(CrawlTest.BaseSaveFolder, "app-screenshots"), "screenshot-diffs");
                ReportWriter.SaveReport(DiffContext.AppHtmlDiffs, Path.Combine(CrawlTest.BaseSaveFolder, "app-html"), "app-markup-diffs");
                ReportWriter.SaveReport(DiffContext.AppTextDiffs, Path.Combine(CrawlTest.BaseSaveFolder, "app-text"), "app-text-diffs");


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
                TimerUtil.StopTimer(CrawlTest.Timers, "DiffDuration");
                CrawlTest.Duration = TimerUtil.GetElapsedTime(CrawlTest.Timers, "DiffDuration");

                // Update diff manifest
                await WriterQueueService.EnqueueAsync(async () =>
                {
                    string diffTestsManifestFile = Path.Combine(SiteArtifactsWinPath, "diff-tests", "tests.json");
                    ReportWriter.UpdateJsonManifest(diffTestsManifestFile, CrawlTest);
                });

                // copy baseline and newtest manifest files to diff base save for easy access
                string sourceInfoFile = "test-info.json";
                await FileUtil.CopyFileAsync(DiffContext.BaseTestSavePath, CrawlTest.BaseSaveFolder, sourceInfoFile, "baseline-test-info.json");
                await FileUtil.CopyFileAsync(DiffContext.NewTestSavePath, CrawlTest.BaseSaveFolder, sourceInfoFile, "new-test-info.json");

                // log all timers
                var allTimings = TimerUtil.GetAllTimings(CrawlTest.Timers);
                StringBuilder sb = new StringBuilder();
                sb.Append("\r\n==== Time Reports ====\r\n");
                foreach (var timing in allTimings)
                {
                    sb.Append($"- {timing.Key} took {timing.Value.TotalSeconds} seconds.\r\n");
                }
                Logger.Info(sb.ToString());

                // copy log to save path
                CopySpecflowLogToSavePath(CrawlTest.BaseSaveFolder);

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
                string LogFileFullPath = Path.Combine(LogFilePath, CrawlTest.LogFileName);
                string LogFileDestFullPath = Path.Combine(savePath, CrawlTest.LogFileName);
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
