CALL SetEnvironment.cmd

move /Y %TDXROOT%\T0002\export\*.txt %RAWDATAFOLDER%\Daily\IncludeRight\
CALL .\ProcessDailyData.cmd
CALL .\GenerateMetrics.cmd
CALL .\SelectStocksBasedOnMetrics.cmd

