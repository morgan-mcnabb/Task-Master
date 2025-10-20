FROM node:20-alpine AS ui
WORKDIR /src/apps/ui

COPY apps/ui/package*.json ./
RUN npm ci

COPY apps/ui/ ./
RUN npm run build -- --outDir /ui-dist

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY TaskMaster/ ./TaskMaster/

COPY --from=ui /ui-dist ./TaskMaster/TaskMasterApi/wwwroot

RUN dotnet restore TaskMaster/TaskMaster.sln
RUN dotnet publish TaskMaster/TaskMasterApi/TaskMasterApi.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

RUN mkdir -p /data

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "TaskMasterApi.dll"]
