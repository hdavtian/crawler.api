Using this txt file to save different settings


------------------------------------
---- Dev ---------------------------
---- appsettings.json --------------
------------------------------------
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "SiteRootWinPath": "C:\\sites\\ictf-reports\\public",
  "SiteArtifactsWinPath": "C:\\sites\\ictf-reports\\public\\test-artifacts",
  "ApiRootWinPath": "C:\\sites\\crawler-web-api\\CrawlerWebApi\\CrawlerWebApi\\bin\\Debug\\net8.0"
}


-------------------------------------
---- Prod ---------------------------
---- appsettings.json ---------------
-------------------------------------
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "SiteRootWinPath": "c:\\inetpub\\taffie",
  "SiteArtifactsWinPath": "c:\\inetpub\\taffie\\test-artifacts",
  "ApiRootWinPath": "c:\\inetpub\\taffieapi"
}

-------------------------------------
---- Dev ----------------------------
---- appsettings.Development.json ---
-------------------------------------
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "CorsSettings": {
    "AllowedOrigin": "http://localhost:4200"
  }
}

-------------------------------------
---- Prod ---------------------------
---- appsettings.Development.json ---
-------------------------------------
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "CorsSettings": {
    "AllowedOrigin": "https://taffie.investcloud.int"
  }
}

******************************************************************************************************************
******************************************* CrawlerConfiguration.json ********************************************
******************************************************************************************************************

----- dev -----
{
  "LibrarySettings": {
    "SiteRootWinPath": "C:\\sites\\ictf-reports\\public",
    "SiteArtifactsWinPath": "C:\\sites\\ictf-reports\\public\\test-artifacts",
    "ApiRootWinPath": "C:\\sites\\crawler-web-api\\CrawlerWebApi\\CrawlerWebApi\\bin\\Debug\\net8.0"
  }
}

----- prod -----
{
  "LibrarySettings": {
    "SiteRootWinPath": "c:\\inetpub\\taffie",
    "SiteArtifactsWinPath": "c:\\inetpub\\taffie\\test-artifacts",
    "ApiRootWinPath": "c:\\inetpub\\taffieapi"
  }
}