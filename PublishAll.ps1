
# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

./Publish.ps1 -ProjectPath "Reloaded.Utils.AfsRedirector/Reloaded.Utils.AfsRedirector.csproj" `
              -PackageName "Reloaded.Utils.AfsRedirector" `

Pop-Location