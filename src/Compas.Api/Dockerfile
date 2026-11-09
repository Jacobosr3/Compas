# Imagen de la API de Compás para la nube (Vercel Container runtime, Render, Cloud Run, Fly.io…).
# Contexto de construcción: esta carpeta (src/Compas.Api).

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Compas.Api.csproj ./
RUN dotnet restore Compas.Api.csproj
COPY . ./
# (El proyecto lanza Vite con PowerShell solo al compilar en Windows; en Linux no se ejecuta.)
RUN dotnet publish Compas.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# El proveedor indica el puerto con la variable PORT (Program.cs la lee); por defecto 8080.
ENV PORT=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
ENTRYPOINT ["dotnet", "Compas.Api.dll"]
