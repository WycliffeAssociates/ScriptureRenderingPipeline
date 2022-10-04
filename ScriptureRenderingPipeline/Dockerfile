FROM mcr.microsoft.com/dotnet/sdk:6.0 AS installer-env
COPY . /src/dotnet-function-app
RUN cd /src/dotnet-function-app && \
    mkdir -p /home/site/wwwroot && \
    dotnet publish ScriptureRenderingPipeline/*.csproj --output /home/site/wwwroot

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS functions-env

ARG TARGETPLATFORM
ARG BUILDPLATFORM
RUN echo $TARGETPLATFORM
RUN wget https://github.com/Azure/azure-functions-core-tools/archive/refs/tags/4.0.4785.tar.gz -O source.tar.gz
RUN tar xzf source.tar.gz && cd azure-functions* && dotnet build Azure.Functions.Cli.sln && export runtime=$(case $TARGETPLATFORM in linux/amd64) echo linux-x64;; linux/arm64) echo linux-arm64;;esac) && dotnet publish src/Azure.Functions.Cli/Azure.Functions.Cli.csproj --runtime="$runtime" --output=/tmp/cli

FROM mcr.microsoft.com/dotnet/runtime:6.0

ARG TARGETPLATFORM
ARG BUILDPLATFORM
RUN echo $TARGETPLATFORM

ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]
COPY --from=functions-env ["/tmp/cli", "/opt/functions/"]
WORKDIR /home/site/wwwroot
ENTRYPOINT [ "/opt/functions/func", "start", "--csharp" ]