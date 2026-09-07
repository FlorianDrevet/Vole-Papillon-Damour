FROM node:24.15.0-alpine AS build
WORKDIR /src/Catalog

COPY Catalog/package.json Catalog/package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY SharedUi ../SharedUi
COPY Catalog .

ARG API_URL=https://api.example.com
ARG DEPLOY_HOST=
ARG GOOGLE_ANALYTICS_MEASUREMENT_ID=
ARG CLARITY_PROJECT_ID=

RUN sed -i "s#https://vole-papillon-damour-backend.onrender.com#${API_URL}#g" src/environments/environment.ts \
 && if [ -n "${GOOGLE_ANALYTICS_MEASUREMENT_ID}" ]; then sed -i "s#__GOOGLE_ANALYTICS_MEASUREMENT_ID__#${GOOGLE_ANALYTICS_MEASUREMENT_ID}#g" src/environments/environment.ts; fi \
 && if [ -n "${CLARITY_PROJECT_ID}" ]; then sed -i "s#__CLARITY_PROJECT_ID__#${CLARITY_PROJECT_ID}#g" src/environments/environment.ts; fi \
 && if [ -n "${DEPLOY_HOST}" ]; then sed -i "s#__DEPLOY_HOST__#${DEPLOY_HOST}#g" angular.json; fi \
 && npm run build

FROM node:24.15.0-alpine AS final
WORKDIR /app

ENV NODE_ENV=production
ENV PORT=8080

COPY Catalog/package.json Catalog/package-lock.json ./
RUN npm ci --omit=dev --no-audit --no-fund

COPY --from=build /src/Catalog/dist/vole_papillon_damour_catalog/browser ./browser
COPY --from=build /src/Catalog/dist/vole_papillon_damour_catalog/server ./server

EXPOSE 8080

CMD ["node", "server/server.mjs"]
