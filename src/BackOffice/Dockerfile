FROM node:22-alpine AS build
WORKDIR /src/BackOffice

COPY BackOffice/package.json BackOffice/package-lock.json ./
RUN npm install --no-audit --no-fund

COPY SharedUi ../SharedUi
COPY BackOffice .

ARG API_URL=https://api.example.com
ARG WEBSITE_URL=https://website.example.com/accueil

ARG APPINSIGHTS_CONNECTION_STRING=

RUN sed -i "s#https://vole-papillon-damour-backend.onrender.com#${API_URL}#g" src/environments/environment.ts \
 && sed -i "s#https://vole-papillon-damour-website.onrender.com/accueil#${WEBSITE_URL}#g" src/environments/environment.ts \
 && sed -i "s#__APPINSIGHTS_CONNECTION_STRING__#${APPINSIGHTS_CONNECTION_STRING}#g" src/environments/environment.ts \
 && npm run build

FROM nginx:1.27-alpine AS final
COPY BackOffice/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/BackOffice/dist/vole_papillon_damour_backoffice_v2/browser /usr/share/nginx/html

EXPOSE 8080
