/// <reference types="cypress" />

describe('Platform Management', () => {
    beforeEach(() => {
        cy.login('test@test.com', 'password123');
        cy.visit('/platforms');
    });

    it('should allow a user to request a new platform', () => {
        cy.contains('Request New Platform').click();
        cy.url().should('include', '/platforms/request');

        cy.get('input[name="name"]').type('New Cypress Platform');
        cy.get('input[name="baseUrl"]').type('https://cypress.io');
        cy.get('input[name="commissionRate"]').type('0.15');
        cy.get('button[type="submit"]').click();

        cy.url().should('include', '/platforms');
        cy.contains('New Cypress Platform').should('be.visible');
    });
});