# Stage 1: Build .NET API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy all files and restore dependencies
COPY . .
RUN dotnet restore

# Publish release build
RUN dotnet publish -c Release -o /app/publish

# Stage 2: ASP.NET Core Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Expose internal port 5000
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5000

# Replace 'YourApiProjectName.dll' with your project's actual compiled DLL name:
ENTRYPOINT ["dotnet", "YourApiProjectName.dll"]
