# Build stage
FROM node:20-alpine AS builder

WORKDIR /app

# Copy package files
COPY package.json package-lock.json* ./

# Install dependencies
RUN npm install

# Copy source code
COPY src/ ./src/
COPY tsconfig.json ./

# Build the TypeScript
RUN npm run build

# RUN npm run simple

# Runtime stage
FROM node:20-alpine

WORKDIR /app

# Copy package files
COPY package.json package-lock.json* ./

# Install production dependencies only
RUN npm install --omit=dev

# Copy built application from builder
COPY --from=builder /app/dist ./dist

# Set default command to run the built JavaScript directly
 CMD ["node", "dist/index.js", "complex"]
