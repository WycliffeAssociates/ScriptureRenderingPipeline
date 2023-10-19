FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["VerseReportingProcessor/VerseReportingProcessor.csproj", "VerseReportingProcessor/"]
COPY ["PipelineCommon/PipelineCommon.csproj", "PipelineCommon/"]
RUN dotnet restore "VerseReportingProcessor/VerseReportingProcessor.csproj"
COPY . .
WORKDIR "/src/VerseReportingProcessor"
RUN dotnet build "VerseReportingProcessor.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "VerseReportingProcessor.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VerseReportingProcessor.dll"]
