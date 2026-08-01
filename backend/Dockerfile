# syntax=docker/dockerfile:1

# argon2 needs a native addon compiled against musl libc — python3/make/g++
# are only needed to produce node_modules once, never in the final image.
FROM node:20-alpine AS deps
RUN apk add --no-cache python3 make g++
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --omit=dev

# ---- build stage (full deps, incl. dev, to run `nest build`) ----
FROM node:20-alpine AS build
RUN apk add --no-cache python3 make g++
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY tsconfig*.json nest-cli.json ./
COPY src ./src
RUN npm run build

# ---- runtime stage (no build toolchain, no dev deps) ----
FROM node:20-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
COPY package.json ./
COPY --from=deps /app/node_modules ./node_modules
COPY --from=build /app/dist ./dist

# No secrets baked in: all configuration (Mongo URI, JWT secrets, admin seed,
# etc.) comes from environment variables at runtime — see .env.example.
USER node
EXPOSE 3000
CMD ["node", "dist/main"]
