# syntax=docker/dockerfile:1

# Build stage
FROM node:22-alpine AS build
WORKDIR /app

# Copy package files and install dependencies
COPY package.json package-lock.json ./
RUN npm ci

# Copy source and build
COPY . .
RUN npm run build

# Runtime stage — nginx-unprivileged is purpose-built to run rootless:
# it uses uid 101, writes its pid to /tmp, drops the privileged `user`
# directive, and listens on 8080, so there is no /run/nginx.pid permission error.
FROM nginxinc/nginx-unprivileged:alpine AS runtime

# Copy build artifacts
COPY --from=build /app/dist /usr/share/nginx/html

# Copy nginx config as template for envsubst
COPY nginx.conf /etc/nginx/templates/default.conf.template

# Default orchestration API URL (will be substituted at runtime)
ENV ORCHESTRATION_API_URL=http://orchestration-api:8080

EXPOSE 8080

# nginx-unprivileged default entrypoint handles envsubst and runs as uid 101.
