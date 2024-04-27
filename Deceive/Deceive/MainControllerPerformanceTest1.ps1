# Define the path to the application executable
$appPath = "C:\Path\To\Deceive.exe"

# Start the application
$process = Start-Process -FilePath $appPath -PassThru

# Give the application some time to initialize
Start-Sleep -Seconds 10

# Measure CPU and memory usage over a 1-minute interval
$startTime = Get-Date
while ((Get-Date) -lt $startTime.AddMinutes(1)) {
    $cpu = Get-Counter '\Process(deceive)\% Processor Time'
    $mem = Get-Counter '\Process(deceive)\Working Set - Private'
    Write-Output "Time: $(Get-Date), CPU Usage: $($cpu.CounterSamples.CookedValue), Memory Usage: $($mem.CounterSamples.CookedValue)"
    Start-Sleep -Seconds 5
}

# Stop the process
$process | Stop-Process