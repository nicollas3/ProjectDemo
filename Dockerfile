FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/NeuroCare.Web/NeuroCare.Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
RUN mkdir -p /app/App_Data/uploads && chown -R $APP_UID /app/App_Data
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "NeuroCare.Web.dll"]
