using CrawlerWebApi.Interfaces;
using CrawlerWebApi.Models;
using IC.Test.Playwright.Crawler.Drivers;
using IC.Test.Playwright.Crawler.Models;
using IC.Test.Playwright.Crawler.Utility;
using NLog;

namespace CrawlerWebApi.Services
{
    public class BaselineTestService : IBaselineTestService
    {
        private readonly PlaywrightContext _playwrightContext;
        private readonly LoginDriver _loginDriver;
        private readonly TestModel _testModel;
        private readonly CrawlDriver _crawlDriver;
        private readonly CrawlContext _crawlContext;
        private readonly Logger _logger;

        public BaselineTestService(
            PlaywrightContext playwrightContext,
            LoginDriver loginDriver,
            TestModel testModel,
            CrawlDriver crawlDriver,
            CrawlContext crawlContext
        )
        {
            _playwrightContext = playwrightContext;
            _loginDriver = loginDriver;
            _testModel = testModel;
            _crawlDriver = crawlDriver;
            _crawlContext = crawlContext;
            _logger = LogManager.GetCurrentClassLogger();
        }

        public async Task<TestResult> RunBaselineTestAsync(BaselineTestPostRequestModel request)
        {
            try
            {
                string url = request.Url;
                string username = request.Username;
                string password = request.Password;

                // Set up test model
                try
                {
                    TimerUtil.StartTimer(_testModel.Timers, "ScenarioDuration");
                    _testModel.Name = "Baseline test: " + url;
                    _testModel.Description = "Some description";
                    _testModel.DateTime = DateTime.Now;
                    _testModel.Browser.Width = 1600;
                    _testModel.Browser.Height = 1000;
                    _logger.Info("Test model set up successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to set up test model.");
                    return new TestResult { Success = false, ErrorMessage = "Failed to set up test model." };
                }

                // Create the HAR file path
                string _harFileName = $"{_testModel.Id}.har";
                string _harFileOriginalPath = Path.Combine(@"C:\ictf", _harFileName);

                // Initialize PlaywrightContext
                try
                {
                    await _playwrightContext.InitializeAsync(_harFileOriginalPath);
                    _testModel.Browser.Name = _playwrightContext.BrowserName;
                    _logger.Info("Playwright context initialized successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to initialize Playwright context.");
                    return new TestResult { Success = false, ErrorMessage = "Failed to initialize Playwright context." };
                }

                // Setup network interception
                List<NetworkData> _networkData = new List<NetworkData>();
                var page = _playwrightContext.Page;
                string _currentPageUrl = page.Url;

                try
                {
                    page.FrameNavigated += (_, frame) =>
                    {
                        if (frame == page.MainFrame)
                        {
                            _currentPageUrl = frame.Url;
                            _logger.Info($"Navigated to: {_currentPageUrl}");
                        }
                    };

                    page.Request += (_, request) =>
                    {
                        _logger.Info($"Request intercepted: {request.Url}");
                        _networkData.Add(new NetworkData
                        {
                            Url = request.Url,
                            Method = request.Method,
                            Headers = request.Headers,
                            PostData = request.PostData,
                            PageUrl = _currentPageUrl
                        });
                    };

                    page.Response += async (_, response) =>
                    {
                        _logger.Info($"Response intercepted: {response.Url} with status {response.Status}");
                        var matchingRequest = _networkData.FirstOrDefault(r => r.Url == response.Url);
                        if (matchingRequest != null)
                        {
                            matchingRequest.StatusCode = response.Status;
                        }
                    };
                    _logger.Info("Network interception set up successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to set up network interception.");
                    return new TestResult { Success = false, ErrorMessage = "Failed to set up network interception." };
                }

                // Perform login
                try
                {
                    await _loginDriver.LoginToApplication(url, username, password);
                    _logger.Info("Login performed successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed during login process.");
                    return new TestResult { Success = false, ErrorMessage = "Failed during login process." };
                }

                // Perform crawl
                try
                {
                    await _crawlDriver.Crawl(_testModel.BaseSaveFolder, _testModel.BaseUrl);
                    _logger.Info("Crawl completed successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed during crawl process.");
                    return new TestResult { Success = false, ErrorMessage = "Failed during crawl process." };
                }

                // Stop timer and assign duration
                try
                {
                    TimerUtil.StopTimer(_testModel.Timers, "ScenarioDuration");
                    _testModel.Duration = TimerUtil.GetElapsedTime(_testModel.Timers, "ScenarioDuration");
                    _testModel.BaseUrl = _crawlContext.BaseUrl;
                    _logger.Info("Timer stopped and duration assigned successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to stop timer and assign duration.");
                    return new TestResult { Success = false, ErrorMessage = "Failed to stop timer and assign duration." };
                }

                // Save reports
                try
                {
                    SaveReports(_networkData);
                    _logger.Info("Reports saved successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to save reports.");
                    return new TestResult { Success = false, ErrorMessage = "Failed to save reports." };
                }

                // Dispose of Playwright context
                try
                {
                    await _playwrightContext.DisposeAsync();
                    _logger.Info("Playwright context disposed successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to dispose Playwright context.");
                    return new TestResult { Success = false, ErrorMessage = "Failed to dispose Playwright context." };
                }

                // Move HAR file
                try
                {
                    await CrawlerCommon.MoveHarFile(_harFileOriginalPath, Path.Combine(_testModel.BaseSaveFolder, _harFileName));
                    _logger.Info("HAR file moved successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to move HAR file.");
                    return new TestResult { Success = false, ErrorMessage = "Failed to move HAR file." };
                }

                // Move Video file
                try
                {
                    await MoveVideo();
                    _logger.Info("Video file moved successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to move video file.");
                    return new TestResult { Success = false, ErrorMessage = "Failed to move video file." };
                }

                // If everything is successful
                _logger.Info("Baseline test executed successfully.");

                // copy log to save path
                CopySpecflowLogToSavePath(_testModel.BaseSaveFolder);

                return new TestResult { Success = true };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error during baseline test execution.");
                return new TestResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        private void SaveReports(List<NetworkData> networkData)
        {
            ReportWriter.SaveModelAsJsonFile(networkData, _testModel.BaseSaveFolder, "networkData");
            ReportWriter.SaveModelAsJsonFile(_testModel, _testModel.BaseSaveFolder, "test-info");
            ReportWriter.SaveReport(_crawlContext.VisitedUrls, _testModel.BaseSaveFolder, "urls");
            ReportWriter.SaveReport(_crawlContext.AppMarkups, _testModel.BaseSaveFolder, "app-markup");
            ReportWriter.SaveReport(_crawlContext.AppTexts, _testModel.BaseSaveFolder, "app-text");
            ReportWriter.SaveReport(_crawlContext.PageScreenshots, _testModel.BaseSaveFolder, "page-screenshots");
            ReportWriter.SaveReport(_crawlContext.AppScreenshots, _testModel.BaseSaveFolder, "app-screenshots");
            ReportWriter.SaveReport(_crawlContext.IcWebPages, _testModel.BaseSaveFolder, "pages-and-apps");
            ReportWriter.UpdateJsonManifest(@"C:\ictf\tests.json", _testModel);
        }

        // @todo This can be reworked to be make more sense, should analyze videos,
        //   if many, only keep latest video (most recent create date) and delete rest
        //   and then move that video to save location
        private async Task MoveVideo()
        {
            try
            {
                string buildBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var projectRoot = Path.GetFullPath(Path.Combine(buildBaseDirectory, @"..\..\..\"));
                string videoSourceDir = Path.Combine(projectRoot, "videos");
                string videoDestDir = Path.Combine(_testModel.BaseSaveFolder, "videos");
                await FileUtil.CopyDirectoryRecursiveAsync(videoSourceDir, videoDestDir);

                // delete videos in source folder otherwise they accumelate
                await FileUtil.DeleteFilesInDirectoryAsync(videoSourceDir);
            }
            catch (Exception ex)
            {
                _logger.Error($"Something went wrong trying to copy the video file. Error: {ex.Message}");
            }
        }

        private void CopySpecflowLogToSavePath(string savePath)
        {
            try
            {
                // copy specflow log file
                string LogFilePath = @"C:\temp";
                string LogFileName = "specflow-console.log";
                string LogFileFullPath = Path.Combine(LogFilePath, LogFileName);
                string LogFileDestFullPath = Path.Combine(savePath, LogFileName);
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
                        _logger.Error($"Failed to copy log file: {ex.Message}");

                        // Wait for the specified delay before retrying
                        Thread.Sleep(delay);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Something went wrong trying to copy the log file. Error: {ex.Message}");
            }
        }
    }
}
