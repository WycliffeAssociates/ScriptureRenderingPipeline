FROM mcr.microsoft.com/dotnet/sdk:8.0 AS installer-env
COPY . /src/dotnet-function-app
RUN cd /src/dotnet-function-app && \
    mkdir -p /home/site/wwwroot && \
    dotnet publish ScriptureRenderingPipeline/*.csproj --output /home/site/wwwroot

FROM mcr.microsoft.com/azure-functions/dotnet:4
ENV FUNCTIONS_WORKER_RUNTIME=dotnet
ENV FUNCTIONS_INPROC_NET8_ENABLED=1
ENV FUNCTIONS_EXTENSION_VERSION=~4
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]