using CrawlerWebApi.Interfaces;
using CrawlerWebApi.Models;
using IC.Test.Playwright.Crawler.Drivers;
using IC.Test.Playwright.Crawler.Models;
using IC.Test.Playwright.Crawler.Utility;
using NLog;
using System.Text;
using Microsoft.Extensions.Configuration;
using DiffPlex;

namespace CrawlerWebApi.Services
{
    public class DiffTestService : IDiffTestService
    {
        private readonly TestModel _testModel;
        private readonly DiffDriver _diffDriver;
        private readonly DiffContext _diffContext;
        private readonly CrawlArtifacts _crawlArtifacts;
        private readonly Logger _logger;
        private readonly string _siteArtifactsWinPath;
        private readonly WriterQueueService _writerQueueService;

        public DiffTestService(
            TestModel testModel,
            DiffDriver diffDriver,
            DiffContext diffContext,
            CrawlArtifacts crawlArtifacts,
            IConfiguration configuration,
            WriterQueueService writerQueueService
            )
        {
            _testModel = testModel;
            _diffDriver = diffDriver;
            _diffContext = diffContext;
            _crawlArtifacts = crawlArtifacts;
            _logger = LogManager.GetCurrentClassLogger();
            _siteArtifactsWinPath = configuration["SiteArtifactsWinPath"];
            _writerQueueService = writerQueueService;
        }

        public async Task<TestResult> RunDiffTestAsync(DiffTestPostRequestModel request)
        {
            try
            {
                _logger.Info("<<TestStarted>>");

                TimerUtil.StartTimer(_testModel.Timers, "DiffDuration");

                // Get paths to save dirs for previously run tests
                _diffContext.BaseTestSavePath = PathUtil.ReplaceDoubleBackslashes(request.BaseTestPath);
                _diffContext.NewTestSavePath = PathUtil.ReplaceDoubleBackslashes(request.NewTestPath);

                // Set values for test model
                _testModel.Name = "Diff test";
                //string diffPathPartial = PathUtil.CreateSavePath("diff-tests", _testModel.Id.ToString());
                //_testModel.BaseSaveFolder = $"{diffPathPartial}__{_testModel.Id}";
                _testModel.BaseSaveFolder = PathUtil.CreateSavePath("diff-tests", _testModel.Id.ToString());
                _testModel.Description = $"Comparing baseline test '{_diffContext.BaseTestSavePath}' and new test '{_diffContext.NewTestSavePath}'";
                _testModel.DateTime = DateTime.Now;

                // IMPORTANT!!!!! remove this! For Testing only
                var guid1 = Guid.Parse("c5ca09f1-21da-4778-9c43-8705c1603dee");
                var guid2 = Guid.Parse("a790cb2b-cd3c-40d9-b29c-8e61f3a0b523");

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


                List<PageScreenshot> test1PageScreenshots = await _crawlArtifacts.GetPageScreenshots(guid1);
                List<PageScreenshot> test2PageScreenshots = await _crawlArtifacts.GetPageScreenshots(guid2);

                // - Loop through baseline test array
                //   Take first object.UrlModel.Title and search other test array for an object with match
                //   if no match then log and continue to next record
                //   If matched on 'Title' then see if it matches Width and Height and FileSize, log if they don't, either way compare

                PageScreenshot test2ps;

                _logger.Info("Starting page screenshot comparison(s)");

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
                        _logger.Info(logText.ToString());

                        // ------------------------------------------------
                        // Page Diff
                        // ------------------------------------------------
                        string img1 = Path.Combine(test1ps.Path, test1ps.FileName);
                        string img2 = Path.Combine(test2ps.Path, test2ps.FileName);
                        string diffImg = Path.Combine(_testModel.BaseSaveFolder, "page-screenshots", test1ps.FileName.Split("__")[0] + "__" + Guid.NewGuid() + ".png");
                        _diffDriver.DiffImages(img1, img2, diffImg, test1ps.UrlModel.Title, null);

                    } 
                    else
                    {
                        // did not match
                        // log an error
                        // @todo: add to errors array
                        _logger.Error($"Page screenshot of a page with title '{test1ps.UrlModel.Title}' was not found in new test, skipping comparison on this one ...");
                        continue;
                    }
                }

                string pageScreenshotPath = Path.Combine(_testModel.BaseSaveFolder, "page-screenshots");
                ReportWriter.SaveReport(_diffContext.PageScreenshotDiffs, pageScreenshotPath, "screenshot-diffs");

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

                List<AppArtifactManifest> test1AppArtifacts = await _crawlArtifacts.GetAppArtifacts(guid1);
                List<AppArtifactManifest> test2AppArtifacts = await _crawlArtifacts.GetAppArtifacts(guid2);

                AppArtifactManifest test2aa;

                _logger.Info("Starting app screenshot comparison(s)");

