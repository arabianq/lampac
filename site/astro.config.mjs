import { defineConfig } from "astro/config";
import sitemap from "@astrojs/sitemap";

const hidden = ["/install", "/config", "/modules", "/api", "/changelog"];

export default defineConfig({
  site: "https://lampac.dev",
  output: "static",
  i18n: {
    defaultLocale: "ru",
    locales: ["ru", "en"],
    routing: {
      prefixDefaultLocale: false,
    },
  },
  integrations: [
    sitemap({
      i18n: {
        defaultLocale: "ru",
        locales: {
          ru: "ru-RU",
          en: "en-US",
        },
      },
      filter: (page) => !hidden.some((path) => page.includes(path)),
    }),
  ],
});
