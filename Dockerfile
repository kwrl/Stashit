FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Stashit/Stashit.csproj", "src/Stashit/"]
RUN dotnet restore src/Stashit/Stashit.csproj

COPY ["src/Stashit/", "src/Stashit/"]
RUN dotnet publish src/Stashit/Stashit.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Redis endpoint the app dials at startup. Override at runtime (compose, k8s, -e).
ENV STORAGE__BASEURL=redis:6379

ENTRYPOINT ["dotnet", "Stashit.dll"]