                foreach (AppArtifactManifest test1aa in test1AppArtifacts)
                {
                    StringBuilder logText = new StringBuilder();

                    string _appNameSlug;

                    if ((bool)test1aa.IsTabbedApp)
                    {
                        test2aa = test2AppArtifacts.FirstOrDefault(item => item.AppName.Equals(test1aa.AppName) && item.TabAppName.Equals(test1aa.TabAppName) && item.UrlModel.Title.Equals(test1aa.UrlModel.Title));
                        if (test2aa == null)
                        {
                            _logger.Error($"<<Error>>Screenshot for app '{test1aa.AppName}' > '{test1aa.TabAppName}' on page '{test1aa.UrlModel.Title}' was found on baseline but not the new test, skipping comparison on this one ...");
                            continue;
                        }
                        _appNameSlug = test1aa.AppName + "__" + test1aa.TabAppName + "__" + test1aa.Id;
                    }
                    else
                    {
                        test2aa = test2AppArtifacts.FirstOrDefault(item => item.AppName.Equals(test1aa.AppName) && item.UrlModel.Title.Equals(test1aa.UrlModel.Title));
                        if (test2aa == null)
                        {
                            _logger.Error($"<<Error>>Screenshot for app '{test1aa.AppName}' on page '{test1aa.UrlModel.Title}' was found on baseline but not the new test, skipping comparison on this one ...");
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

                    _logger.Info(logText.ToString());

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

                    _logger.Info(logText.ToString());

                    // ----------------------
                    // Diff App screenshots
                    // ----------------------
                    string img1 = Path.Combine(test1aa.ScreenshotSavePath, test1aa.ScreenshotFileName);
                    string img2 = Path.Combine(test2aa.ScreenshotSavePath, test2aa.ScreenshotFileName);
                    string diffImg = Path.Combine(_testModel.BaseSaveFolder, "app-screenshots", _appNameSlug + ".png");
                    _diffDriver.DiffImages(img1, img2, diffImg, test1aa.UrlModel.Title, test1aa.AppName);

                    // ----------------------
                    // Diff App html
                    // ---------------------- 
                    AppHtml app1Html = await _crawlArtifacts.GetAppHTML(guid1, test1aa.HtmlFileName);
                    AppHtml app2Html = await _crawlArtifacts.GetAppHTML(guid2, test2aa.HtmlFileName);
                    string DiffSavePath = Path.Combine(_testModel.BaseSaveFolder, "app-html");
                    await _diffDriver.DiffHtml(app1Html, app2Html, DiffSavePath, _appNameSlug + ".html", true);

                    // ----------------------
                    // Diff App text
                    // ---------------------- 
                    AppText app1Text = await _crawlArtifacts.GetAppText(guid1, test1aa.TextFileName);
                    AppText app2Text = await _crawlArtifacts.GetAppText(guid2, test2aa.TextFileName);
                    string TextDiffSavePath = Path.Combine(_testModel.BaseSaveFolder, "app-html");
                    await _diffDriver.DiffText(app1Text, app2Text, TextDiffSavePath, _appNameSlug + ".html", false);
                }

                ReportWriter.SaveReport(_diffContext.AppScreenshotDiffs, Path.Combine(_testModel.BaseSaveFolder, "app-screenshots"), "screenshot-diffs");
                ReportWriter.SaveReport(_diffContext.AppHtmlDiffs, Path.Combine(_testModel.BaseSaveFolder, "app-html"), "app-markup-diffs");
                ReportWriter.SaveReport(_diffContext.AppTextDiffs, Path.Combine(_testModel.BaseSaveFolder, "app-text"), "app-text-diffs");


                /*

                // ----------------------------------------------------
                // Compare Page screenshots and create diffs
                // ----------------------------------------------------
                string imageDir = "page-screenshots";
                string BaseTestImgPath = Path.Combine(_diffContext.BaseTestSavePath, imageDir);
                string NewTestImgPath = Path.Combine(_diffContext.NewTestSavePath, imageDir);
                string DiffTestImgPath = Path.Combine(_testModel.BaseSaveFolder, imageDir);
                _diffDriver.DiffImgDirs(BaseTestImgPath, NewTestImgPath, DiffTestImgPath);
                string pageScreenshotPath = Path.Combine(_testModel.BaseSaveFolder, imageDir);
                ReportWriter.SaveReport(_diffContext.ScreenshotDiffs, pageScreenshotPath, "screenshot-diffs");

                // ----------------------------------------------------
                // Compare App screenshots and create diffs
                // ----------------------------------------------------
                imageDir = "app-screenshots";
                BaseTestImgPath = Path.Combine(_diffContext.BaseTestSavePath, imageDir);
                NewTestImgPath = Path.Combine(_diffContext.NewTestSavePath, imageDir);
                DiffTestImgPath = Path.Combine(_testModel.BaseSaveFolder, imageDir);
                _diffDriver.DiffImgDirs(BaseTestImgPath, NewTestImgPath, DiffTestImgPath);
                string appScreenshotPath = Path.Combine(_testModel.BaseSaveFolder, imageDir);
                ReportWriter.SaveReport(_diffContext.ScreenshotDiffs, appScreenshotPath, "screenshot-diffs");

                // ----------------------------------------------------
                // Compare App Html markup and create reports
                // ----------------------------------------------------
                string dotFileType = ".html";
                string subDirPath = "app-html";
                string jsonFile = "app-html.json";
                bool ignoreTextContent = true;
                string baseJsonFilePath = Path.Combine(_diffContext.BaseTestSavePath, jsonFile);
                string newJsonFilePath = Path.Combine(_diffContext.NewTestSavePath, jsonFile);
                string DiffSavePath = Path.Combine(_testModel.BaseSaveFolder, subDirPath);
                FileInfo _baseJsonFile = new FileInfo(baseJsonFilePath);
                FileInfo _newJsonFile = new FileInfo(newJsonFilePath);

                await _diffDriver.DiffHtmlFiles(_baseJsonFile, _newJsonFile, DiffSavePath, dotFileType, ignoreTextContent);

                // ----------------------------------------------------
                // Compare App text and create reports
                // ----------------------------------------------------
                dotFileType = ".txt";
                subDirPath = "app-innertext";
                jsonFile = "app-text.json";
                ignoreTextContent = false;
                baseJsonFilePath = Path.Combine(_diffContext.BaseTestSavePath, jsonFile);
                newJsonFilePath = Path.Combine(_diffContext.NewTestSavePath, jsonFile);
                DiffSavePath = Path.Combine(_testModel.BaseSaveFolder, subDirPath);
                _baseJsonFile = new FileInfo(baseJsonFilePath);
                _newJsonFile = new FileInfo(newJsonFilePath);
                await _diffDriver.DiffTextFiles(_baseJsonFile, _newJsonFile, DiffSavePath, ignoreTextContent);

                */


                // stop timer
                TimerUtil.StopTimer(_testModel.Timers, "DiffDuration");
                _testModel.Duration = TimerUtil.GetElapsedTime(_testModel.Timers, "DiffDuration");

                // Update diff manifest
                await _writerQueueService.EnqueueAsync(async () =>
                {
                    string diffTestsManifestFile = Path.Combine(_siteArtifactsWinPath, "diff-tests", "tests.json");
                    ReportWriter.UpdateJsonManifest(diffTestsManifestFile, _testModel);
                });

                // copy baseline and newtest manifest files to diff base save for easy access
                string sourceInfoFile = "test-info.json";
                await FileUtil.CopyFileAsync(_diffContext.BaseTestSavePath, _testModel.BaseSaveFolder, sourceInfoFile, "baseline-test-info.json");
                await FileUtil.CopyFileAsync(_diffContext.NewTestSavePath, _testModel.BaseSaveFolder, sourceInfoFile, "new-test-info.json");

                // log all timers
                var allTimings = TimerUtil.GetAllTimings(_testModel.Timers);
                StringBuilder sb = new StringBuilder();
                sb.Append("\r\n==== Time Reports ====\r\n");
                foreach (var timing in allTimings)
                {
                    sb.Append($"- {timing.Key} took {timing.Value.TotalSeconds} seconds.\r\n");
                }
                _logger.Info(sb.ToString());

                // copy log to save path
                CopySpecflowLogToSavePath(_testModel.BaseSaveFolder);

                // ***************************************
                _logger.Info("<<TestEnded>>");
                return new TestResult { Success = true, ErrorMessage = "Successfully completed diff test" };

            } catch (Exception ex)
            {
                _logger.Info("<<TestError>>, <<TestEnded>>");
                _logger.Error(ex, "<<Error>> Unexpected error during diff test execution.");
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
                string LogFileFullPath = Path.Combine(LogFilePath, _testModel.LogFileName);
                string LogFileDestFullPath = Path.Combine(savePath, _testModel.LogFileName);
                int maxRetries = 5;
                int delay = 1000; // milliseconds

                //await FileUtil.CopyFileAsync(LogFilePath, savePath, LogFileName);
                for (int retry = 0; retry < maxRetries; retry++)
                {
                    try
                    {
                        // Attempt to copy the log file
                        _logger.Info($"Will try to copy log file from '{LogFileFullPath}' to '{LogFileDestFullPath}'");
                        File.Copy(LogFileFullPath, LogFileDestFullPath, true);
                        _logger.Info("Log file was copied successfully");
                        break; // Exit loop if copy is successful
                    }
                    catch (IOException ex)
                    {
                        // Log the exception if needed
                        _logger.Error($"<<Error>> Failed to copy log file: {ex.Message}");

                        // Wait for the specified delay before retrying
                        Thread.Sleep(delay);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"<<Error>> Something went wrong trying to copy the log file. Error: {ex.Message}");
            }
        }
    }
}
