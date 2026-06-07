import "./commands";

// The dev server uses a self-signed cert and Blazor Server may surface benign reconnection noise;
// don't let unrelated app exceptions fail an assertion-focused E2E run.
Cypress.on("uncaught:exception", () => false);
