FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app
COPY ["Chisato.Takina/appsettings.json", "/app/"]
# Open rabbitmq port to make rabbitmq from outside of container can connect to program
EXPOSE 5672

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Chisato.Takina/Chisato.Takina.csproj", "Chisato.Takina/"]
RUN dotnet restore "Chisato.Takina/Chisato.Takina.csproj"
COPY . .
WORKDIR "/src/Chisato.Takina"
RUN dotnet build "Chisato.Takina.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Chisato.Takina.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Chisato.Takina.dll"]
