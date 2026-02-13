# Estágio de Compilação (Build)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia os arquivos de projeto primeiro (para aproveitar cache)
COPY ["Nexus.Api/Nexus.Api.csproj", "Nexus.Api/"]
COPY ["Nexus.Domain/Nexus.Domain.csproj", "Nexus.Domain/"]

# Restaura as dependências
RUN dotnet restore "Nexus.Api/Nexus.Api.csproj"

# Copia todo o resto do código
COPY . .

# Compila a API
WORKDIR "/src/Nexus.Api"
RUN dotnet build "Nexus.Api.csproj" -c Release -o /app/build

# Publica a API
FROM build AS publish
RUN dotnet publish "Nexus.Api.csproj" -c Release -o /app/publish

# Estágio Final (Execução)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# O Render precisa que a gente diga qual porta usar
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "Nexus.Api.dll"]
