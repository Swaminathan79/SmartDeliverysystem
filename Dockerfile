docker run -dt \
-v "/c/Users/Admin/onecoremsvsmon/18.3.20117.7792:/remote_debugger:ro" \
-v "/c/Users/Admin/AppData/Roaming/Microsoft/UserSecrets:/Users/ContainerUser/AppData/Roaming/Microsoft/UserSecrets:ro" \
-v "/c/Users/Admin/AppData/Roaming/Microsoft/UserSecrets:/Users/ContainerAdministrator/AppData/Roaming/Microsoft/UserSecrets:ro" \
-v "/c/Users/Admin/AppData/Roaming/ASP.NET/Https:/Users/ContainerUser/AppData/Roaming/ASP.NET/Https:ro" \
-v "/c/Users/Admin/AppData/Roaming/ASP.NET/Https:/Users/ContainerAdministrator/AppData/Roaming/ASP.NET/Https:ro" \
-v "/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Sdks/Microsoft.Docker.Sdk/tools/win-x64/net6.0:/VSTools:ro" \
-v "/c/Program Files/Microsoft Visual Studio/18/Community/Common7/IDE/CommonExtensions/Microsoft/HotReload:/HotReloadAgent:ro" \
-v "/c/Users/Admin/source/repos/SmartDeliverySystem/PackageService:/app:rw" \
-v "/c/Users/Admin/.nuget/packages:/nuget/fallbackpackages2:rw" \
-e "ASPNETCORE_LOGGING__CONSOLE__DISABLECOLORS=True" \
-e "ASPNETCORE_ENVIRONMENT=Development" \
-e "DOTNET_USE_POLLING_FILE_WATCHER=1" \
-e "NUGET_PACKAGES=/nuget/fallbackpackages2" \
-e "ASPNETCORE_HTTPS_PORTS=8081" \
-e "ASPNETCORE_HTTP_PORTS=8080" \
--name PackageService \
--entrypoint "/remote_debugger/x64/msvsmon.exe" \
packageservice:dev \
/noauth /anyuser /silent /nostatus /noclrwarn /nosecuritywarn /nofirewallwarn /nowowwarn /fallbackloadremotemanagedpdbs /timeout:2147483646 /LogDebuggeeOutputToStdOut
