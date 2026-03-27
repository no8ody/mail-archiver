FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ARG GIT_COMMIT=unknown
ENV MAILARCHIVER_GIT_COMMIT=${GIT_COMMIT}

RUN apt-get update \
    && apt-get install -y --no-install-recommends git \
    && rm -rf /var/lib/apt/lists/*

COPY ["MailArchiver.csproj", "./"]
RUN dotnet restore "./MailArchiver.csproj"

COPY . .
RUN dotnet publish "./MailArchiver.csproj" -c Release -o /app/publish /p:UseAppHost=false /p:GitCommitShort=${GIT_COMMIT}

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ARG GIT_COMMIT=unknown
ENV MAILARCHIVER_GIT_COMMIT=${GIT_COMMIT}
ENV ASPNETCORE_URLS=http://0.0.0.0:5000

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --system mailarchiver \
    && useradd --system --gid mailarchiver --home-dir /app --shell /usr/sbin/nologin mailarchiver \
    && mkdir -p /app/uploads /app/logs /app/DataProtection-Keys /tmp \
    && chown -R mailarchiver:mailarchiver /app /tmp

COPY --from=build /app/publish .
RUN chown -R mailarchiver:mailarchiver /app

USER mailarchiver
EXPOSE 5000

ENTRYPOINT ["dotnet", "MailArchiver.dll"]
