# Use Node 20 LTS (Iron) - Current LTS version
# Supports latest npm and provides better security & performance
FROM node:20-alpine3.19

WORKDIR /usr/src/app

RUN mkdir -p ./config

COPY ./src ./src
COPY ./package* ./
COPY ./tsconfig.json ./

ENV PORT=80

# Update Alpine packages for security
RUN apk update && apk upgrade

# Install OpenSSL for certificate generation
RUN apk add --no-cache openssl

# Generate self-signed certificate for local HTTPS development
# This allows Azure SDK to send bearer tokens over localhost
RUN mkdir -p /etc/ssl/private /etc/ssl/certs && \
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
    -keyout /etc/ssl/private/selfsigned.key \
    -out /etc/ssl/certs/selfsigned.crt \
    -subj "/C=US/ST=State/L=City/O=LocalDev/CN=api-emulator"

# Upgrade to latest npm (compatible with Node 20+)
RUN npm install -g npm

# Docker User Namespace remapping issues in npm 9.x
# Refer to https://azureossd.github.io/2022/06/30/Docker-User-Namespace-remapping-issues/#npm-specific-issues-causing-userns-remap-exceptions
# Find any affected files and do a chown to root
RUN npm install && find ./node_modules/ ! -user root -exec chown root:root {} \;

# Compile TypeScript to JavaScript
RUN ./node_modules/typescript/bin/tsc

EXPOSE 80

ENTRYPOINT [ "node" ]
CMD [ "./dist/index.js" ]
