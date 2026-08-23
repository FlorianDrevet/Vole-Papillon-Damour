FROM node:22-alpine AS build
WORKDIR /src/Website

COPY Website/package.json Website/package-lock.json ./
RUN npm install --no-audit --no-fund

COPY SharedUi ../SharedUi
COPY Website .

ARG API_URL=https://api.example.com

ARG APPINSIGHTS_CONNECTION_STRING=
# Angular compiles security.allowedHosts into the SSR server, so the host the
# app is served from has to be known when the bundle is built. Left empty the
# placeholder simply stays in the list: the schema requires unique entries, so a
# default that repeats one already there would fail validation.
ARG DEPLOY_HOST=

RUN sed -i "s#https://vole-papillon-damour-backend.onrender.com#${API_URL}#g" src/environments/environment.ts \
 && sed -i "s#__APPINSIGHTS_CONNECTION_STRING__#${APPINSIGHTS_CONNECTION_STRING}#g" src/environments/environment.ts \
 && if [ -n "${DEPLOY_HOST}" ]; then sed -i "s#__DEPLOY_HOST__#${DEPLOY_HOST}#g" angular.json; fi \
 && npm run build

FROM node:22-alpine AS final
WORKDIR /app

ENV NODE_ENV=production
ENV PORT=8080

COPY Website/package.json Website/package-lock.json ./
RUN npm install --omit=dev --no-audit --no-fund

COPY --from=build /src/Website/dist/vole_papillon_damour_website/browser ./browser
COPY --from=build /src/Website/dist/vole_papillon_damour_website/server ./server

EXPOSE 8080

CMD ["node", "server/server.mjs"]
