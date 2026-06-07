// Log in through the MindAttic.Authentication login page (posts to /_ma-auth/login). Selectors are
// resilient: the username/password inputs are matched by type, the submit by role/text. Adjust the
// loginPath env if your auth UI differs.
Cypress.Commands.add("loginAsAdmin", () => {
  const user = Cypress.env("adminUser");
  const password = Cypress.env("adminPassword");
  if (!password) {
    throw new Error(
      "CYPRESS_ADMIN_PASSWORD is not set. Provide the admin password (see e2e/README.md)."
    );
  }

  cy.session([user, password], () => {
    cy.visit(Cypress.env("loginPath"));
    // Username / email field (first text-ish input), then the password field.
    cy.get('input[type="text"], input[type="email"], input[name*="user" i]')
      .first()
      .clear()
      .type(user);
    cy.get('input[type="password"]').first().clear().type(password, { log: false });
    cy.get('button[type="submit"], input[type="submit"]').first().click();
    // Land somewhere authenticated (not back on the login page).
    cy.location("pathname", { timeout: 15000 }).should(
      "not.include",
      Cypress.env("loginPath")
    );
  });
});

// Open the admin "New Page" editor on /admin/pages.
Cypress.Commands.add("startNewPage", () => {
  cy.visit("/admin/pages");
  cy.contains("button, a", /new|add/i)
    .first()
    .click();
});
