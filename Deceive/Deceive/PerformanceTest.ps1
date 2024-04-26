# Define the path to the executable
$exePath = "C:\Desktop\ChristinaHua\Deceieve\Deceive.exe"

# Start the process and measure execution time
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$process = Start-Process -FilePath $exePath -PassThru
Wait-Process -Id $process.Id
$stopwatch.Stop()

# Gather CPU and Memory Usage
$cpuUsage = Get-Counter '\Process(*)\% Processor Time' | Select-Object -ExpandProperty countersamples | Where-Object {$_.instancename -eq $process.ProcessName}
$memoryUsage = Get-Counter '\Process(*)\Working Set - Private' | Select-Object -ExpandProperty countersamples | Where-Object {$_.instancename -eq $process.ProcessName}

# Output the results
Write-Output "Startup Time: $($stopwatch.ElapsedMilliseconds) ms"
Write-Output "CPU Usage: $($cpuUsage.CookedValue) %"
Write-Output "Memory Usage: $($memoryUsage.CookedValue) KB"

# Optionally, save results to a CSV file for later analysis
$result = [PSCustomObject]@{
    "StartupTime_ms" = $stopwatch.ElapsedMilliseconds
    "CPUUsage_Percent" = $cpuUsage.CookedValue
    "MemoryUsage_KB" = $memoryUsage.CookedValue
}
$result | Export-Csv -Path "PerformanceResults.csv" -NoTypeInformation -Append