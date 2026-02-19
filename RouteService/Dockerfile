# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
# For more information, please see https://aka.ms/containercompat

# This stage is used when running from VS in fast mode (Default for Debug configuration)
# ======================
# BASE RUNTIME IMAGE
# ======================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5003
ENV ASPNETCORE_URLS=http://+:5003


# install curl for healthcheck
USER root
RUN apt-get update \
    && apt-get install -y curl \
    && rm -rf /var/lib/apt/lists/*

# This stage is used to build the service project
# ======================
# BUILD STAGE
# ======================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj first (better caching)
COPY ["RouteService/RouteService.csproj", "RouteService/"]
RUN dotnet restore "RouteService/RouteService.csproj"

# Copy everything
COPY . .

WORKDIR "/src/RouteService"

RUN dotnet build "RouteService.csproj" -c $BUILD_CONFIGURATION -o /app/build


# This stage is used to publish the service project to be copied to the final stage
# ======================
# PUBLISH STAGE
# ======================
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./RouteService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
# ======================
# FINAL IMAGE
# ======================
#FROM base AS final
#ARG BUILD_CONFIGURATION=Release

#RUN dotnet publish "./RouteService.csproj" \
 #   -c $BUILD_CONFIGURATION \
 #   -o /app/publish \
 #   /p:UseAppHost=false

# =========================
# FINAL
# =========================
# FINAL IMAGE
FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:5003
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "RouteService.dll"]
