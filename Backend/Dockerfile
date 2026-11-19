ARG REGISTRY=nexus.elm.sa

## Stage Build ##
FROM ${REGISTRY}/docker-public/dotnet/sdk:10.0 AS build

WORKDIR /src
COPY . . 

# Build
RUN dotnet build -c Release -o /src/publish

RUN mkdir /output
RUN tar -czvf /output/app.tar.gz -C /src/publish .
