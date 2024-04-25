# ---------------------
# Builder
FROM ghcr.io/ezywebwerkstaden/ezy.dotnetcoresdk:1.0.20--3.1.200-buster as builder
LABEL se.ezy.project=identityserver-admin \
  se.ezy.image-purpose=build

WORKDIR /src

COPY --chown=buildagent:buildagent ["src/Skoruba.IdentityServer4.Admin/Skoruba.IdentityServer4.Admin.csproj", "src/Skoruba.IdentityServer4.Admin/"]
COPY --chown=buildagent:buildagent ["src/Skoruba.IdentityServer4.Admin.EntityFramework.MySql/Skoruba.IdentityServer4.Admin.EntityFramework.MySql.csproj", "src/Skoruba.IdentityServer4.Admin.EntityFramework.MySql/"]
COPY --chown=buildagent:buildagent ["src/Skoruba.IdentityServer4.Admin.EntityFramework.Identity/Skoruba.IdentityServer4.Admin.EntityFramework.Identity.csproj", "src/Skoruba.IdentityServer4.Admin.EntityFramework.Identity/"]
COPY --chown=buildagent:buildagent ["src/Skoruba.IdentityServer4.Admin.EntityFramework/Skoruba.IdentityServer4.Admin.EntityFramework.csproj", "src/Skoruba.IdentityServer4.Admin.EntityFramework/"]
COPY --chown=buildagent:buildagent ["src/Skoruba.IdentityServer4.Admin.EntityFramework.Extensions/Skoruba.IdentityServer4.Admin.EntityFramework.Extensions.csproj", "src/Skoruba.IdentityServer4.Admin.EntityFramework.Extensions/"]
COPY --chown=buildagent:buildagent ["src/Skoruba.IdentityServer4.Admin.EntityFramework.Shared/Skoruba.IdentityServer4.Admin.EntityFramework.Shared.csproj", "src/Skoruba.IdentityServer4.Admin.EntityFramework.Shared/"]
COPY --chown=buildagent:buildagent ["src/Skoruba.IdentityServer4.Admin.BusinessLogic/Skoruba.IdentityServer4.Admin.BusinessLogic.csproj", "src/Skoruba.IdentityServer4.Admin.BusinessLogic/"]
COPY --chown=buildagent:buildagent ["src/Skoruba.IdentityServer4.Admin.BusinessLogic.Shared/Skoruba.IdentityServer4.Admin.BusinessLogic.Shared.csproj", "src/Skoruba.IdentityServer4.Admin.BusinessLogic.Shared/"]
COPY --chown=buildagent:buildagent ["src/Skoruba.IdentityServer4.Admin.EntityFramework.PostgreSQL/Skoruba.IdentityServer4.Admin.EntityFramework.PostgreSQL.csproj", "src/Skoruba.IdentityServer4.Admin.EntityFramework.PostgreSQL/"]
COPY --chown=buildagent:buildagent ["src/Skoruba.IdentityServer4.Admin.BusinessLogic.Identity/Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.csproj", "src/Skoruba.IdentityServer4.Admin.BusinessLogic.Identity/"]
COPY --chown=buildagent:buildagent ["src/Skoruba.IdentityServer4.Admin.EntityFramework.SqlServer/Skoruba.IdentityServer4.Admin.EntityFramework.SqlServer.csproj", "src/Skoruba.IdentityServer4.Admin.EntityFramework.SqlServer/"]
COPY --chown=buildagent:buildagent ["src/Skoruba.IdentityServer4.Shared/Skoruba.IdentityServer4.Shared.csproj", "src/Skoruba.IdentityServer4.Shared/"]

RUN --mount=type=secret,id=github_ezy,uid=1000,gid=1000,uid=1000,gid=1000,dst=/etc/secrets/github_ezy.env \
      export $(xargs < /etc/secrets/github_ezy.env) && \
      export GCR_PAT=$(echo $GCR_PAT|tr -d "\t\r\n") && \
      dotnet restore "src/Skoruba.IdentityServer4.Admin/Skoruba.IdentityServer4.Admin.csproj"
COPY --chown=buildagent:buildagent . .
WORKDIR "/src/src/Skoruba.IdentityServer4.Admin"

ARG version=0.1.0
RUN mkdir -p /home/buildagent/build
RUN dotnet build "Skoruba.IdentityServer4.Admin.csproj" -c Release -o /home/buildagent/build



# ---------------------
# Publish
FROM builder AS publish
LABEL se.ezy.project=identityserver-admin \
  se.ezy.image-purpose=build

RUN mkdir -p /home/buildagent/publish
RUN --mount=type=secret,id=github_ezy,uid=1000,gid=1000,uid=1000,gid=1000,dst=/etc/secrets/github_ezy.env \
      export $(xargs < /etc/secrets/github_ezy.env) && \
      dotnet publish "Skoruba.IdentityServer4.Admin.csproj" -c Release -o /home/buildagent/publish



# ---------------------
# Final runtime image for GCP
FROM ghcr.io/ezywebwerkstaden/ezy.ubi8.dotnet-31-runtime:3.1-1.0.8 as gcp
LABEL se.ezy.project=identityserver-admin \
  se.ezy.image-purpose=dist

COPY --from=publish /home/buildagent/publish .
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENTRYPOINT ["dotnet", "Skoruba.IdentityServer4.Admin.dll"]



# ---------------------
# Final runtime image for AWS
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1.2-buster-slim as aws
LABEL se.ezy.project=identityserver-admin \
  se.ezy.image-purpose=dist

## DataDog .NET Tracer
ARG TRACER_VERSION=2.30.0
RUN curl -LO https://github.com/DataDog/dd-trace-dotnet/releases/download/v${TRACER_VERSION}/datadog-dotnet-apm_${TRACER_VERSION}_amd64.deb && \
    dpkg -i ./datadog-dotnet-apm_${TRACER_VERSION}_amd64.deb
ENV CORECLR_ENABLE_PROFILING=1 \
  CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8} \
  CORECLR_PROFILER_PATH=/opt/datadog/Datadog.Trace.ClrProfiler.Native.so \
  DD_DOTNET_TRACER_HOME=/opt/datadog \
  LD_PRELOAD=/opt/datadog/continuousprofiler/Datadog.Linux.ApiWrapper.x64.so \
  DD_PROFILING_ENABLED=1 \
  DD_PROFILING_EXCEPTION_ENABLED=1 \
  DD_RUNTIME_METRICS_ENABLED=1

WORKDIR /app
COPY --from=publish /home/buildagent/publish .
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENV ASPNETCORE_URLS http://+:8000
EXPOSE 8000
ENTRYPOINT ["dotnet", "Skoruba.IdentityServer4.Admin.dll"]