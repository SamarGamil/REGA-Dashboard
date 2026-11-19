ARG REGISTRY

FROM ${REGISTRY}/docker-public/node:21.7.2-bookworm-slim AS build
WORKDIR /code
ENV NPM_CONFIG_USERCONFIG=/code/.npmrc
COPY . .
RUN npm install --force --verbose --no-audit
RUN npm run build

RUN mkdir /output
RUN tar -czvf /output/app.tar.gz -C dist .
