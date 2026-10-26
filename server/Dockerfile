# NextGen Drive authoritative server. Build context is server/.
FROM node:22-alpine

WORKDIR /app
ENV NODE_ENV=production

# Dependencies first, so code changes do not reinstall packages
COPY package.json package-lock.json ./
RUN npm ci --omit=dev && npm cache clean --force

COPY src ./src
COPY sql ./sql

# The server does not need root; the node user owns nothing but reads the app
USER node

EXPOSE 8080
CMD ["node", "src/auction_server.js"]
