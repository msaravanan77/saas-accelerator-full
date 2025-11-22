# Dockerfile for AdminSite (Publisher Portal)
# This builds the publisher-facing administration application

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copy project files
COPY ["AdminSite/AdminSite.csproj", "AdminSite/"]
COPY ["Services/Services.csproj", "Services/"]
COPY ["DataAccess/DataAccess.csproj", "DataAccess/"]

# Restore dependencies
RUN dotnet restore "AdminSite/AdminSite.csproj"

# Copy source code
COPY . .

# Build the application
WORKDIR "/src/AdminSite"
RUN dotnet build "AdminSite.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "AdminSite.csproj" -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Copy published application
COPY --from=publish /app/publish .

# Set environment variables for ASP.NET Core
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "Marketplace.SaaS.Accelerator.AdminSite.dll"]
