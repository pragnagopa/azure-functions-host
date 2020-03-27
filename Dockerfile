## docker run -e "CONTAINER_NAME=TestContainer" -e "WEBSITE_AUTH_ENCRYPTION_KEY=dvWGD2FcG3sdFmlf894+zKECWemglW2rGxvtBbTinUg=" -e "CONTAINER_ENCRYPTION_KEY=dvWGD2FcG3sdFmlf894+zKECWemglW2rGxvtBbTinUg=" -e "WEBSITE_PLACEHOLDER_MODE=1" -e "WEBSITE_MOUNT_ENABLED=0" -e "AZURE_FUNCTIONS_ENVIRONMENT=development" -e "Fabric_NodeIPOrFQDN=localhost" -p 8080:80 <imagetag>
## docker run -it -e WEBSITE_CONTAINER_READY=0 -e WEBSITE_PLACEHOLDER_MODE=1  -e CONTAINER_NAME=TestContainer -e CONTAINER_ENCRYPTION_KEY="x6C/xJSKuWi3xqH/WzXUXAUFvOx7pkEp0TlCJYS3KwY=" -p 8080:80 fast-path-baseline
## docker build -t fast-path-baseline .

# Build WebJobs.Script.WebHost.csproj image
FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build-env
#FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build-env
ENV PublishWithAspNetCoreTargetManifest=false \
    HOST_VERSION=2.0.36002 \
    HOST_COMMIT=1a94a7a5706fc666a2f32ffdc23755ea445b6a3a

RUN BUILD_NUMBER=$(echo $HOST_VERSION | cut -d'.' -f 3) && \
    wget https://github.com/pragnagopa/azure-functions-host/archive/$HOST_COMMIT.tar.gz && \
    tar xzf $HOST_COMMIT.tar.gz && \
    cd azure-functions-host* && \
    dotnet publish -v q /p:BuildNumber=$BUILD_NUMBER /p:CommitHash=$HOST_COMMIT src/WebJobs.Script.WebHost/WebJobs.Script.WebHost.csproj --runtime debian.9-x64 --output /azure-functions-host

# Runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:2.2

COPY --from=build-env ["/azure-functions-host", "/azure-functions-host"]

ENV HOME=/home \
    ASPNETCORE_URLS=http://+:80 \
    FUNCTIONS_WORKER_RUNTIME=node

EXPOSE 80

CMD dotnet /azure-functions-host/Microsoft.Azure.WebJobs.Script.WebHost.dll