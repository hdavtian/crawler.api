using CrawlerWebApi.Interfaces;
using CrawlerWebApi.Models;
using IC.Test.Playwright.Crawler.Drivers;
using IC.Test.Playwright.Crawler.Models;
using IC.Test.Playwright.Crawler.Utility;
using NLog;
using Microsoft.Extensions.Configuration;

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
        private readonly string _apiRootWinPath;
        private readonly string _siteArtifactsWinPath;

        public BaselineTestService(
            PlaywrightContext playwrightContext,
            LoginDriver loginDriver,
            TestModel testModel,
            CrawlDriver crawlDriver,
            CrawlContext crawlContext,
            IConfiguration configuration
        )
        {
            _playwrightContext = playwrightContext;
            _loginDriver = loginDriver;
            _testModel = testModel;
            _crawlDriver = crawlDriver;
            _crawlContext = crawlContext;
            _logger = LogManager.GetCurrentClassLogger();
            _apiRootWinPath = configuration["ApiRootWinPath"];
            _siteArtifactsWinPath = configuration["SiteArtifactsWinPath"];
        }

        public async Task<TestResult> RunBaselineTestAsync(BaselineTestPostRequestModel request)
        {
            try
            {
                _logger.Info("<<TestStarted>>");

                string url = request.Url;
                string username = request.Username;
                string password = request.Password;
                bool headless = request.Headless;
                string browser = request.Browser;
                int windowWidth = request.WindowWidth;
                int windowHeight = request.WindowHeight;
                bool recordVideo = request.RecordVideo;
                bool takePageScreenshots = request.TakePageScreenshots;
                bool takeAppScreenshots = request.TakeAppScreenshots;
                bool captureAppHtml = request.CaptureAppHtml;
                bool captureAppText = request.CaptureAppText;
                bool generateAxeReports = request.GenerateAxeReports;
                bool captureNetworkTraffic = request.CaptureNetworkTraffic;
                bool saveHar = request.SaveHar;

                // lets set relavant flags in _crawlContext
                _crawlContext.TakePageScreenshots = takePageScreenshots;
                _crawlContext.TakeAppScreenshots = takeAppScreenshots;
                _crawlContext.CaptureAppHtml = captureAppHtml;
                _crawlContext.CaptureAppText = captureAppText;
                _crawlContext.GenerateAxeReports = generateAxeReports;
                _crawlContext.CaptureNetworkTraffic = captureNetworkTraffic;

                string _harFileName = $"{_testModel.Id}.har";
                //string _harFileOriginalPath = Path.Combine(@"C:\ictf", _harFileName);
                string _harFileOriginalPath = Path.Combine(_siteArtifactsWinPath, _harFileName);

                // Set up test model
                try
                {
                    TimerUtil.StartTimer(_testModel.Timers, "ScenarioDuration");
                    _testModel.Name = "Baseline test: " + url;
                    _testModel.Description = "Some description";
                    _testModel.DateTime = DateTime.Now;
                    _testModel.Browser.Width = windowWidth;
                    _testModel.Browser.Height = windowHeight;
                    _logger.Info("Test model set up successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "<<Error>> Failed to set up test model.");
                    _logger.Info("<<TestEnded>>");
                    return new TestResult { Success = false, ErrorMessage = "Failed to set up test model." };
                }

                // Initialize PlaywrightContext
                try
                {
                    _playwrightContext.SetBrowserTypeByName(browser);
                    _playwrightContext._headless = _testModel.Browser.Headless = request.Headless;
                    _playwrightContext._browserWidth = windowWidth;
                    _playwrightContext._browserHeight = windowHeight;
                    _playwrightContext._recordVideo = recordVideo;
                    _playwrightContext._saveHar = saveHar;

                    if (saveHar)
                    {
                        _playwrightContext._harPath = _harFileOriginalPath;
                    }

                    await _playwrightContext.InitializeAsync();
                    _testModel.Browser.Name = _playwrightContext.BrowserName;
                    _logger.Info("Playwright context initialized successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "<<Error>> Failed to initialize Playwright context.");
                    _logger.Info("<<TestEnded>>");
                    return new TestResult { Success = false, ErrorMessage = "Failed to initialize Playwright context." };
                }

                // Setup network interception
                List<NetworkData> _networkData = new List<NetworkData>();
                if (captureNetworkTraffic)
                {
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
                        _logger.Error(ex, "<<Error>> Failed to set up network interception.");
                        _logger.Info("<<TestEnded>>");
                        return new TestResult { Success = false, ErrorMessage = "Failed to set up network interception." };
                    }
                }

                // Perform login
                try
                {
                    await _loginDriver.LoginToApplication(url, username, password);
                    _logger.Info("Login performed successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "<<Error>> Failed during login process.");
                    _logger.Info("<<TestEnded>>");
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
                    _logger.Error(ex, "<<Error>> Failed during crawl process.");
                    _logger.Info("<<TestEnded>>");
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
                    _logger.Error(ex, "<<Error>> Failed to stop timer and assign duration.");
                    _logger.Info("<<TestEnded>>");
                    return new TestResult { Success = false, ErrorMessage = "Failed to stop timer and assign duration." };
                }

                // Save reports
                try
                {
                    if (captureNetworkTraffic)
                    {
                        ReportWriter.SaveModelAsJsonFile(_networkData, _testModel.BaseSaveFolder, "networkData");
                        _logger.Info("Network log reports saved successfully.");
                    }

                    ReportWriter.SaveModelAsJsonFile(_testModel, _testModel.BaseSaveFolder, "test-info");
                    ReportWriter.SaveReport(_crawlContext.VisitedUrls, _testModel.BaseSaveFolder, "urls");
                    
                    if (captureAppHtml)
                    {
                        ReportWriter.SaveReport(_crawlContext.AppMarkups, _testModel.BaseSaveFolder, "app-markup");
                        _logger.Info("Captured App Html log reports saved successfully.");
                    }

                    if (captureAppText)
                    {
                        ReportWriter.SaveReport(_crawlContext.AppTexts, _testModel.BaseSaveFolder, "app-text");
                        _logger.Info("Captured App Text log reports saved successfully.");
                    }

                    if (takePageScreenshots)
                    {
                        ReportWriter.SaveReport(_crawlContext.PageScreenshots, _testModel.BaseSaveFolder, "page-screenshots");
                        _logger.Info("Captured Page screenshots log reports saved successfully.");
                    }

                    if (takeAppScreenshots)
                    {
                        ReportWriter.SaveReport(_crawlContext.AppScreenshots, _testModel.BaseSaveFolder, "app-screenshots");
                        _logger.Info("Captured App screenshots log reports saved successfully.");
                    }

                    ReportWriter.SaveReport(_crawlContext.IcWebPages, _testModel.BaseSaveFolder, "pages-and-apps");
                    string testsManifestFile = Path.Combine(_siteArtifactsWinPath, "tests.json");
                    ReportWriter.UpdateJsonManifest(testsManifestFile, _testModel);
                    ReportWriter.PruneTestsManifest(testsManifestFile);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "<<Error>> Failed to save reports.");
                    _logger.Info("<<TestEnded>>");
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
                    _logger.Error(ex, "<<Error>> Failed to dispose Playwright context.");
                    _logger.Info("<<TestEnded>>");
                    return new TestResult { Success = false, ErrorMessage = "Failed to dispose Playwright context." };
                }

                // Move HAR file
                if (saveHar)
                {
                    try
                    {
                        await CrawlerCommon.MoveHarFile(_harFileOriginalPath, Path.Combine(_testModel.BaseSaveFolder, _harFileName));
                        _logger.Info("HAR file moved successfully.");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "<<Error>> Failed to move HAR file.");
                        _logger.Info("<<TestEnded>>");
                        return new TestResult { Success = false, ErrorMessage = "Failed to move HAR file." };
                    }
                }

                // Move Video file
                if (recordVideo)
                {
                    try
                    {
                        await MoveVideo();
                        _logger.Info("Video file moved successfully.");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "<<Error>> Failed to move video file.");
                        _logger.Info("<<TestEnded>>");
                        return new TestResult { Success = false, ErrorMessage = "Failed to move video file." };
                    }
                }

                // If everything is successful
                _logger.Info("Baseline test executed successfully.");

                // copy log to save path
                CopySpecflowLogToSavePath(_testModel.BaseSaveFolder);

                _logger.Info("<<TestEnded>>");

                return new TestResult { Success = true };
            }
            catch (Exception ex)
            {
                _logger.Info("<<TestError>>, <<TestEnded>>");
                _logger.Error(ex, "<<Error>> Unexpected error during baseline test execution.");
                return new TestResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        // @todo This can be reworked to be make more sense, should analyze videos,
        //   if many, only keep latest video (most recent create date) and delete rest
        //   and then move that video to save location
        private async Task MoveVideo()
        {
            try
            {
                string videoSourceDir = Path.Combine(_apiRootWinPath, "videos");
                string videoDestDir = Path.Combine(_testModel.BaseSaveFolder, "videos");
                await FileUtil.CopyDirectoryRecursiveAsync(videoSourceDir, videoDestDir);

                // delete videos in source folder otherwise they accumelate
                await FileUtil.DeleteFilesInDirectoryAsync(videoSourceDir);
            }
            catch (Exception ex)
            {
                _logger.Error($"<<Error>> Something went wrong trying to copy the video file. Error: {ex.Message}");
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
