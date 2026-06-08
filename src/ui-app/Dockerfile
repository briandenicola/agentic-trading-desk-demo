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

# Runtime stage
FROM nginx:alpine AS runtime

# Copy build artifacts
COPY --from=build /app/dist /usr/share/nginx/html

# Copy nginx config as template for envsubst
COPY nginx.conf /etc/nginx/templates/default.conf.template

# Default orchestration API URL (will be substituted at runtime)
ENV ORCHESTRATION_API_URL=http://orchestration-api:8080

# Run nginx on an unprivileged port as the non-root nginx user.
ARG APP_UID=101
RUN mkdir -p /var/cache/nginx /var/run /var/log/nginx /etc/nginx/conf.d \
    && chown -R $APP_UID:0 /usr/share/nginx/html /etc/nginx/templates /etc/nginx/conf.d /var/cache/nginx /var/run /var/log/nginx
USER $APP_UID

EXPOSE 8080

# nginx image default entrypoint handles envsubst for templates
