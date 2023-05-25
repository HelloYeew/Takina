FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Chisato.Takina.WebApi/Chisato.Takina.WebApi.csproj", "Chisato.Takina.WebApi/"]
RUN dotnet restore "Chisato.Takina.WebApi/Chisato.Takina.WebApi.csproj"
COPY . .
WORKDIR "/src/Chisato.Takina.WebApi"
RUN dotnet build "Chisato.Takina.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Chisato.Takina.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Chisato.Takina.WebApi.dll"]
