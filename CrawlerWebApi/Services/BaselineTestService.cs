using CrawlerWebApi.Interfaces;
using CrawlerWebApi.Models;
using IC.Test.Playwright.Crawler.Drivers;
using IC.Test.Playwright.Crawler.Models;
using IC.Test.Playwright.Crawler.Utility;
using NLog;
using Microsoft.Extensions.Configuration;
using System.Text;

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
        private readonly WriterQueueService _writerQueueService;

        public BaselineTestService(
            PlaywrightContext playwrightContext,
            LoginDriver loginDriver,
            TestModel testModel,
            CrawlDriver crawlDriver,
            CrawlContext crawlContext,
            IConfiguration configuration,
            WriterQueueService writerQueueService
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
            _writerQueueService = writerQueueService;
        }

        public async Task<TestResult> RunBaselineTestAsync(BaselineTestPostRequestModel request)
        {
            try
            {
                _logger.Info("<<TestStarted>>");

                LogBaselineTestPostRequestModel(request);

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

                // Set up test model
                try
                {
                    TimerUtil.StartTimer(_testModel.Timers, "ScenarioDuration");
                    _testModel.Name = "Baseline test: " + url;
                    _testModel.Description = "Some description";
                    _testModel.DateTime = DateTime.Now;
                    _testModel.Browser.Width = windowWidth;
                    _testModel.Browser.Height = windowHeight;
                    string projectNameSubdomain = UrlUtil.GetSubdomainFromUrl(url).ToLower();
                    _testModel.BaseSaveFolder = PathUtil.CreateSavePath("crawl-tests", projectNameSubdomain, projectNameSubdomain, windowWidth, windowHeight, _testModel.Id.ToString());
                    _logger.Info("Test model set up successfully.");
                    _logger.Info($"BaseSaveFolder: {_testModel.BaseSaveFolder}");
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
                    _playwrightContext._testId = _testModel.Id;
                    _playwrightContext.SetBrowserTypeByName(browser);
                    _playwrightContext._headless = _testModel.Browser.Headless = request.Headless;
                    _playwrightContext._browserWidth = windowWidth;
                    _playwrightContext._browserHeight = windowHeight;

                    _playwrightContext._recordVideo = recordVideo;
                    if (recordVideo)
                    {
                        _playwrightContext._videoSavePath = Path.Combine(_testModel.BaseSaveFolder, "videos");
                    }
                    
                    _playwrightContext._saveHar = saveHar;

                    if (saveHar)
                    {
                        _playwrightContext._harPath = _testModel.BaseSaveFolder;
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
                            //_logger.Info($"Request intercepted: {request.Url}");
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
                            //_logger.Info($"Response intercepted: {response.Url} with status {response.Status}");
                            var matchingRequest = _networkData.FirstOrDefault(r => r.Url == response.Url);
                            if (matchingRequest != null)
                            {
                                matchingRequest.StatusCode = response.Status;
                            }
                        };
                        //_logger.Info("Network interception set up successfully.");
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

                    // Update the manifest
                    await _writerQueueService.EnqueueAsync(async () =>
                    {
                        ReportWriter.UpdateJsonManifest(testsManifestFile, _testModel);
                        ReportWriter.PruneTestsManifest(testsManifestFile);
                    });
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

                // If everything is successful
                _logger.Info("Baseline test executed successfully.");

                // move log to save path
                FileUtil.MoveFileAsync(@"c:\Temp", _testModel.BaseSaveFolder,_testModel.LogFileName, _logger);
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

        private void LogBaselineTestPostRequestModel(BaselineTestPostRequestModel model)
        {
            var properties = model.GetType().GetProperties();
            var logMessage = new StringBuilder();

            logMessage.AppendLine("Logging BaselineTestPostRequestModel properties:");

            foreach (var property in properties)
            {
                var value = property.Name.Equals("Password", StringComparison.OrdinalIgnoreCase)
                    ? "******" // Mask the password value
                    : property.GetValue(model) ?? "null"; // Log the actual value or "null" if not set

                logMessage.AppendLine($"{property.Name}: {value}");
            }

            _logger.Info(logMessage.ToString());
        }
    }
}
