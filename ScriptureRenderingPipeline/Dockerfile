FROM mcr.microsoft.com/dotnet/sdk:6.0 AS installer-env
COPY . /src/dotnet-function-app
RUN cd /src/dotnet-function-app && \
    mkdir -p /home/site/wwwroot && \
    dotnet publish ScriptureRenderingPipeline/*.csproj --output /home/site/wwwroot

FROM mcr.microsoft.com/dotnet/runtime:6.0
ARG TARGETPLATFORM
ARG BUILDPLATFORM
RUN apt update && apt install -y curl gpg
RUN curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.gpg && mv microsoft.gpg /etc/apt/trusted.gpg.d/microsoft.gpg

RUN export arch=$(echo $TARGETPLATFORM | cut -d'/' -f 2) && echo "deb [arch=$arch] https://packages.microsoft.com/debian/11/prod bullseye main" > /etc/apt/sources.list.d/dotnetdev.list
RUN apt update && apt install -y azure-functions-core-tools-4 && apt remove -y curl gpg
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]
WORKDIR /home/site/wwwroot
ENTRYPOINT [ "func", "start", "--csharp" ]