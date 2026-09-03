FROM node:24.15.0-alpine AS build
WORKDIR /src/Scan

COPY Scan/package.json Scan/package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY SharedUi ../SharedUi
COPY Scan .

ARG API_URL=https://api.example.com
ARG APPINSIGHTS_CONNECTION_STRING=

RUN sed -i "s#https://vole-papillon-damour-backend.onrender.com#${API_URL}#g" src/environments/environment.ts \
 && sed -i "s#__APPINSIGHTS_CONNECTION_STRING__#${APPINSIGHTS_CONNECTION_STRING}#g" src/environments/environment.ts \
 && npm run build -- --configuration production

FROM nginx:1.27-alpine AS final
COPY Scan/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/Scan/dist/vole_papillon_damour_scan/browser /usr/share/nginx/html

EXPOSE 8080
