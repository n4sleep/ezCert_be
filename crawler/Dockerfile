FROM node:24-alpine
WORKDIR /app
# dist/ is compiled on the HOST (corp npm registry is unreachable from inside
# Docker; tsx/esbuild platform binaries are avoided entirely by running plain
# node on precompiled JS).
COPY package.json package-lock.json ./
COPY node_modules ./node_modules
COPY dist ./dist
ENV PORT=8081
EXPOSE 8081
CMD ["node", "dist/server.js"]
