# Dockerfile for CustomerSite (Landing Page and Subscription Portal)
# This builds the customer-facing SaaS application

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copy project files
COPY ["CustomerSite/CustomerSite.csproj", "CustomerSite/"]
COPY ["Services/Services.csproj", "Services/"]
COPY ["DataAccess/DataAccess.csproj", "DataAccess/"]

# Restore dependencies
RUN dotnet restore "CustomerSite/CustomerSite.csproj"

# Copy source code
COPY . .

# Build the application
WORKDIR "/src/CustomerSite"
RUN dotnet build "CustomerSite.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "CustomerSite.csproj" -c Release -o /app/publish

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

ENTRYPOINT ["dotnet", "Marketplace.SaaS.Accelerator.CustomerSite.dll"]
