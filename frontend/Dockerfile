# JAG-77: sitio React compilado y servido por nginx
FROM node:22-alpine AS compilacion
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# Vacío: la API se sirve en el mismo dominio (/api), detrás de Caddy
ARG VITE_SERVIDOR=""
ENV VITE_SERVIDOR=$VITE_SERVIDOR
RUN npm test && npm run build

FROM nginx:1.27-alpine
# Plantilla: la imagen de nginx reemplaza ${URL_BACKEND} al iniciar y la deja en conf.d/default.conf
COPY nginx.conf /etc/nginx/templates/default.conf.template
ENV URL_BACKEND=http://localhost:8080
COPY --from=compilacion /app/dist /usr/share/nginx/html
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=5s CMD wget -q -O /dev/null http://127.0.0.1/ || exit 1
