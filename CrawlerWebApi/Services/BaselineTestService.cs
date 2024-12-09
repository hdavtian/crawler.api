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
        private readonly PlaywrightContext PlaywrightContext;
        private readonly LoginDriver LoginDriver;
        private readonly CrawlTest CrawlTest;
        private readonly CrawlDriver CrawlDriver;
        private readonly CrawlContext CrawlContext;
        private readonly Logger Logger;
        private readonly string ApiRootWinPath;
        private readonly string SiteArtifactsWinPath;
        private readonly WriterQueueService WriterQueueService;

        public BaselineTestService(
            PlaywrightContext PlaywrightContext,
            LoginDriver LoginDriver,
            CrawlTest CrawlTest,
            CrawlDriver CrawlDriver,
            CrawlContext CrawlContext,
            IConfiguration AppConfiguration,
            WriterQueueService WriterQueueService
        )
        {
            this.PlaywrightContext = PlaywrightContext;
            this.LoginDriver = LoginDriver;
            this.CrawlTest = CrawlTest;
            this.CrawlDriver = CrawlDriver;
            this.CrawlContext = CrawlContext;
            Logger = LogManager.GetCurrentClassLogger();
            ApiRootWinPath = AppConfiguration["ApiRootWinPath"];
            SiteArtifactsWinPath = AppConfiguration["SiteArtifactsWinPath"];
            this.WriterQueueService = WriterQueueService;
        }

        public async Task<TestResult> RunBaselineTestAsync(BaselineTestPostRequestModel request)
        {
            try
            {
                Logger.Info("<<TestStarted>>");
                Logger.Info("TestId: " + CrawlTest.Id);

                LogBaselineTestPostRequestModel(request);

                // lets set relavant flags in CrawlContext
                CrawlContext.TakePageScreenshots = request.TakePageScreenshots;
                CrawlContext.TakeAppScreenshots = request.TakeAppScreenshots;
                CrawlContext.CaptureAppHtml = request.CaptureAppHtml;
                CrawlContext.CaptureAppText = request.CaptureAppText;
                CrawlContext.GenerateAxeReports = request.GenerateAxeReports;
                CrawlContext.CaptureNetworkTraffic = request.CaptureNetworkTraffic;

                string _harFileName = $"{CrawlTest.Id}.har";

                // Set up test model
                try
                {
                    TimerUtil.StartTimer(CrawlTest.Timers, "ScenarioDuration");
                    CrawlTest.Name = "";
                    CrawlTest.Description = "";
                    CrawlTest.DateTime = DateTime.Now;
                    CrawlTest.Browser.Width = request.WindowWidth;
                    CrawlTest.Browser.Height = request.WindowHeight;
                    string projectNameSubdomain = UrlUtil.GetSubdomainFromUrl(request.Url).ToLower();
                    CrawlTest.BaseSaveFolder = PathUtil.CreateSavePath("crawl-tests", projectNameSubdomain, request.WindowWidth, request.WindowHeight, CrawlTest.Id.ToString());
                    CrawlTest.ExtraUrls = request.ExtraUrls;
                    CrawlTest.PtierVersion = request.PtierVersion;
                    Logger.Info("Test model set up successfully.");
                    Logger.Info($"BaseSaveFolder: {CrawlTest.BaseSaveFolder}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> Failed to set up test model.");
                    Logger.Info("<<TestEnded>>");
                    return new TestResult { Success = false, ErrorMessage = "Failed to set up test model." };
                }

                // Initialize PlaywrightContext
                try
                {
                    PlaywrightContext._testId = CrawlTest.Id;
                    PlaywrightContext.SetBrowserTypeByName(request.Browser);
                    PlaywrightContext._headless = CrawlTest.Browser.Headless = request.Headless;
                    PlaywrightContext._browserWidth = request.WindowWidth;
                    PlaywrightContext._browserHeight = request.WindowHeight;

                    PlaywrightContext._recordVideo = request.RecordVideo;
                    if (request.RecordVideo)
                    {
                        PlaywrightContext._videoSavePath = Path.Combine(CrawlTest.BaseSaveFolder, "videos");
                    }
                    
                    PlaywrightContext._saveHar = request.SaveHar;

                    if (request.SaveHar)
                    {
                        PlaywrightContext._harPath = CrawlTest.BaseSaveFolder;
                    }

                    await PlaywrightContext.InitializeAsync();
                    CrawlTest.Browser.Name = PlaywrightContext.BrowserName;
                    Logger.Info("Playwright context initialized successfully.");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> Failed to initialize Playwright context.");
                    Logger.Info("<<TestEnded>>");
                    return new TestResult { Success = false, ErrorMessage = "Failed to initialize Playwright context." };
                }

                // Setup network interception
                List<NetworkData> _networkData = new List<NetworkData>();
                if (request.CaptureNetworkTraffic)
                {
                    var page = PlaywrightContext.Page;
                    string _currentPageUrl = page.Url;

                    try
                    {
                        page.FrameNavigated += (_, frame) =>
                        {
                            if (frame == page.MainFrame)
                            {
                                _currentPageUrl = frame.Url;
                                Logger.Info($"Navigated to: {_currentPageUrl}");
                            }
                        };

                        page.Request += (_, request) =>
                        {
                            //Logger.Info($"Request intercepted: {request.Url}");
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
                            //Logger.Info($"Response intercepted: {response.Url} with status {response.Status}");
                            var matchingRequest = _networkData.FirstOrDefault(r => r.Url == response.Url);
                            if (matchingRequest != null)
                            {
                                matchingRequest.StatusCode = response.Status;
                            }
                        };
                        //Logger.Info("Network interception set up successfully.");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "<<Error>> Failed to set up network interception.");
                        Logger.Info("<<TestEnded>>");
                        return new TestResult { Success = false, ErrorMessage = "Failed to set up network interception." };
                    }
                }

                // Perform login
                try
                {
                    await LoginDriver.LoginToApplication(request.Url, request.Username, request.Password);
                    Logger.Info("Login performed successfully.");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> Failed during login process.");
                    Logger.Info("<<TestEnded>>");
                    return new TestResult { Success = false, ErrorMessage = "Failed during login process." };
                }

                // Perform crawl
                try
                {
                    await CrawlDriver.Crawl();
                    Logger.Info("Crawl completed successfully.");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> Failed during crawl process.");
                    Logger.Info("<<TestEnded>>");
                    return new TestResult { Success = false, ErrorMessage = "Failed during crawl process." };
                }

                // Stop timer and assign duration
                try
                {
                    TimerUtil.StopTimer(CrawlTest.Timers, "ScenarioDuration");
                    CrawlTest.Duration = TimerUtil.GetElapsedTime(CrawlTest.Timers, "ScenarioDuration");
                    CrawlTest.BaseUrl = request.Url;
                    Logger.Info("Timer stopped and duration assigned successfully.");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> Failed to stop timer and assign duration.");
                    Logger.Info("<<TestEnded>>");
                    return new TestResult { Success = false, ErrorMessage = "Failed to stop timer and assign duration." };
                }

                // Save reports
                try
                {
                    if (request.CaptureNetworkTraffic)
                    {
                        ReportWriter.SaveModelAsJsonFile(_networkData, CrawlTest.BaseSaveFolder, "networkData");
                        Logger.Info("Network log reports saved successfully.");
                    }

                    // Add totals to CrawlTest to be included in manifest and test-info.json files
                    CrawlTest.UrlTotal = CrawlContext.VisitedUrls.Count;
                    CrawlTest.AppsUniqueTotal = ReportWriter.GetUniqueAppTotal(CrawlContext.IcWebPages);
                    CrawlTest.AppsTotal = ReportWriter.GetAllAppsTotal(CrawlContext.IcWebPages);
                    CrawlTest.PageScreenshotsTotal = CrawlContext.PageScreenshots.Count;
                    CrawlTest.AppScreenshotsTotal = CrawlContext.AppScreenshots.Count;

                    ReportWriter.SaveModelAsJsonFile(CrawlTest, CrawlTest.BaseSaveFolder, "test-info");
                    ReportWriter.SaveReport(CrawlContext.VisitedUrls, CrawlTest.BaseSaveFolder, "urls");
                    
                    if (request.CaptureAppHtml)
                    {
                        ReportWriter.SaveReport(CrawlContext.AppMarkups, CrawlTest.BaseSaveFolder, "app-html");
                        Logger.Info("Captured App Html log reports saved successfully.");
                    }

                    if (request.CaptureAppText)
                    {
                        ReportWriter.SaveReport(CrawlContext.AppTexts, CrawlTest.BaseSaveFolder, "app-text");
                        Logger.Info("Captured App Text log reports saved successfully.");
                    }

                    if (request.TakePageScreenshots)
                    {
                        ReportWriter.SaveReport(CrawlContext.PageScreenshots, CrawlTest.BaseSaveFolder, "page-screenshots");
                        Logger.Info("Captured Page screenshots log reports saved successfully.");
                    }

                    if (request.TakeAppScreenshots)
                    {
                        ReportWriter.SaveReport(CrawlContext.AppScreenshots, CrawlTest.BaseSaveFolder, "app-screenshots");
                        Logger.Info("Captured App screenshots log reports saved successfully.");
                    }

                    ReportWriter.SaveReport(CrawlContext.IcWebPages, CrawlTest.BaseSaveFolder, "pages-and-apps");
                    string testsManifestFile = Path.Combine(SiteArtifactsWinPath, "tests.json");

                    // Update the manifest
                    await WriterQueueService.EnqueueAsync(async () =>
                    {
                        ReportWriter.UpdateJsonManifest(testsManifestFile, CrawlTest);
                        ReportWriter.PruneTestsManifest(testsManifestFile);
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> Failed to save reports.");
                    Logger.Info("<<TestEnded>>");
                    return new TestResult { Success = false, ErrorMessage = "Failed to save reports." };
                }

                // Dispose of Playwright context
                try
                {
                    await PlaywrightContext.DisposeAsync();
                    Logger.Info("Playwright context disposed successfully.");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> Failed to dispose Playwright context.");
                    Logger.Info("<<TestEnded>>");
                    return new TestResult { Success = false, ErrorMessage = "Failed to dispose Playwright context." };
                }

                // If everything is successful
                Logger.Info("Baseline test executed successfully.");

                // move log to save path
                FileUtil.MoveFileAsync(@"c:\Temp", CrawlTest.BaseSaveFolder,CrawlTest.LogFileName, Logger);
                Logger.Info("<<TestEnded>>");

                return new TestResult { Success = true };
            }
            catch (Exception ex)
            {
                Logger.Info("<<TestError>>, <<TestEnded>>");
                Logger.Error(ex, "<<Error>> Unexpected error during baseline test execution.");
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

            Logger.Info(logMessage.ToString());
        }
    }
}
