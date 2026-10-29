FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /repo
COPY Directory.Build.props global.json ./
COPY src ./src
RUN dotnet restore src/Services/Company/Vat.Company.Api/Vat.Company.Api.csproj
RUN dotnet publish src/Services/Company/Vat.Company.Api/Vat.Company.Api.csproj -c Release --no-restore -o /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER 1654
ENTRYPOINT ["dotnet", "Vat.Company.Api.dll"]
