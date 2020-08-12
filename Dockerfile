FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /source

RUN git clone https://github.com/swoga/MikrotikExporter.Net.git .

RUN dotnet publish -c release -o /app -p:PublishSingleFile=true -r linux-x64 --no-self-contained

FROM mcr.microsoft.com/dotnet/core/runtime:3.1
EXPOSE 9436
WORKDIR /app

COPY --from=build /app /app
COPY --from=build /source/modules /modules

CMD ["/app/MikrotikExporter"]
