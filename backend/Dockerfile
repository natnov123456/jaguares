# JAG-77: imagen de producción de la API (ASP.NET Core 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilacion
WORKDIR /src
COPY src/ ./src/
RUN dotnet publish src/FundacionJaguares.Api/FundacionJaguares.Api.csproj -c Release -o /app --nologo

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# curl: chequeo de salud del contenedor · fuentes: los certificados y reportes PDF necesitan tipografías
RUN apt-get update && apt-get install -y --no-install-recommends curl fonts-dejavu-core fontconfig \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=compilacion /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Almacenamiento__RutaLocal=/datos/uploads
# La carpeta de archivos (certificados, galería, plantillas) debe pertenecer al usuario que corre la API:
# un volumen nuevo hereda este dueño. Sin esto, guardar archivos falla con "acceso denegado".
RUN mkdir -p /datos/uploads && chown -R app:app /datos
VOLUME /datos/uploads
EXPOSE 8080
# Corre sin privilegios de administrador
USER app
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD curl -fs http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "FundacionJaguares.Api.dll"]
