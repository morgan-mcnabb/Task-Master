FROM node:20-alpine AS ui
WORKDIR /src/apps/ui

COPY apps/ui/package.json ./
COPY apps/ui/package-lock.json ./

RUN npm ci

COPY apps/ui/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY TaskMaster/ ./TaskMaster/

RUN rm -rf TaskMaster/TaskMasterApi/wwwroot/* || true

COPY --from=ui /src/apps/ui/dist/ ./TaskMaster/TaskMasterApi/wwwroot/

RUN dotnet restore TaskMaster/TaskMasterApi/TaskMasterApi.csproj
RUN dotnet publish TaskMaster/TaskMasterApi/TaskMasterApi.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

VOLUME ["/data"]

COPY --from=build /out ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TaskMasterApi.dll"]
