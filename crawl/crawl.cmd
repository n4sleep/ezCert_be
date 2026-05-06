@echo off
rem Thin shim so users can run `.\crawl <url>` from PowerShell or cmd
rem instead of `node crawl.mjs <url>`. Forwards all args verbatim.
node "%~dp0crawl.mjs" %*
