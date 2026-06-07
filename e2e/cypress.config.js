const { defineConfig } = require("cypress");

// All environment-specific values are overridable so this runs against any dev instance without edits:
//   CYPRESS_BASE_URL      the running CMS (default https://localhost:7207)
//   CYPRESS_ADMIN_USER    admin username (default "admin")
//   CYPRESS_ADMIN_PASSWORD  admin password (no default — must be supplied)
//   CYPRESS_LOGIN_PATH    the login page path (default "/account/login")
module.exports = defineConfig({
  e2e: {
    baseUrl: process.env.CYPRESS_BASE_URL || "https://localhost:7207",
    chromeWebSecurity: false, // dev TLS / self-signed cert
    defaultCommandTimeout: 10000,
    video: false,
    env: {
      adminUser: process.env.CYPRESS_ADMIN_USER || "admin",
      adminPassword: process.env.CYPRESS_ADMIN_PASSWORD || "",
      loginPath: process.env.CYPRESS_LOGIN_PATH || "/account/login",
    },
    setupNodeEvents(on, config) {
      return config;
    },
  },
});
