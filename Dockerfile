FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/CandidateService.API/CandidateService.API.csproj", "src/CandidateService.API/"]
COPY ["src/CandidateService.Application/CandidateService.Application.csproj", "src/CandidateService.Application/"]
COPY ["src/CandidateService.Domain/CandidateService.Domain.csproj", "src/CandidateService.Domain/"]
COPY ["src/CandidateService.Infrastructure/CandidateService.Infrastructure.csproj", "src/CandidateService.Infrastructure/"]
RUN dotnet restore "src/CandidateService.API/CandidateService.API.csproj"
COPY . .
WORKDIR "/src/src/CandidateService.API"
RUN dotnet build "CandidateService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CandidateService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CandidateService.API.dll"]